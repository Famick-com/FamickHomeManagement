using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.Users;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Helpers;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Messaging.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <inheritdoc />
/// <remarks>
/// An address that already has an account gets an invitation like anyone else: the admin sees
/// it pending and the API answers the same, so adding a member never reveals whether an email
/// is registered. Only the email differs, and only its owner reads it.
/// </remarks>
public class HouseholdInvitationService : IHouseholdInvitationService
{
    /// <summary>How long an invitation link works.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private const string InvalidLink = "This invitation link is invalid or has expired.";

    private readonly HomeManagementDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly IMessageService _messageService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly HouseholdMemberFactory _memberFactory;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<HouseholdInvitationService> _logger;

    public HouseholdInvitationService(
        HomeManagementDbContext context,
        ITenantProvider tenantProvider,
        IMessageService messageService,
        IPasswordHasher passwordHasher,
        HouseholdMemberFactory memberFactory,
        IOptions<EmailSettings> emailSettings,
        ILogger<HouseholdInvitationService> logger)
    {
        _context = context;
        _tenantProvider = tenantProvider;
        _messageService = messageService;
        _passwordHasher = passwordHasher;
        _memberFactory = memberFactory;
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool InvitationsEnabled => _emailSettings.IsConfigured;

    /// <inheritdoc />
    public async Task<HouseholdInvitationDto> InviteAsync(
        CreateUserRequest request, string baseUrl, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var email = EmailAddress.Normalize(request.Email);
        var now = DateTime.UtcNow;

        // Within the household the admin can already see who is a member or invited, so saying
        // so gives nothing away.
        var isMember = await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.TenantId == tenantId && u.Email == email, cancellationToken);
        if (isMember)
        {
            throw new DuplicateEntityException("User", "Email", email);
        }

        var isInvited = await _context.HouseholdInvitations
            .IgnoreQueryFilters()
            .AnyAsync(i => i.TenantId == tenantId && i.Email == email
                && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now, cancellationToken);
        if (isInvited)
        {
            throw new DuplicateEntityException("Invitation", "Email", email);
        }

        var token = NewToken();
        var invitation = new HouseholdInvitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Roles = request.Roles.Distinct().ToList(),
            ContactId = request.ContactId,
            InvitedByUserId = _tenantProvider.UserId ?? Guid.Empty,
            TokenHash = HashToken(token),
            ExpiresAt = now.Add(Lifetime),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.HouseholdInvitations.Add(invitation);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Invitation {InvitationId} created for household {TenantId}", invitation.Id, tenantId);

        await SendAsync(invitation, token, baseUrl, cancellationToken);
        return ToDto(invitation);
    }

    /// <inheritdoc />
    public async Task<List<HouseholdInvitationDto>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var now = DateTime.UtcNow;

        var invitations = await _context.HouseholdInvitations
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now)
            .OrderBy(i => i.Email)
            .ToListAsync(cancellationToken);

        return invitations.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<HouseholdInvitationDto> ResendAsync(
        Guid invitationId, string baseUrl, CancellationToken cancellationToken = default)
    {
        var invitation = await FindPendingInHouseholdAsync(invitationId, cancellationToken);

        // A new token, so a link that went astray stops working.
        var token = NewToken();
        var now = DateTime.UtcNow;
        invitation.TokenHash = HashToken(token);
        invitation.ExpiresAt = now.Add(Lifetime);
        invitation.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        await SendAsync(invitation, token, baseUrl, cancellationToken);
        return ToDto(invitation);
    }

    /// <inheritdoc />
    public async Task RevokeAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        var invitation = await FindPendingInHouseholdAsync(invitationId, cancellationToken);

        invitation.RevokedAt = DateTime.UtcNow;
        invitation.UpdatedAt = invitation.RevokedAt;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Invitation {InvitationId} revoked", invitation.Id);
    }

    /// <inheritdoc />
    public async Task<InvitationDetailsResponse> GetDetailsAsync(string token, CancellationToken cancellationToken = default)
    {
        var invitation = await FindPendingByTokenAsync(token, cancellationToken);
        if (invitation == null)
        {
            return new InvitationDetailsResponse { IsValid = false, ErrorMessage = InvalidLink };
        }

        var householdName = await HouseholdNameAsync(invitation.TenantId, cancellationToken);
        return new InvitationDetailsResponse
        {
            IsValid = true,
            Email = invitation.Email,
            FirstName = invitation.FirstName,
            HouseholdName = householdName,
            CanAccept = !await HasAccountAsync(invitation.Email, cancellationToken)
        };
    }

    /// <inheritdoc />
    public async Task<AcceptInvitationResponse> AcceptAsync(
        AcceptInvitationRequest request, CancellationToken cancellationToken = default)
    {
        var invitation = await FindPendingByTokenAsync(request.Token, cancellationToken);
        if (invitation == null)
        {
            return new AcceptInvitationResponse { Success = false, Message = InvalidLink };
        }

        if (await HasAccountAsync(invitation.Email, cancellationToken))
        {
            return new AcceptInvitationResponse
            {
                Success = false,
                Message = "This email address already has an account, and an account can't join a second household yet."
            };
        }

        User user;
        try
        {
            user = await _memberFactory.CreateAsync(
                invitation.TenantId,
                invitation.Email,
                invitation.FirstName,
                invitation.LastName,
                _passwordHasher.HashPassword(request.Password),
                mustChangePassword: false,
                invitation.Roles,
                invitation.ContactId,
                cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // The unique email index: the same link submitted twice at once, or the address
            // registered between the check above and here.
            _logger.LogWarning(ex, "Invitation {InvitationId} could not create its user", invitation.Id);
            return new AcceptInvitationResponse { Success = false, Message = InvalidLink };
        }

        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.AcceptedUserId = user.Id;
        invitation.UpdatedAt = invitation.AcceptedAt;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Invitation {InvitationId} accepted; user {UserId} created", invitation.Id, user.Id);

        return new AcceptInvitationResponse
        {
            Success = true,
            Message = "Your account is ready. You can now sign in."
        };
    }

    private async Task SendAsync(HouseholdInvitation invitation, string token, string baseUrl, CancellationToken cancellationToken)
    {
        var householdName = await HouseholdNameAsync(invitation.TenantId, cancellationToken);
        var inviter = await _context.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == invitation.InvitedByUserId)
            .Select(u => (u.FirstName + " " + u.LastName).Trim())
            .FirstOrDefaultAsync(cancellationToken);
        var invitedBy = string.IsNullOrWhiteSpace(inviter) ? "A member of your household" : inviter;
        var root = baseUrl.TrimEnd('/');

        try
        {
            if (await HasAccountAsync(invitation.Email, cancellationToken))
            {
                await _messageService.SendTransactionalAsync(
                    invitation.Email,
                    MessageType.HouseholdInvitationExistingAccount,
                    new HouseholdInvitationExistingAccountData
                    {
                        HouseholdName = householdName,
                        InvitedByName = invitedBy,
                        LoginLink = $"{root}/login"
                    },
                    cancellationToken);
            }
            else
            {
                await _messageService.SendTransactionalAsync(
                    invitation.Email,
                    MessageType.HouseholdInvitation,
                    new HouseholdInvitationData
                    {
                        UserName = invitation.FirstName,
                        HouseholdName = householdName,
                        InvitedByName = invitedBy,
                        AcceptLink = $"{root}/accept-invitation?token={Uri.EscapeDataString(token)}",
                        ExpiresInDays = (int)Lifetime.TotalDays
                    },
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // The invitation stands; the admin can resend it.
            _logger.LogError(ex, "Failed to send invitation {InvitationId}", invitation.Id);
        }
    }

    private async Task<HouseholdInvitation> FindPendingInHouseholdAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var now = DateTime.UtcNow;

        return await _context.HouseholdInvitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.TenantId == tenantId
                && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(HouseholdInvitation), invitationId);
    }

    private async Task<HouseholdInvitation?> FindPendingByTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = HashToken(token);
        var invitation = await _context.HouseholdInvitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        return invitation != null && invitation.IsPending(DateTime.UtcNow) ? invitation : null;
    }

    private Task<bool> HasAccountAsync(string email, CancellationToken cancellationToken) =>
        _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, cancellationToken);

    private async Task<string> HouseholdNameAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _context.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "a household";

    private Guid RequireTenantId() =>
        _tenantProvider.TenantId
        ?? throw new InvalidOperationException(
            "No tenant is resolved for this request, so an invitation cannot be made or managed.");

    private static HouseholdInvitationDto ToDto(HouseholdInvitation invitation) => new()
    {
        Id = invitation.Id,
        Email = invitation.Email,
        FirstName = invitation.FirstName,
        LastName = invitation.LastName,
        Roles = invitation.Roles.ToList(),
        ContactId = invitation.ContactId,
        CreatedAt = invitation.CreatedAt,
        ExpiresAt = invitation.ExpiresAt
    };

    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

    private static string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
