using System.Security.Cryptography;
using System.Text;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Counts failed password attempts per account and locks the account out for a while once
/// they pile up.
/// <para>
/// Keys are opaque strings from <see cref="LoginThrottleKeys"/>. A password key is derived from
/// the email as typed, whether or not an account exists for it, so a locked-out address and an
/// unknown one answer identically and the lockout cannot be used to discover accounts.
/// </para>
/// </summary>
public interface ILoginThrottle
{
    /// <summary>How long the key stays locked out, or null when it may try now.</summary>
    Task<TimeSpan?> GetLockoutAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Records a failed attempt and returns the lockout it triggers, if any.</summary>
    Task<TimeSpan?> RecordFailureAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Clears the failure count and any lockout after a successful attempt.</summary>
    Task RecordSuccessAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>Builds <see cref="ILoginThrottle"/> keys. Emails are hashed so no store holds them.</summary>
public static class LoginThrottleKeys
{
    public static string Password(string email) =>
        "pw:" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes((email ?? string.Empty).Trim().ToLowerInvariant())));

    public static string Reauth(Guid userId) => $"reauth:{userId:N}";
}

/// <summary>
/// The lockout schedule every <see cref="ILoginThrottle"/> applies.
/// <para>
/// Five failures are free. The sixth locks for a minute, and each further failure doubles the
/// lock up to fifteen minutes. Failures are forgotten fifteen minutes after the last one. That
/// holds a guesser to a handful of tries an hour per account while capping how long anyone can
/// keep a real user out by failing on purpose; passkeys, social sign-in and password reset all
/// still work during a lock.
/// </para>
/// </summary>
public static class LoginThrottlePolicy
{
    public const int FreeAttempts = 5;

    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan BaseLockout = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan MaxLockout = TimeSpan.FromMinutes(15);

    /// <summary>The lockout earned by the <paramref name="failures"/>th consecutive failure.</summary>
    public static TimeSpan? LockoutAfter(long failures)
    {
        if (failures <= FreeAttempts)
        {
            return null;
        }

        var doublings = Math.Min(failures - FreeAttempts - 1, 10);
        var lockout = TimeSpan.FromTicks(BaseLockout.Ticks << (int)doublings);
        return lockout < MaxLockout ? lockout : MaxLockout;
    }
}
