using System;
using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities
{
    /// <summary>
    /// Represents a transaction in the stock log (audit trail for all stock movements).
    /// Every stock transaction (purchase, consume, inventory correction, etc.) is logged here.
    /// </summary>
    public class StockLog : BaseEntity, ITenantEntity
    {
        /// <summary>
        /// Tenant ID for multi-tenancy support
        /// </summary>
        public Guid TenantId { get; set; }

        /// <summary>
        /// Product ID (foreign key to Products table)
        /// </summary>
        public Guid ProductId { get; set; }

        /// <summary>
        /// Quantity involved in this transaction (positive for additions, negative for removals)
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Best before date for the stock involved in this transaction
        /// </summary>
        public DateTime? BestBeforeDate { get; set; }

        /// <summary>
        /// Purchase date for the stock (for purchase transactions)
        /// </summary>
        public DateTime? PurchasedDate { get; set; }

        /// <summary>
        /// Date when stock was used/consumed (for consume transactions)
        /// </summary>
        public DateTime? UsedDate { get; set; }

        /// <summary>
        /// Whether the stock was spoiled in this transaction (1 = spoiled, 0 = not spoiled)
        /// </summary>
        public int Spoiled { get; set; }

        /// <summary>
        /// Stock ID that links this transaction to a specific stock entry
        /// </summary>
        public string StockId { get; set; } = string.Empty;

        /// <summary>
        /// Type of transaction:
        /// - "purchase" - Stock added via purchase
        /// - "consume" - Stock consumed/used
        /// - "inventory-correction" - Manual adjustment
        /// - "product-opened" - Product was opened
        /// - "stock-edit-new" - Stock entry edited
        /// - "stock-edit-old" - Stock entry before edit (negative amount)
        /// - "self-production" - Stock produced internally
        /// </summary>
        public string TransactionType { get; set; } = string.Empty;

        /// <summary>
        /// Price for this transaction (per stock quantity unit)
        /// </summary>
        public decimal? Price { get; set; }

        /// <summary>
        /// Whether this transaction has been undone
        /// </summary>
        public bool Undone { get; set; }

        /// <summary>
        /// Timestamp when this transaction was undone
        /// </summary>
        public DateTime? UndoneTimestamp { get; set; }

        /// <summary>
        /// Date when product was opened (for product-opened transactions)
        /// </summary>
        public DateTime? OpenedDate { get; set; }

        /// <summary>
        /// Location involved in this transaction
        /// </summary>
        public Guid? LocationId { get; set; }

        /// <summary>
        /// Recipe ID if this transaction is related to a recipe (for consume transactions)
        /// </summary>
        public Guid? RecipeId { get; set; }

        /// <summary>
        /// Correlation ID for grouping related transactions
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Transaction ID for external integration tracking
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// Reference to the stock entry row ID (foreign key to stock table)
        /// </summary>
        public Guid? StockRowId { get; set; }

        /// <summary>
        /// Shopping location where stock was purchased (for purchase transactions)
        /// </summary>
        public Guid? ShoppingLocationId { get; set; }

        /// <summary>
        /// User who performed this transaction
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Optional note about this transaction
        /// </summary>
        public string? Note { get; set; }

        // Navigation properties
        public virtual Product? Product { get; set; }
        public virtual Location? Location { get; set; }
        public virtual User? User { get; set; }
        public virtual StockEntry? StockRow { get; set; }
    }
}
