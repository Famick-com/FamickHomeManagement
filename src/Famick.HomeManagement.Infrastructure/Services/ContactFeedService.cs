using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class ContactFeedService : IContactFeedService
{
    private readonly HomeManagementDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<ContactFeedService> _logger;

    public ContactFeedService(
        HomeManagementDbContext context,
        IFileStorageService fileStorageService,
        ILogger<ContactFeedService> logger)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    public async Task<List<UserContactVcfTokenDto>> GetTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _context.UserContactVcfTokens
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return tokens.Select(MapToDto).ToList();
    }

    public async Task<UserContactVcfTokenDto> CreateTokenAsync(
        CreateVcfTokenRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var secret = FeedTokens.Generate();
        var token = new UserContactVcfToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = FeedTokens.Hash(secret),
            IsRevoked = false,
            Label = request.Label
        };

        _context.UserContactVcfTokens.Add(token);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created VCF token {TokenId} for user {UserId}", token.Id, userId);

        // The only time the token is available: only its hash is kept.
        var dto = MapToDto(token);
        dto.Token = secret;
        return dto;
    }

    public async Task RevokeTokenAsync(
        Guid tokenId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // A token belongs to the person who made it; anyone else gets the same answer as for a
        // token that doesn't exist.
        var token = await _context.UserContactVcfTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId, cancellationToken);

        if (token == null)
            throw new EntityNotFoundException(nameof(UserContactVcfToken), tokenId);

        token.IsRevoked = true;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Revoked VCF token {TokenId}", tokenId);
    }

    public async Task DeleteTokenAsync(
        Guid tokenId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // A token belongs to the person who made it; anyone else gets the same answer as for a
        // token that doesn't exist.
        var token = await _context.UserContactVcfTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId, cancellationToken);

        if (token == null)
            throw new EntityNotFoundException(nameof(UserContactVcfToken), tokenId);

        _context.UserContactVcfTokens.Remove(token);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted VCF token {TokenId}", tokenId);
    }

    public async Task<string?> GenerateVcfFeedAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        // Look up token WITHOUT tenant query filter (unauthenticated request). The feed stops when
        // its owner is deactivated or the household is, not only when the token is revoked.
        var tokenHash = FeedTokens.Hash(token);
        var tokenEntity = await _context.UserContactVcfTokens
            .IgnoreQueryFilters()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash
                && !t.IsRevoked
                && t.User!.IsActive
                && _context.Tenants.IgnoreQueryFilters().Any(h => h.Id == t.TenantId && h.IsActive),
                cancellationToken);

        if (tokenEntity == null)
        {
            _logger.LogWarning("VCF feed requested with invalid or revoked token");
            return null;
        }

        var tenantId = tokenEntity.TenantId;

        // The feed is the token owner's view of the household's contacts, not the whole table:
        // a contact somebody else keeps private must not land in this person's phone.
        var visible = ContactVisibility.VisibleTo(tokenEntity.UserId);

        var document = await new ContactVcfWriter(_context, _fileStorageService, _logger)
            .WriteAsync(tenantId, visible, cancellationToken);

        return document.Content;
    }

    #region Private Methods

    private static UserContactVcfTokenDto MapToDto(UserContactVcfToken token)
    {
        return new UserContactVcfTokenDto
        {
            Id = token.Id,
            Label = token.Label,
            IsRevoked = token.IsRevoked,
            CreatedAt = token.CreatedAt
        };
    }

    #endregion
}
