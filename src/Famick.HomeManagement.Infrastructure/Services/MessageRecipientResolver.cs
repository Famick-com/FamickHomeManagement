using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Messaging.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Resolves message recipients from the database.
/// </summary>
public class MessageRecipientResolver : IMessageRecipientResolver
{
    private readonly HomeManagementDbContext _db;

    public MessageRecipientResolver(HomeManagementDbContext db)
    {
        _db = db;
    }

    public async Task<MessageRecipient?> ResolveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return null;

        return new MessageRecipient(
            UserId: user.Id,
            Email: user.Email,
            PhoneNumber: user.PhoneNumber,
            FirstName: user.FirstName,
            LastName: user.LastName,
            TenantId: user.TenantId,
            IsActive: user.IsActive);
    }
}
