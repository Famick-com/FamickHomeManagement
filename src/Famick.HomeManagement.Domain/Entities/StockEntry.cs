using System;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities
{
    /// <summary>
    /// Represents a stock entry for a product in inventory.
    /// Each stock entry represents a specific batch of a product with its own best before date,
    /// purchase information, and location.
    /// </summary>
    public class StockEntry : BaseEntity, ITenantEntity
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
        /// Quantity of product in this stock entry (in stock quantity units)
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Best before date for this stock entry
        /// </summary>
        public DateTime? BestBeforeDate { get; set; }

        /// <summary>
        /// Date when this stock was purchased
        /// </summary>
        public DateTime PurchasedDate { get; set; }

        /// <summary>
        /// Unique identifier for this stock entry (used for tracking across transactions)
        /// Format: UUID or custom identifier
        /// </summary>
        public string StockId { get; set; } = string.Empty;

        /// <summary>
        /// Price paid for this stock entry (per stock quantity unit)
        /// </summary>
        public decimal? Price { get; set; }

        /// <summary>
        /// Whether this stock entry is opened
        /// </summary>
        public bool Open { get; set; }

        /// <summary>
        /// Date when this stock entry was opened
        /// </summary>
        public DateTime? OpenedDate { get; set; }

        /// <summary>
        /// How the remaining amount is tracked when open (percentage or count).
        /// Null when not open or tracking mode not yet set.
        /// </summary>
        public OpenTrackingMode? OpenTrackingMode { get; set; }

        /// <summary>
        /// Original amount before the product was opened.
        /// Used to calculate percentage remaining when OpenTrackingMode is Percentage.
        /// </summary>
        public decimal? OriginalAmount { get; set; }

        /// <summary>
        /// Location where this stock is stored (overrides product default location if set)
        /// </summary>
        public Guid? LocationId { get; set; }

        /// <summary>
        /// Shopping location where this stock was purchased
        /// </summary>
        public Guid? ShoppingLocationId { get; set; }

        /// <summary>
        /// Optional note about this stock entry
        /// </summary>
        public string? Note { get; set; }

        // Navigation properties
        public virtual Product? Product { get; set; }
        public virtual Location? Location { get; set; }
    }
}
