namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Raw address components passed into the canonicalizer for dedup hashing.
/// </summary>
public sealed record AddressComponentsInput(
    string? Line1,
    string? City,
    string? State,
    string? PostalCode,
    string? Country);

/// <summary>
/// Canonicalizer output. Same shape as the input — the values are just
/// canonicalized in-place (e.g. "St" → "Street", "N" → "North").
/// </summary>
public sealed record CanonicalAddressComponents(
    string? Line1,
    string? City,
    string? State,
    string? PostalCode,
    string? Country);

/// <summary>
/// Produces a stable canonical form of address components for dedup
/// hashing. Distinct from <see cref="IAddressNormalizationService"/>,
/// which is a paid geocoding+verification API: this runs on every write
/// and must be free, fast, and never throw on transport failure.
/// Implementations include a pass-through (no external dependency) and a
/// libpostal-rest sidecar.
/// </summary>
public interface IAddressCanonicalizer
{
    /// <summary>Provider name for diagnostics — "Libpostal" or "PassThrough".</summary>
    string ProviderName { get; }

    /// <summary>
    /// Returns canonical components. Implementations must be deterministic
    /// (same input always yields the same output) and must never throw on
    /// transport failure — fall back to input components on error so writes
    /// never break.
    /// </summary>
    Task<CanonicalAddressComponents> CanonicalizeAsync(
        AddressComponentsInput input,
        CancellationToken ct = default);
}
