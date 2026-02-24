using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities
{
    /// <summary>
    /// Represents a shopping list container (can have multiple lists per tenant).
    /// Example: "Default", "Weekly Shopping", "Party Supplies", "Hardware Store"
    /// </summary>
    public class ShoppingList : BaseEntity, ITenantEntity
    {
        /// <summary>
        /// Tenant identifier for multi-tenancy isolation
        /// </summary>
        public Guid TenantId { get; set; }

        /// <summary>
        /// Shopping list name (unique per tenant)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of the shopping list
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// The store this shopping list is associated with
        /// </summary>
        public Guid ShoppingLocationId { get; set; }

        // Navigation properties

        /// <summary>
        /// The store this shopping list is associated with
        /// </summary>
        public virtual ShoppingLocation? ShoppingLocation { get; set; }
        /// <summary>
        /// Items in this shopping list
        /// </summary>
        public virtual ICollection<ShoppingListItem>? Items { get; set; }
    }
}
