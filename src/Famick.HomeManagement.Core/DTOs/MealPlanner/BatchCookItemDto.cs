namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class BatchCookItemDto
{
    public Guid Id { get; set; }
    public Guid SourceEntryId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal? TotalQuantity { get; set; }
    public Guid? QuantityUnitId { get; set; }
    public string? QuantityUnitName { get; set; }
    public List<BatchCookItemUsageDto> Usages { get; set; } = new();
}

public class BatchCookItemUsageDto
{
    public Guid Id { get; set; }
    public Guid BatchCookItemId { get; set; }
    public Guid DependentEntryId { get; set; }
    public string? DependentEntryMealName { get; set; }
    public int DependentEntryDayOfWeek { get; set; }
    public decimal? QuantityUsed { get; set; }
}

public class CreateBatchCookItemRequest
{
    public Guid ProductId { get; set; }
    public decimal? TotalQuantity { get; set; }
    public Guid? QuantityUnitId { get; set; }
}

public class LinkBatchCookItemRequest
{
    public Guid BatchCookItemId { get; set; }
    public decimal? QuantityUsed { get; set; }
}

public class BatchCookSuggestionDto
{
    public Guid BatchCookItemId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid SourceEntryId { get; set; }
    public string? SourceMealName { get; set; }
    public int SourceDayOfWeek { get; set; }
    public decimal? RemainingQuantity { get; set; }
    public string? QuantityUnitName { get; set; }
}
