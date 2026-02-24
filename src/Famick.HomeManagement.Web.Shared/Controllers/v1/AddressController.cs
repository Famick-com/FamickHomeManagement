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
}
