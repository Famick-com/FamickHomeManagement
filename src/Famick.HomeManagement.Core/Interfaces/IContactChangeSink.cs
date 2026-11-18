namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Hears about every committed change to a contact or to the details stored under it: phones,
/// emails, addresses, social profiles, the photo, allergens and dietary preferences, and which
/// group it belongs to.
/// </summary>
/// <remarks>
/// <para>
/// Called after the change has committed, never inside the transaction that made it, so an
/// implementation cannot roll the change back and must not assume it can. A failure is logged
/// and swallowed: the change has already happened.
/// </para>
/// <para>
/// Calls are per save, not per field. A contact changed twice in one save is reported once, and
/// an implementation should treat a call as "look at these contacts again" rather than as a
/// description of what changed. A deleted contact is reported like any other.
/// </para>
/// <para>
/// Called from inside the save or commit that made the change, so an implementation that writes to the
/// database must use a context of its own rather than the one passed around the request.
/// </para>
/// <para>
/// The default does nothing. A hosting layer that keeps copies of contacts elsewhere registers
/// its own.
/// </para>
/// </remarks>
public interface IContactChangeSink
{
    Task ContactsChangedAsync(Guid tenantId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct = default);
}
