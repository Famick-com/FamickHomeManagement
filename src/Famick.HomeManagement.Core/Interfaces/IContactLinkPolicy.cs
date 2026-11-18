using Famick.HomeManagement.Core.Exceptions;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Says which contacts are copies whose details another household keeps up to date, and which
/// of this household's own people other households hold copies of.
/// </summary>
/// <remarks>
/// A hosting layer that links households registers its own implementation. The default answers
/// that nothing is linked, which is always true on a single-household server.
/// </remarks>
public interface IContactLinkPolicy
{
    /// <summary>
    /// Link information for those of <paramref name="contactIds"/> that have any, in the current
    /// household. A contact missing from the result is an ordinary contact.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ContactLinkInfo>> GetLinkInfoAsync(
        IReadOnlyCollection<Guid> contactIds, CancellationToken ct = default);
}

/// <summary>How a contact takes part in a link between households.</summary>
/// <param name="ManagedByHousehold">
/// The household that owns the details when this contact is a copy, or null when this household
/// owns them.
/// </param>
/// <param name="LinkedHouseholds">
/// The households holding a copy of this contact, when this household owns it.
/// </param>
public sealed record ContactLinkInfo(string? ManagedByHousehold, IReadOnlyList<string> LinkedHouseholds)
{
    /// <summary>Whether another household keeps this contact's details.</summary>
    public bool IsManagedElsewhere => ManagedByHousehold != null;
}

public static class ContactLinkPolicyExtensions
{
    /// <summary>
    /// Throws <see cref="ContactManagedElsewhereException"/> when another household keeps
    /// <paramref name="contactId"/>'s details.
    /// </summary>
    public static async Task EnsureOwnedHereAsync(
        this IContactLinkPolicy policy, Guid contactId, CancellationToken ct = default)
    {
        var links = await policy.GetLinkInfoAsync([contactId], ct);
        if (links.TryGetValue(contactId, out var link) && link.ManagedByHousehold is { } owner)
            throw new ContactManagedElsewhereException(owner);
    }
}
