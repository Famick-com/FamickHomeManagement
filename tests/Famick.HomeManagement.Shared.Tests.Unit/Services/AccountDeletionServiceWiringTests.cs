using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Messaging.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Resolves the service through a real container rather than constructing it directly.
/// </summary>
/// <remarks>
/// The other tests hand dependencies in by hand, which proves the logic but says nothing
/// about whether the container supplies them the same way. The message service in
/// particular is resolved on demand from an injected <see cref="IServiceProvider"/> — an
/// optional constructor parameter — and if the container declined to supply it the send
/// path would return silently and no email would ever go out, with nothing logged to say
/// so. That is exactly the failure this pins down.
/// </remarks>
public class AccountDeletionServiceWiringTests
{
    [Fact]
    public async Task ResolvedFromTheContainerItCanStillReachTheMessageService()
    {
        var messages = new Mock<IMessageService>();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<HomeManagementDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IJwtMinIatService>(_ => Mock.Of<IJwtMinIatService>());
        services.AddScoped<IMessageService>(_ => messages.Object);
        services.AddScoped<IAccountDeletionService, AccountDeletionService>();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "The Therien Family" });
        db.Users.Add(new User
        {
            Id = userId, TenantId = tenantId, Email = "someone@example.com", Username = "someone"
        });
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>();

        await service.RequestAsync(userId);

        messages.Verify(
            m => m.SendTransactionalAsync(
                "someone@example.com",
                MessageType.AccountDeletionScheduled,
                It.IsAny<IMessageData>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "a deletion scheduled through the container must still send its email");
    }
}
