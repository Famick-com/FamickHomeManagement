using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for address operations including normalization/geocoding
/// </summary>
[ApiController]
[Route("api/v1/addresses")]
[Authorize]
public class AddressController : ApiControllerBase
{
    private readonly IAddressNormalizationService _addressService;
    private readonly IAddressService _addressSearchService;
    private readonly IValidator<NormalizeAddressRequest> _normalizeValidator;

    public AddressController(
        IAddressNormalizationService addressService,
        IAddressService addressSearchService,
        IValidator<NormalizeAddressRequest> normalizeValidator,
        ITenantProvider tenantProvider,
        ILogger<AddressController> logger)
        : base(tenantProvider, logger)
    {
        _addressService = addressService;
        _addressSearchService = addressSearchService;
        _normalizeValidator = normalizeValidator;
    }

    /// <summary>
    /// Searches existing addresses within the current tenant
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<AddressDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Search(
        [FromQuery] string query = "",
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return ApiResponse(new List<AddressDto>());

        limit = Math.Clamp(limit, 1, 25);
        var results = await _addressSearchService.SearchAsync(query, limit, cancellationToken);
        return ApiResponse(results);
    }

    /// <summary>
    /// Normalizes and geocodes an address via Geoapify
    /// </summary>
    /// <remarks>
    /// Returns the normalized/verified address with latitude/longitude coordinates.
    /// The response includes a confidence score indicating match quality.
    /// Returns null if the address could not be found or verified.
    /// </remarks>
    [HttpPost("normalize")]
    [ProducesResponseType(typeof(NormalizedAddressResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Normalize(
        [FromBody] NormalizeAddressRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _normalizeValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Normalizing address: {AddressLine1}, {City}, {StateProvince}",
            request.AddressLine1, request.City, request.StateProvince);

        var result = await _addressService.NormalizeAsync(request, cancellationToken);

        if (result == null)
        {
            return NotFoundResponse("Could not normalize address. Please verify the address is correct.");
        }

        return ApiResponse(result);
    }

    /// <summary>
    /// Normalizes and geocodes an address, returning multiple suggestions
    /// </summary>
    /// <remarks>
    /// Returns multiple address suggestions sorted by confidence.
    /// Useful when the input address is ambiguous or incomplete.
    /// </remarks>
    [HttpPost("normalize/suggestions")]
    [ProducesResponseType(typeof(List<NormalizedAddressResult>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> NormalizeSuggestions(
        [FromBody] NormalizeAddressRequest request,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _normalizeValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Getting address suggestions for: {AddressLine1}, {City}, {StateProvince} (limit: {Limit})",
            request.AddressLine1, request.City, request.StateProvince, limit);

        var results = await _addressService.NormalizeSuggestionsAsync(request, limit, cancellationToken);

        return ApiResponse(results);
    }

    /// <summary>
    /// Unified autocomplete: merges local address search with the external
    /// provider (Smarty US Autocomplete Pro) in parallel.
    /// </summary>
    /// <remarks>
    /// Each suggestion carries a <c>SuggestionId</c> the client sends back
    /// through <c>POST /resolve-suggestion</c> to materialize a persisted
    /// <see cref="AddressDto"/>. Local hits also include <c>AddressId</c>.
    /// </remarks>
    [HttpGet("autocomplete")]
    [ProducesResponseType(typeof(List<AddressSuggestionDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Autocomplete(
        [FromQuery] string query = "",
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return ApiResponse(new List<AddressSuggestionDto>());

        limit = Math.Clamp(limit, 1, 25);
        var results = await _addressSearchService.AutocompleteAsync(query, limit, cancellationToken);
        return ApiResponse(results);
    }

    /// <summary>
    /// Resolves a suggestion returned from <c>GET /autocomplete</c> into a
    /// persisted <see cref="AddressDto"/>, creating it if necessary.
    /// </summary>
    /// <remarks>
    /// Returns 410 Gone when the suggestion is unknown or has expired; the
    /// client should re-query <c>GET /autocomplete</c> in that case.
    /// </remarks>
    [HttpPost("resolve-suggestion")]
    [ProducesResponseType(typeof(AddressDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(410)]
    public async Task<IActionResult> ResolveSuggestion(
        [FromBody] ResolveAddressSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || request.SuggestionId == Guid.Empty)
            return ErrorResponse("SuggestionId is required.");

        var result = await _addressSearchService.ResolveSuggestionAsync(request, cancellationToken);
        if (result == null)
            return StatusCode(410, new { message = "Suggestion not found or expired. Please re-query." });

        return ApiResponse(result);
    }

    /// <summary>
    /// Expands an autocomplete suggestion that has multiple secondary units
    /// (apt / suite / etc.) into the canonical list of unit-level suggestions.
    /// </summary>
    /// <remarks>
    /// Used when a parent <see cref="AddressSuggestionDto"/> has
    /// <c>SecondaryCount &gt; 1</c>. The returned children carry their own
    /// cached <c>SuggestionId</c>s that the client passes to
    /// <c>POST /resolve-suggestion</c> after the user picks a unit. Returns
    /// 410 Gone when the parent suggestion is unknown or has expired.
    /// </remarks>
    [HttpGet("secondaries/{suggestionId:guid}")]
    [ProducesResponseType(typeof(List<AddressSuggestionDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(410)]
    public async Task<IActionResult> Secondaries(
        Guid suggestionId,
        CancellationToken cancellationToken = default)
    {
        if (suggestionId == Guid.Empty)
            return ErrorResponse("SuggestionId is required.");

        var result = await _addressSearchService.ExpandSuggestionSecondariesAsync(suggestionId, cancellationToken);
        if (result == null)
            return StatusCode(410, new { message = "Suggestion not found or expired. Please re-query." });

        return ApiResponse(result);
    }

    /// <summary>
    /// Manual-entry path: standardizes the supplied address to USPS format via
    /// the external provider when available, dedupes, and persists. Returns
    /// the resulting <see cref="AddressDto"/>.
    /// </summary>
    [HttpPost("standardize-manual")]
    [ProducesResponseType(typeof(AddressDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> StandardizeManual(
        [FromBody] StandardizeAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null ||
            (string.IsNullOrWhiteSpace(request.AddressLine1) && string.IsNullOrWhiteSpace(request.City)))
        {
            return ErrorResponse("At least AddressLine1 or City must be supplied.");
        }

        var result = await _addressSearchService.StandardizeAndCreateAsync(request, cancellationToken);
        return ApiResponse(result);
    }
}
