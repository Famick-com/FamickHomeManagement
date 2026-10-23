using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.DataPortability;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Messaging.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <inheritdoc />
/// <remarks>
/// <para>
/// Built inside the request rather than queued for the background worker. The archive is one
/// person's contacts and profile, not a household, and the removal waits on it: the member is
/// deleted straight afterwards, and their private contacts with them. A queued export would race
/// the delete it exists to precede.
/// </para>
/// <para>
/// Stored as a <see cref="HouseholdDataTransfer"/> of kind <see cref="HouseholdDataTransferKind.MemberExport"/>,
/// reusing the household archive's storage and expiry. Every household-facing read of that table
/// filters to <see cref="HouseholdDataTransferKind.Export"/>, so one of these never shows up as
/// "the latest export" for the rest of the household to download.
/// </para>
/// </remarks>
public sealed class MemberDataExportService(
    HomeManagementDbContext context,
    ITenantProvider tenantProvider,
    IFileStorageService storage,
    IFileAccessTokenService tokenService,
    IOptions<EmailSettings> emailSettings,
    ILogger<MemberDataExportService> logger,
    IMessageService? messages = null)
    : IMemberDataExportService
{
    /// <summary>How long the emailed link works. As for a household export.</summary>
    private static readonly TimeSpan ArchiveLifetime = TimeSpan.FromDays(7);

    public bool CanEmailMembers =>
        messages != null
        && emailSettings.Value.IsConfigured
        && ArchiveDownloads.CheckEmailedLink(storage.GetExportArchiveUrl(Guid.Empty, "token")) == EmailedLinkProblem.None;

    public async Task SendToMemberAsync(Guid memberUserId, Guid requestedByUserId, CancellationToken ct = default)
    {
        if (!CanEmailMembers || messages == null)
        {
            throw new BusinessRuleViolationException(
                "MemberExportEmailUnavailable",
                "This server cannot send email, so the member's data cannot be sent to them.");
        }

        var member = await FindMemberAsync(memberUserId, ct);
        if (string.IsNullOrWhiteSpace(member.Email))
        {
            throw new BusinessRuleViolationException(
                "MemberExportNoEmail", "This member has no email address to send their data to.");
        }

        var (transfer, summary, householdName) = await BuildAsync(member, requestedByUserId, ct);

        var token = tokenService.GenerateToken(
            MemberDataExport.TokenResourceType, transfer.Id, transfer.TenantId,
            expirationMinutes: (int)ArchiveLifetime.TotalMinutes);

        // Not caught: the Admin is about to delete the account, and must be told if the one copy
        // of these contacts did not go out, rather than learn it from the member afterwards.
        await messages.SendTransactionalAsync(member.Email, MessageType.MemberDataExportReady, new MemberDataExportReadyData
        {
            UserName = member.FirstName,
            HouseholdName = householdName,
            DownloadLink = storage.GetExportArchiveUrl(transfer.Id, token),
            ExpiresOn = transfer.ExpiresAt?.ToString("d MMMM yyyy") ?? string.Empty,
            ContactCount = summary.ContactCount,
        }, ct);

        logger.LogInformation(
            "Member export {TransferId} for user {UserId} sent to them, requested by {RequestedBy}: {Contacts} contacts",
            transfer.Id, member.Id, requestedByUserId, summary.ContactCount);
    }

    public async Task<string> CreateOwnExportLinkAsync(Guid userId, CancellationToken ct = default)
    {
        var member = await FindMemberAsync(userId, ct);
        var (transfer, summary, _) = await BuildAsync(member, userId, ct);

        logger.LogInformation("Member export {TransferId} built for user {UserId}: {Contacts} contacts",
            transfer.Id, userId, summary.ContactCount);

        // Minutes: it only has to cover the click that follows. The archive stays for the usual
        // lifetime, so asking again within it is cheap enough not to need a longer token.
        var token = tokenService.GenerateToken(
            MemberDataExport.TokenResourceType, transfer.Id, transfer.TenantId, expirationMinutes: 15);

        return storage.GetExportArchiveUrl(transfer.Id, token);
    }

    public async Task<ExportDownloadResult> OpenArchiveAsync(
        Guid transferId, long? rangeStart, long? rangeEnd, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();

        var transfer = await context.HouseholdDataTransfers
            .FirstOrDefaultAsync(t => t.Id == transferId
                                   && t.TenantId == tenantId
                                   && t.Kind == HouseholdDataTransferKind.MemberExport, ct);

        return await ArchiveDownloads.OpenAsync(storage, transfer, rangeStart, rangeEnd, ct);
    }

    private async Task<(HouseholdDataTransfer Transfer, MemberArchiveSummary Summary, string HouseholdName)> BuildAsync(
        User member, Guid requestedByUserId, CancellationToken ct)
    {
        var householdName = await context.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == member.TenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct) ?? "Household";

        var transferId = Guid.NewGuid();
        var fileName = BuildFileName(member);

        await using var buffer = new MemoryStream();
        var summary = await new MemberArchiveWriter(context, storage, logger)
            .WriteAsync(member, householdName, buffer, ct);

        var bytes = buffer.Length;
        buffer.Position = 0;
        await storage.SaveExportArchiveAsync(transferId, buffer, fileName, ct);

        // Recorded only once the archive is stored, and straight into Completed. It never passes
        // through Queued, so the background worker — which takes the oldest Queued row of any kind
        // and runs only household exports — never meets one.
        var transfer = new HouseholdDataTransfer
        {
            Id = transferId,
            TenantId = member.TenantId,
            Kind = HouseholdDataTransferKind.MemberExport,
            Status = HouseholdDataTransferStatus.Completed,
            RequestedByUserId = requestedByUserId,
            IncludeFiles = true,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(ArchiveLifetime),
            ArchiveFileName = fileName,
            ArchiveBytes = bytes,
        };

        context.HouseholdDataTransfers.Add(transfer);
        await context.SaveChangesAsync(ct);

        return (transfer, summary, householdName);
    }

    private async Task<User> FindMemberAsync(Guid userId, CancellationToken ct)
    {
        var tenantId = RequireTenant();

        return await context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct)
            ?? throw new EntityNotFoundException("User", userId);
    }

    private Guid RequireTenant() =>
        tenantProvider.TenantId
        ?? throw new InvalidOperationException(
            "No tenant in context. The member export reads across query filters, so it refuses " +
            "rather than guess which household it is in.");

    private static string BuildFileName(User member)
    {
        var name = $"{member.FirstName} {member.LastName}".Trim().ToLowerInvariant();
        var slug = new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        if (slug.Length > 40) slug = slug[..40].Trim('-');
        if (slug.Length == 0) slug = "member";

        return $"famick-my-data-{slug}-{DateTime.UtcNow:yyyyMMdd-HHmm}.zip";
    }
}
