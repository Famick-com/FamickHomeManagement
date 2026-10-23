namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// A copy of one member's own data, for them to take when they leave a household.
/// </summary>
/// <remarks>
/// <para>
/// "Own" means what the household export cannot carry: contacts the member kept private or
/// shared only with named people, which go with the account when it is deleted, and their
/// profile. Everything the household could see stays with the household and is in its export.
/// </para>
/// <para>
/// Only the member ever receives it. When an Admin removes them it is emailed to the member, not
/// handed to the Admin, who was never allowed to see those contacts.
/// </para>
/// </remarks>
public interface IMemberDataExportService
{
    /// <summary>
    /// Whether this server can email a removed member their data: mail is configured, and a
    /// download link would work from an email client and not travel in cleartext.
    /// </summary>
    bool CanEmailMembers { get; }

    /// <summary>
    /// Builds <paramref name="memberUserId"/>'s export and emails them a link to it.
    /// </summary>
    /// <remarks>
    /// Run before the member is deleted, since afterwards their private contacts are gone. The
    /// archive outlives the account: the link works without signing in.
    /// </remarks>
    /// <exception cref="Exceptions.BusinessRuleViolationException">The server cannot email it.</exception>
    /// <exception cref="Exceptions.EntityNotFoundException">No such member in this household.</exception>
    Task SendToMemberAsync(Guid memberUserId, Guid requestedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Builds <paramref name="userId"/>'s own export and returns a short-lived link to download it.
    /// </summary>
    Task<string> CreateOwnExportLinkAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Opens a member export for download. Reached only through a signed link: there is no way
    /// to ask for one by id while signed in, because nobody signed in is entitled to another's.
    /// </summary>
    Task<ExportDownloadResult> OpenArchiveAsync(
        Guid transferId, long? rangeStart, long? rangeEnd, CancellationToken ct = default);
}

/// <summary>Names shared by the member export and the endpoint that serves it.</summary>
public static class MemberDataExport
{
    /// <summary>
    /// The resource type in a member export's access token. Distinct from a household archive's,
    /// so a token for one can never open the other.
    /// </summary>
    public const string TokenResourceType = "member-export";
}
