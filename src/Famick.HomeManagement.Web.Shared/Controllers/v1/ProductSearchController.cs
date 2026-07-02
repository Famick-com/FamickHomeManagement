using Famick.HomeManagement.Core.DTOs.ProductLookup;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// Unified product search. One endpoint that fans out to the master catalog, local products,
/// the linked store, and (opt-in) external plugins, merges duplicates, and ranks per context.
/// Legacy search endpoints delegate here.
/// </summary>
[ApiController]
[Route("api/v1/products")]
[Authorize]
public class ProductSearchController : ApiControllerBase
{
    private readonly IUnifiedProductSearchService _searchService;

    public ProductSearchController(
        IUnifiedProductSearchService searchService,
        ITenantProvider tenantProvider,
        ILogger<ProductSearchController> logger)
        : base(tenantProvider, logger)
    {
        _searchService = searchService;
    }

    /// <summary>
    /// Unified product search. Auto-detects barcode vs name.
    /// </summary>
    [HttpPost("search-unified")]
    [ProducesResponseType(typeof(ProductLookupResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> SearchUnified(
        [FromBody] UnifiedProductSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error_message = "Query is required" });
        }

        var results = await _searchService.SearchAsync(request, cancellationToken);
        return ApiResponse(new ProductLookupResponse { Results = results });
    }
}
