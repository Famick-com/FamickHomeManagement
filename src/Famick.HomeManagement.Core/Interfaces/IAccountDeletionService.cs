using Famick.HomeManagement.Core.DTOs.Account;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Account and household deletion, as required by App Store Review Guideline 5.1.1(v):
/// an app that lets people create an account must let them delete it from inside the app.
/// </summary>
/// <remarks>
/// <para>
/// Deletion is staged rather than immediate. A request records an intent and a date;
/// the data survives until that date, and authenticating again before it cancels the
/// whole thing. This is what makes a mistaken tap recoverable, and Apple accepts it so
/// long as the delay is stated plainly to the user.
/// </para>
/// <para>
/// What a request destroys depends on the requester's role. An admin owns the household,
/// so their deletion takes it and everyone in it. A member only removes themselves —
/// their leaving must never destroy data other people contributed.
/// </para>
/// </remarks>
public interface IAccountDeletionService
{
    /// <summary>
    /// Reports whether a deletion is scheduled, and what a request from this user would
    /// destroy. Clients call this before showing a confirmation prompt so the warning can
    /// name the household and the number of people affected.
    /// </summary>
    Task<AccountDeletionStatusDto> GetStatusAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Schedules deletion, and immediately ends every session it affects — refresh tokens
    /// are revoked and outstanding access tokens invalidated. That is what lets a later
    /// authenticated request be read as a deliberate return rather than a stale tab.
    /// </summary>
    Task<AccountDeletionRequestResultDto> RequestAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Cancels a pending deletion. Returns false when there was nothing to cancel.
    /// </summary>
    /// <remarks>
    /// Called on the user's behalf when they authenticate again, and also reachable
    /// directly so a client can offer an explicit "keep my account" control.
    /// </remarks>
    Task<bool> CancelAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Permanently removes everything whose grace period has elapsed as of
    /// <paramref name="asOfUtc"/>, and returns how many accounts and households went.
    /// Intended for the scheduled job; safe to run repeatedly.
    /// </summary>
    Task<AccountPurgeSummary> PurgeDueAsync(DateTime asOfUtc, CancellationToken ct = default);

    /// <summary>
    /// Runs on every authenticated request: cancels a pending deletion when the caller
    /// has genuinely returned, and reports whether they may proceed.
    /// </summary>
    /// <param name="tokenIssuedAtUnixSeconds">
    /// The access token's <c>iat</c>. A token minted before the request was made cannot
    /// evidence a return — requesting deletion invalidates outstanding tokens, so this is
    /// belt and braces against a revocation that did not take.
    /// </param>
    /// <remarks>
    /// Written as one round trip because it sits on the hot path. The common answer —
    /// nothing pending — costs a single indexed lookup, matching what
    /// <c>JwtMinIatMiddleware</c> already spends per request.
    /// </remarks>
    Task<AccountAccessDecision> ReconcileAuthenticatedRequestAsync(
        Guid userId, long tokenIssuedAtUnixSeconds, CancellationToken ct = default);
}

/// <summary>
/// What a purge run removed.
/// </summary>
public record AccountPurgeSummary(int UsersPurged, int HouseholdsPurged);

/// <summary>
/// Whether an authenticated caller may proceed.
/// </summary>
public enum AccountAccessDecision
{
    /// <summary>
    /// Nothing pending, or the caller's return just cancelled it.
    /// </summary>
    Allow = 0,

    /// <summary>
    /// The caller's household is scheduled for deletion and they are not an admin, so
    /// they cannot call it off. Refuse rather than let them keep filing data into a
    /// household that is about to disappear.
    /// </summary>
    HouseholdDeletionPending = 1
}
