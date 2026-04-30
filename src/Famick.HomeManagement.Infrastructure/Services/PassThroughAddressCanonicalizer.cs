using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Default <see cref="IAddressCanonicalizer"/> when libpostal isn't
/// configured. Returns the input components unchanged (apart from
/// trimming) so the hash output matches the legacy
/// <c>ComputeNormalizedHash</c> behavior byte-for-byte. Provides the
/// "no extra dependency" deployment path for self-hosted users.
/// </summary>
public sealed class PassThroughAddressCanonicalizer : IAddressCanonicalizer
{
    public string ProviderName => "PassThrough";

    public Task<CanonicalAddressComponents> CanonicalizeAsync(
        AddressComponentsInput input,
        CancellationToken ct = default)
    {
        return Task.FromResult(new CanonicalAddressComponents(
            input.Line1?.Trim(),
            input.City?.Trim(),
            input.State?.Trim(),
            input.PostalCode?.Trim(),
            input.Country?.Trim()));
    }
}
