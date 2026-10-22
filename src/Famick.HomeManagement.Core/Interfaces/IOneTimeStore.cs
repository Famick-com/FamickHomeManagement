namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Short-lived values that may be redeemed exactly once, such as an OAuth state or a sign-in
/// nonce.
/// <para>
/// <see cref="TakeAsync"/> reads and removes in one step: when several callers take the same key
/// at once, at most one of them gets the value. A host that runs several instances registers a
/// shared store (the cloud registers a Redis one) so a value issued on one instance can be
/// redeemed on another.
/// </para>
/// </summary>
public interface IOneTimeStore
{
    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/> until it is taken or expires.</summary>
    Task SetAsync(string key, string value, TimeSpan lifetime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the value and returns it, or returns null when it is unknown, expired or already taken.
    /// </summary>
    Task<string?> TakeAsync(string key, CancellationToken cancellationToken = default);
}
