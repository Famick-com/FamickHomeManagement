namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Work that has to happen alongside a household purge but lives outside the database.
/// </summary>
/// <remarks>
/// <para>
/// Deleting a household's rows is only part of destroying it. A deployment may also hold
/// files in object storage, an encryption key, or a running subscription that keeps
/// charging a customer who no longer exists. None of that belongs in the shared code —
/// a self-hosted server has none of it — so the purge announces itself and whatever is
/// registered does its own cleanup.
/// </para>
/// <para>
/// The two phases exist because the information needed for the cleanup is stored in the
/// rows being deleted. <see cref="PrepareAsync"/> runs while the household can still be
/// read; <see cref="CompleteAsync"/> runs once its removal has committed. A participant
/// holds whatever it captured between the two.
/// </para>
/// </remarks>
public interface IHouseholdPurgeParticipant
{
    /// <summary>
    /// Called before any of the household's rows are deleted. Read and keep whatever the
    /// cleanup will need — after this the household's data is gone.
    /// </summary>
    /// <remarks>
    /// Do not perform destructive work here. The deletion may still fail and roll back,
    /// and a cancelled subscription or an emptied bucket cannot be rolled back with it.
    /// </remarks>
    Task PrepareAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Called after the household's removal has committed. Perform the external cleanup.
    /// </summary>
    /// <remarks>
    /// Throwing does not resurrect the household — it is already gone. Participants should
    /// log enough to identify what was left behind so it can be cleaned up by hand.
    /// </remarks>
    Task CompleteAsync(Guid tenantId, CancellationToken ct = default);
}
