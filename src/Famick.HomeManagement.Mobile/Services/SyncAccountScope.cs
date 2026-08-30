using Famick.HomeManagement.Shared.Sync;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Reports which account the sync engines are currently acting for.
/// </summary>
/// <remarks>
/// <para>
/// Sync maps server record ids to device record ids and, on each run, deletes device
/// records whose server id is absent from the fetched set. That pass is only correct when
/// the mappings and the fetched set belong to the same account. When they don't, every id
/// looks deleted and the pass wipes the device copies.
/// </para>
/// <para>
/// The mapping files are therefore keyed by this scope. Signing out leaves them on disk
/// untouched — they are still an accurate record of what that account put on the device,
/// and signing back in resumes against them rather than re-creating everything.
/// </para>
/// </remarks>
public class SyncAccountScope
{
    private readonly TokenStorage _tokenStorage;

    public SyncAccountScope(TokenStorage tokenStorage)
    {
        _tokenStorage = tokenStorage;
    }

    /// <summary>
    /// The current account's scope key, or null when no account is signed in.
    /// Null means "we cannot attribute mappings to anyone" and must never be treated as
    /// a scope of its own — that is precisely the shared-bucket behaviour being fixed.
    /// </summary>
    public string? CurrentKey
    {
        get
        {
            var identity = _tokenStorage.GetAccountIdentityFromToken();
            if (identity == null) return null;

            return SyncScopeKey.Compute(identity.Value.TenantId, identity.Value.UserId);
        }
    }
}
