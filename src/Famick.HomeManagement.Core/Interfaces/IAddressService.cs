using Famick.HomeManagement.Core.DTOs.Common;

namespace Famick.HomeManagement.Core.Interfaces;

public interface IAddressService
{
    /// <summary>
    /// Searches the local Addresses table (tenant-visible entries) for partial
    /// matches on the given query. Does not call any external provider.
    /// </summary>
    Task<List<AddressDto>> SearchAsync(string query, int limit = 10, CancellationToken ct = default);

    /// <summary>
    /// Combined autocomplete: fans out to the local DB search and the external
    /// provider in parallel, merges results, and caches external suggestions
    /// so they can be later resolved via <see cref="ResolveSuggestionAsync"/>.
    /// External-provider failures degrade to local-only results.
    /// </summary>
    Task<List<AddressSuggestionDto>> AutocompleteAsync(string query, int limit = 10, CancellationToken ct = default);

    /// <summary>
    /// Resolves a suggestion returned from <see cref="AutocompleteAsync"/> into
    /// a persisted <see cref="AddressDto"/>. Local suggestions return the
    /// existing address; external suggestions are standardized, deduped, and
    /// saved. Returns null when the suggestion is unknown or has expired.
    /// </summary>
    Task<AddressDto?> ResolveSuggestionAsync(ResolveAddressSuggestionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Manual-entry path: standardizes the supplied address via the external
    /// provider (falling back to input-as-is when unavailable), dedupes against
    /// existing addresses, and persists a new one if needed. Returns the
    /// resulting <see cref="AddressDto"/>.
    /// </summary>
    Task<AddressDto> StandardizeAndCreateAsync(StandardizeAddressRequest request, CancellationToken ct = default);
}
