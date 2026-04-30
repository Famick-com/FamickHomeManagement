using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// Admin-only operations on the Address table. Currently a single
/// endpoint that rehashes existing rows after the canonicalizer config
/// changes (e.g. enabling libpostal).
/// </summary>
[ApiController]
[Route("api/v1/admin/addresses")]
[Authorize(Policy = "RequireAdmin")]
public class AdminAddressController : ApiControllerBase
{
    private readonly IAddressService _addresses;

    public AdminAddressController(
        IAddressService addresses,
        ITenantProvider tenantProvider,
        ILogger<AdminAddressController> logger)
        : base(tenantProvider, logger)
    {
        _addresses = addresses;
    }

    /// <summary>
    /// Rehashes a batch of <c>Address</c> rows under the currently-configured
    /// canonicalizer. Pass the previous response's <c>nextContinueToken</c>
    /// back as <c>continueToken</c> to fetch the next batch. Idempotent —
    /// repeated calls produce identical hashes.
    /// </summary>
    /// <remarks>
    /// Use after toggling <c>AddressCanonicalizer:Provider</c>: existing
    /// rows have hashes from the previous canonicalizer and won't dedupe
    /// against new writes until rehashed. Loop until <c>hasMore</c> is
    /// false. After rehashing, rows that now collide on hash are NOT
    /// auto-merged — that is a separate, riskier operation.
    /// </remarks>
    [HttpPost("rehash")]
    [ProducesResponseType(typeof(RehashAddressesResult), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Rehash(
        [FromBody] RehashAddressesRequest? request,
        CancellationToken cancellationToken = default)
    {
        request ??= new RehashAddressesRequest();
        var batchSize = request.BatchSize <= 0 ? 500 : request.BatchSize;

        var result = await _addresses.RehashAddressesAsync(batchSize, request.ContinueToken, cancellationToken);
        return ApiResponse(result);
    }
}

public sealed class RehashAddressesRequest
{
    public int BatchSize { get; set; } = 500;
    public Guid? ContinueToken { get; set; }
}
