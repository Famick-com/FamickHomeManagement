namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a share token for publicly sharing a recipe via a unique link.
/// Tokens have an expiration date and can be revoked.
/// </summary>
public class RecipeShareToken : BaseTenantEntity
{
    /// <summary>
    /// The recipe this token grants access to
    /// </summary>
    public Guid RecipeId { get; set; }

    /// <summary>
    /// Unique token string (UUID-based) used in the share URL
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// When this share token expires (default 90 days from creation)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Whether this token has been manually revoked
    /// </summary>
    public bool IsRevoked { get; set; }

    // Navigation properties

    /// <summary>
    /// The recipe this token grants access to
    /// </summary>
    public virtual Recipe Recipe { get; set; } = null!;
}
