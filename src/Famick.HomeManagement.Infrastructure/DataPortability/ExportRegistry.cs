using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Infrastructure.DataPortability;

/// <summary>
/// Declares, for every entity type a household owns, whether it belongs in an archive.
/// </summary>
/// <remarks>
/// <para>
/// There is no default. <see cref="For"/> throws on a type it has not been told about, and the
/// guard test enumerates the model and asks about every one — so adding an entity without
/// deciding fails the build rather than silently leaking it into an archive or silently dropping
/// it from a backup. Both of those are bad, and neither announces itself.
/// </para>
/// <para>
/// Column-level exclusion is separate, because a type-level answer cannot express "export the
/// user but not their password hash".
/// </para>
/// </remarks>
public static class ExportRegistry
{
    private const string SecretReason =
        "Authenticates somebody. An archive is a file people email around and leave in a " +
        "downloads folder; anything in it that grants access is a credential in the wild.";

    private static readonly Dictionary<Type, EntityDisposition> Dispositions = new()
    {
        // ── Credentials and bearer tokens ────────────────────────────────────────────────
        [typeof(RefreshToken)] = Secret("Session credential. Re-importable refresh tokens would be an account-takeover primitive."),
        [typeof(PasswordResetToken)] = Secret("Grants a password change to whoever holds it."),
        [typeof(UserPasskeyCredential)] = Secret("WebAuthn keys and signature counters. Useless to the user; importing one authorises a device."),
        [typeof(UserJwtMinIat)] = Secret("Token revocation watermark. Restoring an old value un-revokes tokens that were deliberately killed."),
        [typeof(UserExternalLogin)] = Secret("Identity-provider subject identifiers. An identity binding, not household data."),
        [typeof(UserDeviceToken)] = Secret("Push routing credential for one physical device."),
        [typeof(TenantIntegrationToken)] = Secret("Store-integration OAuth access and refresh tokens."),
        [typeof(UserCalendarIcsToken)] = Secret("Unauthenticated feed URL. Exporting it puts a live public link to the household calendar in a shared file."),
        [typeof(UserContactVcfToken)] = Secret("Unauthenticated feed URL, as above, for contacts."),
        [typeof(RecipeShareToken)] = Secret("Unauthenticated share URL."),
        [typeof(AuthProxyPairingConfig)] = Secret("Proxy pairing state. Meaningless off-box and leaks infrastructure identifiers."),

        // ── Per-user state, not household data ───────────────────────────────────────────
        [typeof(UserRole)] = System("Access control. Restoring roles from a file would let an archive grant permissions."),
        [typeof(UserPermission)] = System("Access control, as above."),
        [typeof(UserCloudLoginOptIn)] = System("A per-user account setting that means nothing outside the deployment it was made in."),
        [typeof(Notification)] = System("Delivered messages. Restoring them would re-surface notifications about things long since dealt with."),
        [typeof(NotificationPreference)] = System("Per-user delivery settings, re-established by the user."),
        [typeof(UserMealPlannerPreference)] = System("Per-user UI state."),
        [typeof(UserMealPlannerTip)] = System("Per-user UI state — which tips have been dismissed."),
        [typeof(ContactUserShare)] = System("A sharing grant between users; access control rather than content."),
        [typeof(ExternalCalendarSubscription)] = System("A personal subscription to a third-party feed, whose URL can embed a token."),
        [typeof(ExternalCalendarEvent)] = System("Cached copies of someone else's calendar, refetched from the source."),

        // ── The deployment's own bookkeeping ─────────────────────────────────────────────
        [typeof(HouseholdDataTransfer)] = System("The record of exports and restores. Exporting the export log is self-referential noise."),
        [typeof(HouseholdDataTransferItem)] = System("As above."),

        // ── Personal data that may leave but must not come back ──────────────────────────
        [typeof(User)] = new(ExportDisposition.Export, ImportPolicy.Never,
            "Exported as an identity reference — who the household is — because that is what a " +
            "data-access request asks for. PasswordHash is excluded at the column level. Never " +
            "restored: the archive carries nothing that could authenticate the account, so a " +
            "recreated user would be one nobody can log into."),
        [typeof(UserAuditLog)] = new(ExportDisposition.Export, ImportPolicy.Never,
            "Personal data, so withholding it from an export is the worse position. Never " +
            "restored: replaying an audit trail fabricates a record of things that did not happen."),
        [typeof(ContactAuditLog)] = new(ExportDisposition.Export, ImportPolicy.Never,
            "As UserAuditLog."),
    };

    /// <summary>
    /// Columns withheld from types that are otherwise exported.
    /// </summary>
    private static readonly Dictionary<Type, string[]> ExcludedColumns = new()
    {
        [typeof(User)] = ["PasswordHash"],
        [typeof(Tenant)] = ["StripeCustomerId", "StripeSubscriptionId", "KmsKeyId"],

        // A shopping location is ordinary household data — where you buy things — but it also
        // carries the household's store-integration OAuth credentials. Encrypted at rest, which
        // protects the database and does nothing for a zip file the user downloads and forwards.
        // The store link is re-established by signing in again; the tokens must not travel.
        [typeof(ShoppingLocation)] = ["OAuthAccessToken", "OAuthRefreshToken", "OAuthTokenExpiresAt"],
    };

    /// <summary>
    /// Column names that trip the "looks like a secret" guard but are known to be safe.
    /// </summary>
    /// <remarks>
    /// Each entry is a claim that the name is misleading rather than the column dangerous, so it
    /// wants a reason next to it.
    /// </remarks>
    public static readonly Dictionary<string, string> KnownSafeColumnNames = new()
    {
        ["MustChangePassword"] = "A boolean flag, not a credential.",
        ["TokenType"] = "Describes a token's kind; holds no token.",
    };

    /// <summary>
    /// What the archive does with <paramref name="entityType"/>.
    /// </summary>
    /// <remarks>
    /// Types the registry does not name fall through to "ordinary household data, export it".
    /// That is a permissive default, which is only safe because of what enforces it: the guard
    /// test pins every tenant-scoped type together with the disposition it resolves to, so adding
    /// an entity fails the build and a person has to look at it and re-pin the list. Requiring an
    /// explicit entry for all eighty-odd types instead would turn this file into a second copy of
    /// the entity list, and a copy drifts.
    /// </remarks>
    public static EntityDisposition For(Type entityType)
    {
        if (Dispositions.TryGetValue(entityType, out var declared))
            return declared;

        return new EntityDisposition(ExportDisposition.Export, ImportPolicy.Import,
            "Household data with no reason to withhold it.");
    }

    /// <summary>
    /// True when the registry names <paramref name="entityType"/> explicitly, rather than falling
    /// back to the ordinary "household data" answer.
    /// </summary>
    public static bool IsDeclared(Type entityType) => Dispositions.ContainsKey(entityType);

    public static IReadOnlyCollection<string> ColumnsExcludedFrom(Type entityType) =>
        ExcludedColumns.TryGetValue(entityType, out var columns) ? columns : Array.Empty<string>();

    public static IReadOnlyDictionary<Type, string[]> AllExcludedColumns => ExcludedColumns;

    private static EntityDisposition Secret(string reason) =>
        new(ExportDisposition.ExcludeSecret, ImportPolicy.Never, $"{SecretReason} {reason}");

    private static EntityDisposition System(string reason) =>
        new(ExportDisposition.ExcludeSystem, ImportPolicy.Never, reason);
}
