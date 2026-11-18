using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Household-link messages are offered as preferences only where households can be linked.
/// </summary>
public class NotificationPreferenceTests : IDisposable
{
    private static readonly MessageType[] HouseholdLinkTypes =
        [MessageType.HouseholdLinked, MessageType.HouseholdLinkBroken, MessageType.LinkedHouseholdJoined];

    private readonly HomeManagementDbContext _context = new(
        new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        Mock.Of<ITenantProvider>());

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ASingleHouseholdServerLeavesHouseholdLinkMessagesOut()
    {
        var service = new NotificationService(_context, NullLogger<NotificationService>.Instance,
            new MultiTenancyOptions { IsMultiTenantEnabled = false });

        var preferences = await service.GetPreferencesAsync(Guid.NewGuid());

        preferences.Select(p => p.MessageType).Should().NotContain(HouseholdLinkTypes);
        preferences.Select(p => p.MessageType).Should().Contain(MessageType.Expiry);
    }

    [Fact]
    public async Task AServerThatLinksHouseholdsOffersThem()
    {
        var service = new NotificationService(_context, NullLogger<NotificationService>.Instance,
            new MultiTenancyOptions { IsMultiTenantEnabled = true });

        var preferences = await service.GetPreferencesAsync(Guid.NewGuid());

        preferences.Select(p => p.MessageType).Should().Contain(HouseholdLinkTypes);
        preferences.Single(p => p.MessageType == MessageType.HouseholdLinkBroken).DisplayName
            .Should().Be("Household Unlinked");
    }
}
