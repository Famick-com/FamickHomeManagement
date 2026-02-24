using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities
{
    /// <summary>
    /// Represents a product group/category for organizing products.
    /// Example: "Dairy", "Beverages", "Snacks", "Frozen Foods"
    /// </summary>
    public class ProductGroup : BaseEntity, ITenantEntity
    {
        /// <summary>
        /// Tenant identifier for multi-tenancy isolation
        /// </summary>
        public Guid TenantId { get; set; }

        /// <summary>
        /// Product group name (unique per tenant)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of the product group
        /// </summary>
        public string? Description { get; set; }

        // Navigation properties
        /// <summary>
        /// Products belonging to this group
        /// </summary>
        public virtual ICollection<Product>? Products { get; set; }
    }
}
