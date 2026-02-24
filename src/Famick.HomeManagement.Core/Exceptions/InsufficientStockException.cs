namespace Famick.HomeManagement.Core.Exceptions;

/// <summary>
/// Exception thrown when there is insufficient stock for an operation
/// </summary>
public class InsufficientStockException : BusinessRuleViolationException
{
    public Guid ProductId { get; }
    public decimal Required { get; }
    public decimal Available { get; }

    public InsufficientStockException(Guid productId, decimal required, decimal available)
        : base("InsufficientStock",
               $"Insufficient stock for product {productId}. Required: {required}, Available: {available}")
    {
        ProductId = productId;
        Required = required;
        Available = available;
    }
}
