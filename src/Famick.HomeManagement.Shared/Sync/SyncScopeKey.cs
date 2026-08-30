using System.Security.Cryptography;
using System.Text;

namespace Famick.HomeManagement.Shared.Sync;

/// <summary>
/// Derives a stable key identifying one account, used to scope the on-device sync
/// mapping files that pair server records with device records.
/// </summary>
/// <remarks>
/// <para>
/// The key is built from the tenant and user ids carried in the JWT rather than from
/// the server URL. The same account can be reached at more than one URL — directly on
/// the LAN, or through AuthProxy — and keying on the URL would treat those as separate
/// accounts, splitting the mapping file and re-creating every device record.
/// </para>
/// <para>
/// The ids are hashed rather than used verbatim so account identifiers do not appear in
/// filenames. Eight bytes is ample: the input is a pair of GUIDs and a device holds a
/// handful of accounts, not a population where birthday collisions matter.
/// </para>
/// </remarks>
public static class SyncScopeKey
{
    /// <summary>
    /// Computes the scope key for an account. Returns null when either id is missing,
    /// which callers must treat as "no account" rather than as a usable scope — see the
    /// remarks on <see cref="Compute"/>'s callers about why an unscoped mapping file is
    /// what allowed one account's sync to delete another's device data.
    /// </summary>
    public static string? Compute(string? tenantId, string? userId)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
            return null;

        var raw = $"{tenantId.Trim().ToLowerInvariant()}|{userId.Trim().ToLowerInvariant()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }
}
