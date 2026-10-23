using System.Linq.Expressions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// What becomes of a member's rows when they leave a household that carries on without them.
/// </summary>
/// <remarks>
/// <para>
/// Shared by both ways a member goes: an Admin removing them, and their own account deletion
/// coming due. Without it neither works for anyone who has used the app. Contacts and calendar
/// events name their creator through a required <c>Restrict</c> reference, so the database
/// refuses to delete a user who has made either — and every member has made at least one.
/// </para>
/// <para>
/// The rule follows who could see the row. A contact the member kept to themselves, or shared
/// only with named people, goes with them: nobody else in the household was meant to have it,
/// and #99's export is how it leaves with the person. Anything the household could see stays,
/// and is handed to <c>successorUserId</c> as its creator, because creator is also who may
/// edit and delete it. Stock and contact history keep the row and drop the name instead
/// (those references are <c>SET NULL</c>): attributing a past change to someone who did not
/// make it would be worse than naming nobody.
/// </para>
/// <para>
/// Everything here is staged on the context and saved by the caller in the same
/// <c>SaveChanges</c> as the user's own removal, so the departure is all-or-nothing.
/// </para>
/// </remarks>
internal static class HouseholdMemberDeparture
{
    /// <summary>
    /// The contacts that leave with <paramref name="userId"/>: ones they created and did not share
    /// with the household. Also what their export carries, so the two cannot disagree about what
    /// is lost.
    /// </summary>
    public static Expression<Func<Contact, bool>> LeavesWith(Guid userId) =>
        c => c.CreatedByUserId == userId
          && c.Visibility != ContactVisibilityLevel.TenantShared
          && !c.IsTenantHousehold;

    /// <summary>
    /// Stages the departure of <paramref name="member"/>. Returns the stored files that go with
    /// them, for <see cref="RemoveFilesAsync"/> once the save has committed.
    /// </summary>
    public static async Task<WithdrawnFiles> StageAsync(
        HomeManagementDbContext context, User member, Guid successorUserId, CancellationToken ct)
    {
        var tenantId = member.TenantId;
        var userId = member.Id;

        // IgnoreQueryFilters with an explicit tenant: the purge job runs with no tenant in
        // context, and the filter would then match every household's contacts.
        var created = await context.Contacts
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.CreatedByUserId == userId)
            .ToListAsync(ct);

        var withdrawn = created.Where(LeavesWith(userId).Compile()).ToList();
        var withdrawnIds = withdrawn.Select(c => c.Id).ToHashSet();

        if (withdrawnIds.Count > 0)
        {
            // A private group can hold contacts that are not private. Those move to the
            // household group, as they do when a group is deleted by hand. Members that are
            // themselves going are detached instead: ParentContactId is Restrict, and checked
            // per row, so a group cannot be deleted in the same statement as its members.
            var household = await context.Contacts
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId && c.IsTenantHousehold)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct);

            var members = await context.Contacts
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId
                         && c.ParentContactId != null
                         && withdrawnIds.Contains(c.ParentContactId.Value))
                .ToListAsync(ct);

            foreach (var contact in members)
                contact.ParentContactId = withdrawnIds.Contains(contact.Id) ? null : household;

            context.Contacts.RemoveRange(withdrawn);
        }

        foreach (var contact in created.Where(c => !withdrawnIds.Contains(c.Id)))
            contact.CreatedByUserId = successorUserId;

        var events = await context.CalendarEvents
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && e.CreatedByUserId == userId)
            .ToListAsync(ct);

        foreach (var calendarEvent in events)
            calendarEvent.CreatedByUserId = successorUserId;

        // Not a modelled relationship, so nothing in the database clears it. The contact card
        // stays — the person is still someone the household knows — but stops claiming an account.
        var linked = await context.Contacts
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.LinkedUserId == userId && !withdrawnIds.Contains(c.Id))
            .ToListAsync(ct);

        foreach (var contact in linked)
            contact.LinkedUserId = null;

        // Exports the member asked for go with them: the transfer row cascades from its
        // requester. That covers their own copies and any household export they started. The
        // archive is not part of the row, so its name is read now, while the row still exists.
        //
        // A copy an Admin emailed them on removal is untouched — its requester is the Admin —
        // which is what keeps that link working after the account is gone.
        var archives = await context.HouseholdDataTransfers
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && t.RequestedByUserId == userId && t.ArchiveFileName != null)
            .Select(t => new WithdrawnArchive(t.Id, t.ArchiveFileName!))
            .ToListAsync(ct);

        var images = withdrawn
            .Where(c => !string.IsNullOrEmpty(c.ProfileImageFileName))
            .Select(c => new WithdrawnImage(c.Id, c.ProfileImageFileName!))
            .ToList();

        return new WithdrawnFiles(images, archives);
    }

    /// <summary>
    /// Deletes the stored files whose rows went with the member: their withdrawn contacts'
    /// profile images, and the archives of exports they requested.
    /// </summary>
    /// <remarks>
    /// After the commit, never before: a rollback would otherwise leave rows pointing at files
    /// that are gone. Best effort, because the rows are already deleted and the departure cannot
    /// be undone over a file; a failure is logged with what is left behind. Nothing else would
    /// find these files later — expiry is checked only when an archive is downloaded.
    /// </remarks>
    public static async Task RemoveFilesAsync(
        WithdrawnFiles files, IFileStorageService? storage, ILogger logger, CancellationToken ct)
    {
        if (storage == null) return;

        foreach (var archive in files.Archives)
        {
            try
            {
                await storage.DeleteExportArchiveAsync(archive.TransferId, archive.FileName, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Could not remove the archive of export {TransferId}; the file may need clearing by hand",
                    archive.TransferId);
            }
        }

        foreach (var image in files.Images)
        {
            try
            {
                await storage.DeleteContactProfileImageAsync(image.ContactId, image.FileName, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Could not remove the profile image of deleted contact {ContactId}; the file may need clearing by hand",
                    image.ContactId);
            }
        }
    }

    /// <summary>Stored files whose rows are deleted with a departing member.</summary>
    public sealed record WithdrawnFiles(IReadOnlyList<WithdrawnImage> Images, IReadOnlyList<WithdrawnArchive> Archives);

    /// <summary>A stored profile image whose contact has been deleted.</summary>
    public sealed record WithdrawnImage(Guid ContactId, string FileName);

    /// <summary>A stored export archive whose transfer is deleted with the member who requested it.</summary>
    public sealed record WithdrawnArchive(Guid TransferId, string FileName);
}
