namespace Famick.HomeManagement.Domain.Interfaces;

/// <summary>
/// An item that can arrive as a copy of another household's, through a share link. The copy is the
/// household's own from then on; these only record where it came from.
/// </summary>
public interface ISharedCopy : ITenantEntity
{
    /// <summary>
    /// Identifies the item this was copied from, so adding the same item again finds this copy instead of
    /// making another. Derived from the source, never its ids; null for anything not shared in.
    /// </summary>
    string? OriginKey { get; set; }

    /// <summary>The name of the household that shared it, as it was when it was added.</summary>
    string? SharedByHouseholdName { get; set; }
}
