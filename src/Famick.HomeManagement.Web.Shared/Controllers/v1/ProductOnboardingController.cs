using Famick.HomeManagement.Core.DTOs.ProductOnboarding;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

[ApiController]
[Route("api/v1/product-onboarding")]
[Authorize]
public class ProductOnboardingController : ApiControllerBase
{
    private readonly IProductOnboardingService _service;

    public ProductOnboardingController(
        IProductOnboardingService service,
        ITenantProvider tenantProvider,
        ILogger<ProductOnboardingController> logger)
        : base(tenantProvider, logger)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetState(CancellationToken ct)
    {
        var result = await _service.GetStateAsync(TenantId, ct);
        return ApiResponse(result);
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] ProductOnboardingAnswersDto answers, CancellationToken ct)
    {
        var result = await _service.PreviewAsync(answers, ct);
        return ApiResponse(result);
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody] ProductOnboardingCompleteRequest request, CancellationToken ct)
    {
        var result = await _service.CompleteAsync(TenantId, request, ct);
        return ApiResponse(result);
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        await _service.ResetAsync(TenantId, ct);
        return EmptyApiResponse();
    }
}
