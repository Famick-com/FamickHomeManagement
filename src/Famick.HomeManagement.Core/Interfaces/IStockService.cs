using Famick.HomeManagement.Core.DTOs.Stock;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing stock entries (inventory).
/// </summary>
public interface IStockService
{
    /// <summary>
    /// Add a new stock entry.
    /// </summary>
    Task<StockEntryDto> AddStockAsync(AddStockRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add multiple stock entries in a batch.
    /// Supports both individual items with unique dates and bulk items.
    /// </summary>
    Task<List<StockEntryDto>> AddStockBatchAsync(AddStockBatchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a stock entry by ID.
    /// </summary>
    Task<StockEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all stock entries with optional filters.
    /// </summary>
    Task<List<StockEntryDto>> ListAsync(StockFilterRequest? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all stock entries for a specific product.
    /// </summary>
    Task<List<StockEntryDto>> GetByProductAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all stock entries at a specific location.
    /// </summary>
    Task<List<StockEntryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adjust a stock entry's amount or details.
    /// </summary>
    Task<StockEntryDto> AdjustStockAsync(Guid id, AdjustStockRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a stock entry as opened with tracking mode.
    /// </summary>
    Task<StockEntryDto> OpenProductAsync(Guid id, OpenProductRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consume (use/remove) stock from an entry.
    /// Returns the affected product's refreshed overview row so callers can patch one row
    /// instead of reloading the list, or null when that row no longer exists.
    /// </summary>
    Task<StockOverviewItemDto?> ConsumeStockAsync(Guid id, ConsumeStockRequest request, StockOverviewFilterRequest? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a stock entry.
    /// Returns the affected product's refreshed overview row, or null when that row no longer exists.
    /// </summary>
    Task<StockOverviewItemDto?> DeleteAsync(Guid id, StockOverviewFilterRequest? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get stock entries for a product at a specific location.
    /// </summary>
    Task<List<StockEntryDto>> GetByProductAndLocationAsync(Guid productId, Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get aggregate statistics for stock overview page.
    /// </summary>
    Task<StockStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get stock overview grouped by product.
    /// </summary>
    Task<List<StockOverviewItemDto>> GetOverviewAsync(StockOverviewFilterRequest? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the single stock-overview row that owns <paramref name="productId"/>, or null when
    /// that row no longer exists because the product holds no stock.
    ///
    /// For a child variant this returns the PARENT's row: children are folded into their parent
    /// and have no row of their own in the overview.
    ///
    /// <paramref name="filter"/> honours the same LocationId/ProductGroupId narrowing as
    /// <see cref="GetOverviewAsync"/>, so a patched row matches what a full reload would show.
    /// Status and SearchTerm are ignored — those decide list membership, not row content.
    /// </summary>
    Task<StockOverviewItemDto?> GetOverviewItemAsync(Guid productId, StockOverviewFilterRequest? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get stock log entries for journal display.
    /// </summary>
    Task<List<StockLogDto>> GetLogAsync(int? limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quick consume action - consumes from oldest entry (FEFO).
    /// Returns the product's refreshed overview row, or null when that row no longer exists.
    /// </summary>
    Task<StockOverviewItemDto?> QuickConsumeAsync(QuickConsumeRequest request, StockOverviewFilterRequest? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quick add action - adds stock using product's default location.
    /// Returns the product's refreshed overview row.
    /// </summary>
    Task<StockOverviewItemDto?> QuickAddAsync(Guid productId, decimal amount = 1, DateTime? bestBeforeDate = null, StockOverviewFilterRequest? filter = null, CancellationToken cancellationToken = default);
}
