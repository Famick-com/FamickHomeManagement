namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Short-lived cache for external-provider autocomplete suggestions. Entries
/// are keyed by a server-issued GUID that the client round-trips when it
/// selects a suggestion.
/// </summary>
public interface IAddressSuggestionCache
{
    /// <summary>
    /// Stores the suggestion and returns the cache key (GUID). The entry
    /// expires after <see cref="DefaultTtl"/>.
    /// </summary>
    Guid Store(ExternalAddressSuggestion suggestion);

    /// <summary>
    /// Retrieves a previously cached suggestion. Returns null if the key is
    /// unknown or the entry has expired.
    /// </summary>
    ExternalAddressSuggestion? TryGet(Guid suggestionId);

    static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);
}
