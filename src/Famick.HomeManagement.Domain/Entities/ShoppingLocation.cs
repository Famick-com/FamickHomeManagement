using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities
{
    /// <summary>
    /// Represents a shopping location (store, market, shop) where products can be purchased.
    /// Example: "Walmart", "Costco", "Local Farmer's Market", "Online - Amazon"
    /// </summary>
    public class ShoppingLocation : BaseEntity, ITenantEntity
    {
        /// <summary>
        /// Tenant identifier for multi-tenancy isolation
        /// </summary>
        public Guid TenantId { get; set; }

        /// <summary>
        /// Shopping location name (unique per tenant)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description (address, notes, opening hours, etc.)
        /// </summary>
        public string? Description { get; set; }

        // Store Integration fields

        /// <summary>
        /// Integration type identifier (e.g., "kroger", "walmart"). Null for manual stores.
        /// </summary>
        public string? IntegrationType { get; set; }

        /// <summary>
        /// External store location ID from the integration provider
        /// </summary>
        public string? ExternalLocationId { get; set; }

        /// <summary>
        /// Chain/brand identifier for multi-brand integrations (e.g., "kroger", "ralphs", "fred-meyer")
        /// </summary>
        public string? ExternalChainId { get; set; }

        /// <summary>
        /// OAuth access token (encrypted at rest)
        /// </summary>
        public string? OAuthAccessToken { get; set; }

        /// <summary>
        /// OAuth refresh token (encrypted at rest)
        /// </summary>
        public string? OAuthRefreshToken { get; set; }

        /// <summary>
        /// When the OAuth access token expires
        /// </summary>
        public DateTime? OAuthTokenExpiresAt { get; set; }

        /// <summary>
        /// Store street address
        /// </summary>
        public string? StoreAddress { get; set; }

        /// <summary>
        /// Store phone number
        /// </summary>
        public string? StorePhone { get; set; }

        /// <summary>
        /// Store latitude for mapping
        /// </summary>
        public double? Latitude { get; set; }

        /// <summary>
        /// Store longitude for mapping
        /// </summary>
        public double? Longitude { get; set; }

        /// <summary>
        /// Custom aisle ordering for this store. Aisles stored in walking order.
        /// When null/empty, default numeric-then-alphabetical order is used.
        /// </summary>
        public List<string>? AisleOrder { get; set; }

        // Navigation properties
        /// <summary>
        /// Products that have this as their default shopping location
        /// </summary>
        public virtual ICollection<Product>? Products { get; set; }

        /// <summary>
        /// Product-store metadata entries for this shopping location
        /// </summary>
        public virtual ICollection<ProductStoreMetadata>? ProductStoreMetadata { get; set; }
    }
}
