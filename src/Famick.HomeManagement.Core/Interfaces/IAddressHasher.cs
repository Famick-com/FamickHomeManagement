namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Origin of an address record. Decides whether the hasher runs the
/// configured <see cref="IAddressCanonicalizer"/> or skips it.
/// </summary>
public enum AddressProvenance
{
    /// <summary>
    /// Components were typed by a user or otherwise have unknown origin.
    /// Run the canonicalizer so format variations ("St" vs "Street",
    /// "N" vs "North") collapse to the same hash before dedup lookup.
    /// </summary>
    Unverified,

    /// <summary>
    /// Components came from a verified provider (Smarty / Geoapify
    /// resolution, autocomplete pick). They are already in canonical
    /// form; the canonicalizer is skipped and components are hashed
    /// directly. Within the verified pool, dedup runs primarily via
    /// <c>Address.ProviderPlaceId</c>; the hash is a fallback.
    /// </summary>
    Verified
}

/// <summary>
/// Single source of truth for the <c>Address.NormalizedHash</c> column.
/// Wraps the canonicalizer + SHA256 hashing into one call. Replaces the
/// duplicated <c>ComputeNormalizedHash</c> / <c>GenerateAddressHash</c>
/// static helpers that previously lived in <c>AddressService</c> and
/// <c>ContactService</c>.
/// </summary>
public interface IAddressHasher
{
    /// <summary>
    /// Computes the dedup hash for the given components. Returns null
    /// when every component is null/empty.
    /// </summary>
    Task<string?> ComputeAsync(
        AddressComponentsInput input,
        AddressProvenance provenance,
        CancellationToken ct = default);
}
