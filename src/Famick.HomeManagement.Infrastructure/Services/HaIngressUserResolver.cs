using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <inheritdoc />
public class HaIngressUserResolver : IHaIngressUserResolver
{
    private const string ProviderName = "ha-ingress";
    private const string SyntheticEmailDomain = "ha-ingress.local";

    private readonly HomeManagementDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HaIngressUserResolver> _logger;

    public HaIngressUserResolver(
        HomeManagementDbContext context,
        IConfiguration configuration,
        ILogger<HaIngressUserResolver> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<User> ResolveAsync(HaIngressIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (string.IsNullOrWhiteSpace(identity.HaUserId))
        {
            throw new ArgumentException("HaUserId is required", nameof(identity));
        }

        var existingLink = await _context.UserExternalLogins
            .Include(uel => uel.User)
            .FirstOrDefaultAsync(
                uel => uel.Provider == ProviderName && uel.ProviderUserId == identity.HaUserId,
                cancellationToken);

        if (existingLink != null)
        {
            existingLink.LastUsedAt = DateTime.UtcNow;
            // Refresh display name in case the HA user renamed themselves.
            if (!string.IsNullOrWhiteSpace(identity.DisplayName))
            {
                existingLink.ProviderDisplayName = identity.DisplayName;
            }
            await _context.SaveChangesAsync(cancellationToken);
            return existingLink.User;
        }

        var tenantId = ResolveTenantId();

        // Account-linking heuristic: HA's X-Remote-User-Name is typically a
        // short handle ("alice"), while Famick stores Username == Email
        // ("alice@example.com") across every registration path. Match HA's
        // handle against the email local part and only link when exactly one
        // candidate exists — multiple matches (alice@gmail.com AND
        // alice@yahoo.com) are ambiguous and fall through to new-user
        // creation. Stricter explicit pairing (admin invites pending HA
        // users) is tracked as [[ha-ingress-pairing-ui]].
        if (!string.IsNullOrWhiteSpace(identity.Username))
        {
            var localPartPrefix = identity.Username.Trim().ToLower() + "@";
            var candidates = await _context.Users
                .Where(u => u.Email.ToLower().StartsWith(localPartPrefix))
                .Take(2)
                .ToListAsync(cancellationToken);

            if (candidates.Count == 1)
            {
                var match = candidates[0];
                _context.UserExternalLogins.Add(new UserExternalLogin
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    UserId = match.Id,
                    Provider = ProviderName,
                    ProviderUserId = identity.HaUserId,
                    ProviderDisplayName = identity.DisplayName,
                    ProviderEmail = null,
                    LastUsedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Linked HA Ingress identity (ha={HaUserId}) to existing Famick user {UserId} by email local-part match",
                    identity.HaUserId, match.Id);
                return match;
            }

            if (candidates.Count > 1)
            {
                _logger.LogWarning(
                    "HA Ingress username {HaUsername} matches multiple Famick users by email local part; provisioning a new HA-Ingress user instead",
                    identity.Username);
            }
        }

        var isFirstUser = !await _context.Users.AnyAsync(cancellationToken);

        // HA Ingress headers never carry an email — Supervisor strips it for
        // privacy. Synthesize one against the .local TLD (RFC 6762 reserved,
        // never deliverable) so the existing unique-email constraint is
        // satisfied per-user without ever clashing with a real address.
        var syntheticEmail = $"{identity.HaUserId.ToLowerInvariant()}@{SyntheticEmailDomain}";

        var displayName = identity.DisplayName ?? identity.Username ?? "HA User";
        var firstName = displayName.Split(' ').FirstOrDefault() ?? "HA";
        var lastName = displayName.Contains(' ') ? displayName[(displayName.IndexOf(' ') + 1)..] : "User";

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = syntheticEmail,
            Username = identity.Username ?? syntheticEmail,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = string.Empty,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _context.Users.Add(user);
        _context.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            Role = isFirstUser ? Role.Admin : Role.Editor,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _context.UserExternalLogins.Add(new UserExternalLogin
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            Provider = ProviderName,
            ProviderUserId = identity.HaUserId,
            ProviderDisplayName = identity.DisplayName,
            ProviderEmail = null,
            LastUsedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Provisioned HA Ingress user {UserId} (ha={HaUserId}, role={Role})",
            user.Id, identity.HaUserId, isFirstUser ? Role.Admin : Role.Editor);

        return user;
    }

    private Guid ResolveTenantId()
    {
        var raw = _configuration["SelfHosted:TenantId"];
        return Guid.TryParse(raw, out var parsed)
            ? parsed
            : Guid.Parse("00000000-0000-0000-0000-000000000001");
    }
}
