using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Single source of truth for <c>Address.NormalizedHash</c>. Replaces
/// the duplicated static <c>ComputeNormalizedHash</c> /
/// <c>GenerateAddressHash</c> helpers that previously lived in
/// <c>AddressService</c> and <c>ContactService</c>.
/// </summary>
public sealed class AddressHasher : IAddressHasher
{
    private readonly IAddressCanonicalizer _canonicalizer;

    public AddressHasher(IAddressCanonicalizer canonicalizer)
    {
        _canonicalizer = canonicalizer;
    }

    public async Task<string?> ComputeAsync(
        AddressComponentsInput input,
        AddressProvenance provenance,
        CancellationToken ct = default)
    {
        // Verified inputs (Smarty / Geoapify resolved) are already
        // canonical; running them through libpostal would be wasted work
        // and could introduce hash divergence with the verified pool's
        // own canonical form. Within the verified pool, dedup runs via
        // ProviderPlaceId / smarty_key — the hash is a fallback.
        CanonicalAddressComponents components = provenance == AddressProvenance.Verified
            ? new CanonicalAddressComponents(
                input.Line1?.Trim(),
                input.City?.Trim(),
                input.State?.Trim(),
                input.PostalCode?.Trim(),
                input.Country?.Trim())
            : await _canonicalizer.CanonicalizeAsync(input, ct);

        return Hash(components);
    }

    private static string? Hash(CanonicalAddressComponents c)
    {
        var parts = new[]
        {
            c.Line1?.Trim().ToLowerInvariant(),
            c.City?.Trim().ToLowerInvariant(),
            c.State?.Trim().ToLowerInvariant(),
            c.PostalCode?.Trim().ToLowerInvariant(),
            c.Country?.Trim().ToLowerInvariant()
        };
        var combined = string.Join("|", parts.Where(p => !string.IsNullOrEmpty(p)));
        if (string.IsNullOrEmpty(combined)) return null;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
