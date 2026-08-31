using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Jobs;

/// <summary>
/// Permanently removes accounts and households whose grace period has run out.
/// </summary>
/// <remarks>
/// Deletion is only staged when it is requested; this is what finally carries it out. If
/// the job stops running, nothing is destroyed — data outlives its promised deletion date
/// rather than disappearing early, which is the safer way for it to fail but still a
/// commitment left unmet, so its absence is worth alerting on.
/// </remarks>
public class AccountPurgeJob : IJob
{
    private readonly IAccountDeletionService _deletionService;

    public AccountPurgeJob(IAccountDeletionService deletionService)
    {
        _deletionService = deletionService;
    }

    public async Task RunJob(ILogger logger, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Warn before destroying. Run first so a household about to be purged still gets
        // its final notice even if the purge itself then fails.
        var reminders = await _deletionService.SendDueRemindersAsync(now, ct);

        var summary = await _deletionService.PurgeDueAsync(now, ct);

        logger.LogInformation(
            "Account purge complete: {Reminders} reminder(s), {Households} household(s), {Users} user(s)",
            reminders, summary.HouseholdsPurged, summary.UsersPurged);
    }
}
