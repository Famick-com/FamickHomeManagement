namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Whether a master product is published to every tenant or still awaiting review.
/// </summary>
public enum MasterProductReviewStatus
{
    /// <summary>Published: visible to every tenant.</summary>
    Approved = 0,

    /// <summary>Contributed by a tenant and awaiting platform review. Visible only to the contributing tenant.</summary>
    Pending = 1
}
