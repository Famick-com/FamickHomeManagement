using Famick.HomeManagement.Core.DTOs.Stock;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing stock entries (inventory)
/// </summary>
[ApiController]
[Route("api/v1/stock")]
[Authorize]
public class StockController : ApiControllerBase
{
    private readonly IStockService _stockService;

    public StockController(
        IStockService stockService,
        ITenantProvider tenantProvider,
        ILogger<StockController> logger)
        : base(tenantProvider, logger)
    {
        _stockService = stockService;
    }

    /// <summary>
    /// Lists all stock entries with optional filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<StockEntryDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> List(
        [FromQuery] StockFilterRequest? filter,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing stock entries for tenant {TenantId}", TenantId);

        var entries = await _stockService.ListAsync(filter, cancellationToken);
        return ApiResponse(entries);
    }

    /// <summary>
    /// Gets a specific stock entry by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(StockEntryDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting stock entry {StockId} for tenant {TenantId}", id, TenantId);

        var entry = await _stockService.GetByIdAsync(id, cancellationToken);

        if (entry == null)
        {
            return NotFoundResponse($"Stock entry with ID {id} not found");
        }

        return ApiResponse(entry);
    }

    /// <summary>
    /// Gets all stock entries for a specific product
    /// </summary>
    [HttpGet("by-product/{productId}")]
    [ProducesResponseType(typeof(List<StockEntryDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetByProduct(
        Guid productId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting stock entries for product {ProductId}, tenant {TenantId}", productId, TenantId);

        var entries = await _stockService.GetByProductAsync(productId, cancellationToken);
        return ApiResponse(entries);
    }

    /// <summary>
    /// Gets all stock entries at a specific location
    /// </summary>
    [HttpGet("by-location/{locationId}")]
    [ProducesResponseType(typeof(List<StockEntryDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetByLocation(
        Guid locationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting stock entries at location {LocationId}, tenant {TenantId}", locationId, TenantId);

        var entries = await _stockService.GetByLocationAsync(locationId, cancellationToken);
        return ApiResponse(entries);
    }

    /// <summary>
    /// Gets stock entries for a product at a specific location
    /// </summary>
    [HttpGet("by-product/{productId}/location/{locationId}")]
    [ProducesResponseType(typeof(List<StockEntryDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetByProductAndLocation(
        Guid productId,
        Guid locationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting stock entries for product {ProductId} at location {LocationId}, tenant {TenantId}",
            productId, locationId, TenantId);

        var entries = await _stockService.GetByProductAndLocationAsync(productId, locationId, cancellationToken);
        return ApiResponse(entries);
    }

    /// <summary>
    /// Adds a new stock entry
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(StockEntryDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> AddStock(
        [FromBody] AddStockRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding stock for product {ProductId}, tenant {TenantId}", request.ProductId, TenantId);

        try
        {
            var entry = await _stockService.AddStockAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = entry.Id }, entry);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
    }

    /// <summary>
    /// Adds multiple stock entries in a batch (supports individual date tracking)
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(List<StockEntryDto>), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> AddStockBatch(
        [FromBody] AddStockBatchRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding stock batch for product {ProductId}, tenant {TenantId}", request.ProductId, TenantId);

        try
        {
            var entries = await _stockService.AddStockBatchAsync(request, cancellationToken);
            return CreatedAtAction(nameof(List), entries);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
    }

    /// <summary>
    /// Adjusts a stock entry's amount or details
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(StockEntryDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> AdjustStock(
        Guid id,
        [FromBody] AdjustStockRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adjusting stock entry {StockId}, tenant {TenantId}", id, TenantId);

        try
        {
            var entry = await _stockService.AdjustStockAsync(id, request, cancellationToken);
            return ApiResponse(entry);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
    }

    /// <summary>
    /// Marks a stock entry as opened
    /// </summary>
    [HttpPost("{id}/open")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(StockEntryDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> OpenProduct(
        Guid id,
        [FromBody] OpenProductRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Opening stock entry {StockId}, tenant {TenantId}", id, TenantId);

        try
        {
            var entry = await _stockService.OpenProductAsync(id, request, cancellationToken);
            return ApiResponse(entry);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Consumes (uses/removes) stock from an entry
    /// </summary>
    [HttpPost("{id}/consume")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(StockOverviewItemDto), 200)]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ConsumeStock(
        Guid id,
        [FromBody] ConsumeStockRequest request,
        [FromQuery] Guid? locationId,
        [FromQuery] Guid? productGroupId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Consuming stock entry {StockId}, tenant {TenantId}", id, TenantId);

        try
        {
            var item = await _stockService.ConsumeStockAsync(
                id, request, OverviewFilter(locationId, productGroupId), cancellationToken);
            return OverviewRowResponse(item);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (InsufficientStockException ex)
        {
            return ErrorResponse($"Insufficient stock. Required: {ex.Required}, Available: {ex.Available}");
        }
    }

    /// <summary>
    /// Deletes a stock entry
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(StockOverviewItemDto), 200)]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] Guid? locationId,
        [FromQuery] Guid? productGroupId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting stock entry {StockId}, tenant {TenantId}", id, TenantId);

        try
        {
            var item = await _stockService.DeleteAsync(
                id, OverviewFilter(locationId, productGroupId), cancellationToken);
            return OverviewRowResponse(item);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
    }

    /// <summary>
    /// Gets aggregate statistics for stock overview
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(StockStatisticsDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetStatistics(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting stock statistics for tenant {TenantId}", TenantId);

        var statistics = await _stockService.GetStatisticsAsync(cancellationToken);
        return ApiResponse(statistics);
    }

    /// <summary>
    /// Gets stock overview grouped by product
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(List<StockOverviewItemDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetOverview(
        [FromQuery] StockOverviewFilterRequest? filter,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting stock overview for tenant {TenantId}", TenantId);

        var items = await _stockService.GetOverviewAsync(filter, cancellationToken);
        return ApiResponse(items);
    }

    /// <summary>
    /// Gets stock log entries (journal)
    /// </summary>
    [HttpGet("log")]
    [ProducesResponseType(typeof(List<StockLogDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetLog(
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting stock log for tenant {TenantId}", TenantId);

        var logs = await _stockService.GetLogAsync(limit, cancellationToken);
        return ApiResponse(logs);
    }

    /// <summary>
    /// Quick consume action - consumes from oldest entry (FEFO)
    /// </summary>
    [HttpPost("quick-consume")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(StockOverviewItemDto), 200)]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> QuickConsume(
        [FromBody] QuickConsumeRequest request,
        [FromQuery] Guid? locationId,
        [FromQuery] Guid? productGroupId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Quick consuming product {ProductId}, tenant {TenantId}", request.ProductId, TenantId);

        try
        {
            var item = await _stockService.QuickConsumeAsync(
                request, OverviewFilter(locationId, productGroupId), cancellationToken);
            return OverviewRowResponse(item);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (InsufficientStockException ex)
        {
            return ErrorResponse($"Insufficient stock. Required: {ex.Required}, Available: {ex.Available}");
        }
    }

    /// <summary>
    /// Quick add action - adds stock using product's default location
    /// </summary>
    [HttpPost("quick-add/{productId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(StockOverviewItemDto), 200)]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> QuickAdd(
        Guid productId,
        [FromQuery] decimal amount = 1,
        [FromQuery] DateTime? bestBeforeDate = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] Guid? productGroupId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Quick adding {Amount} to product {ProductId}, tenant {TenantId}", amount, productId, TenantId);

        try
        {
            var item = await _stockService.QuickAddAsync(
                productId, amount, bestBeforeDate, OverviewFilter(locationId, productGroupId), cancellationToken);
            return OverviewRowResponse(item);
        }
        catch (EntityNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
    }

    /// <summary>
    /// The overview narrowing a client has active, so a patched row aggregates the same entries a
    /// full reload would. Null when no narrowing is in play.
    /// </summary>
    private static StockOverviewFilterRequest? OverviewFilter(Guid? locationId, Guid? productGroupId)
        => locationId.HasValue || productGroupId.HasValue
            ? new StockOverviewFilterRequest { LocationId = locationId, ProductGroupId = productGroupId }
            : null;

    /// <summary>
    /// Answers a mutation with the affected product's refreshed overview row, so clients can patch
    /// one row instead of reloading the list. A 204 means the row is gone — the product holds no
    /// stock any more — and should be removed.
    /// </summary>
    private IActionResult OverviewRowResponse(StockOverviewItemDto? item)
        => item is null ? EmptyApiResponse() : ApiResponse(item);
}
