using System.Text.Json;

namespace Famick.HomeManagement.UI.Services;

/// <summary>What the server says about a contact's links with other households.</summary>
public sealed class ContactLinkInfo
{
    public Guid ContactId { get; set; }

    /// <summary>The household that keeps this contact up to date, when it is a copy of one of its people.</summary>
    public string? ManagedByHousehold { get; set; }

    /// <summary>The households that see this contact, when it is one of the household's own people.</summary>
    public List<string> LinkedHouseholds { get; set; } = [];
}

/// <summary>
/// Linked households, where the server has them: which contacts another household keeps up to date,
/// and the server's pages where links are managed.
/// </summary>
public static class LinkedHouseholds
{
    /// <summary>Asks about at most this many contacts at a time, as the server allows.</summary>
    public const int MaxContactsPerRequest = 500;

    public const string ContactsEndpoint = "api/cloud/household-links/contacts";

    /// <summary>The server's page of the household's links. Served by the server, so it is loaded rather than routed.</summary>
    public const string SettingsRoute = "/settings/linked-households";

    /// <summary>The server's page for setting up a household for a contact. Loaded rather than routed.</summary>
    public static string InvitationRoute(Guid contactId) => $"{SettingsRoute}/invite/{contactId}";

    /// <summary>
    /// The link info for those of <paramref name="contactIds"/> that have any, by contact. Empty when
    /// the server couldn't say: the pages then show the contacts as they would without links.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, ContactLinkInfo>> LoadAsync(
        IApiClient api, IEnumerable<Guid> contactIds)
    {
        var ids = contactIds.Distinct().Take(MaxContactsPerRequest).ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, ContactLinkInfo>();

        try
        {
            var query = string.Join("&", ids.Select(id => $"ids={id}"));
            var result = await api.GetAsync<List<ContactLinkInfo>>($"{ContactsEndpoint}?{query}");
            return result.IsSuccess && result.Data != null
                ? result.Data.ToDictionary(i => i.ContactId)
                : new Dictionary<Guid, ContactLinkInfo>();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            return new Dictionary<Guid, ContactLinkInfo>();
        }
    }
}
