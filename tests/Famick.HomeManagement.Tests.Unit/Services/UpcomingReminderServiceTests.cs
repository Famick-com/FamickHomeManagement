using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Tests.Unit.Services;

/// <summary>
/// Unit tests for the offline-reminder prefetch feed. Uses the EF InMemory provider; with no
/// ITenantProvider registered the global tenant filter returns all rows, so correctness relies on
/// the service's own explicit TenantId predicates (which mirror the daily-run evaluators).
/// </summary>
public class UpcomingReminderServiceTests : IDisposable
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-0000000000AA");

    private readonly ServiceProvider _serviceProvider;

    public UpcomingReminderServiceTests()
    {
        // The name is generated once, not inside the options lambda. EF invokes that lambda
        // per DbContext instance, so a Guid created inside it gives every scope its own
        // database — setup writes to one, the test reads from another, and every query comes
        // back empty. Which is why the assertions that expected nothing were the ones passing.
        var databaseName = $"upcoming-{Guid.NewGuid()}";

        var services = new ServiceCollection();
        services.AddDbContext<HomeManagementDbContext>(opt =>
            opt.UseInMemoryDatabase(databaseName));
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();

        // Every test needs a tenant row so the service can resolve a time zone (falls back to UTC).
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
        db.Tenants.Add(new Tenant { Id = TenantId, Name = "Test Home", TimeZoneId = "UTC" });
        db.Users.Add(new User { Id = UserId, TenantId = TenantId, IsActive = true });
        db.SaveChanges();
    }

    public void Dispose() => _serviceProvider.Dispose();

    private UpcomingReminderService CreateService(IServiceScope scope, NotificationSettings? settings = null)
    {
        var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
        return new UpcomingReminderService(
            db,
            Options.Create(settings ?? new NotificationSettings()),
            scope.ServiceProvider.GetRequiredService<ILogger<UpcomingReminderService>>());
    }

    [Fact]
    public async Task GetUpcoming_WhenNoData_ReturnsEmpty()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = CreateService(scope);

        var result = await service.GetUpcomingAsync(TenantId, UserId, days: 14);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcoming_CalendarEvent_FiresReminderMinutesBeforeStart()
    {
        var eventId = Guid.NewGuid();
        var startUtc = DateTime.UtcNow.AddDays(2).Date.AddHours(15); // fixed future time
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
            db.CalendarEvents.Add(new CalendarEvent
            {
                Id = eventId,
                TenantId = TenantId,
                Title = "Dentist",
                StartTimeUtc = startUtc,
                EndTimeUtc = startUtc.AddHours(1),
                ReminderMinutesBefore = 60,
                CreatedByUserId = UserId,
                Members = new List<CalendarEventMember>
                {
                    new() { Id = Guid.NewGuid(), CalendarEventId = eventId, UserId = UserId, ParticipationType = ParticipationType.Involved }
                }
            });
            db.SaveChanges();
        }

        using var scope2 = _serviceProvider.CreateScope();
        var result = await CreateService(scope2).GetUpcomingAsync(TenantId, UserId, days: 14);

        result.Should().ContainSingle();
        var reminder = result[0];
        reminder.Type.Should().Be(MessageType.CalendarReminder);
        reminder.FireAtUtc.Should().Be(startUtc.AddMinutes(-60));
        reminder.DeepLinkUrl.Should().Be($"/calendar/events/{eventId}");
        reminder.Key.Should().StartWith($"cal:{eventId}:");
    }

    [Fact]
    public async Task GetUpcoming_CalendarEvent_SkippedWhenUserNotInvolved()
    {
        var eventId = Guid.NewGuid();
        var startUtc = DateTime.UtcNow.AddDays(2).Date.AddHours(15);
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
            db.CalendarEvents.Add(new CalendarEvent
            {
                Id = eventId,
                TenantId = TenantId,
                Title = "Not mine",
                StartTimeUtc = startUtc,
                EndTimeUtc = startUtc.AddHours(1),
                ReminderMinutesBefore = 30,
                CreatedByUserId = UserId,
                Members = new List<CalendarEventMember>
                {
                    new() { Id = Guid.NewGuid(), CalendarEventId = eventId, UserId = Guid.NewGuid(), ParticipationType = ParticipationType.Involved }
                }
            });
            db.SaveChanges();
        }

        using var scope2 = _serviceProvider.CreateScope();
        var result = await CreateService(scope2).GetUpcomingAsync(TenantId, UserId, days: 14);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcoming_ExpiringStock_ProducesSingleExpiryDigest()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
            // Two items within the default 3-day warning window -> one aggregate digest.
            foreach (var (name, days) in new[] { ("Milk", 2), ("Eggs", 1) })
            {
                var productId = Guid.NewGuid();
                var product = new Product { Id = productId, TenantId = TenantId, Name = name, IsActive = true };
                db.Products.Add(product);
                db.Stock.Add(new StockEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    ProductId = productId,
                    Product = product,
                    Amount = 1m,
                    StockId = $"s-{name}",
                    BestBeforeDate = DateTime.UtcNow.Date.AddDays(days)
                });
            }
            db.SaveChanges();
        }

        using var scope2 = _serviceProvider.CreateScope();
        var result = await CreateService(scope2).GetUpcomingAsync(TenantId, UserId, days: 14);

        result.Should().ContainSingle(r => r.Type == MessageType.Expiry);
        var expiry = result.First(r => r.Type == MessageType.Expiry);
        expiry.FireAtUtc.Should().BeAfter(DateTime.UtcNow);
        expiry.Title.Should().Contain("2 item(s) expiring soon");
        expiry.DeepLinkUrl.Should().Be("/stock");
        expiry.Key.Should().StartWith("exp:");
    }

    [Fact]
    public async Task GetUpcoming_RespectsPushDisabledPreference()
    {
        var productId = Guid.NewGuid();
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
            var product = new Product { Id = productId, TenantId = TenantId, Name = "Eggs", IsActive = true };
            db.Products.Add(product);
            db.Stock.Add(new StockEntry
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProductId = productId,
                Product = product,
                Amount = 1m,
                StockId = "s2",
                BestBeforeDate = DateTime.UtcNow.Date.AddDays(2) // within default 3-day window
            });
            db.NotificationPreferences.Add(new NotificationPreference
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                UserId = UserId,
                MessageType = MessageType.Expiry,
                PushEnabled = false
            });
            db.SaveChanges();
        }

        using var scope2 = _serviceProvider.CreateScope();
        var result = await CreateService(scope2).GetUpcomingAsync(TenantId, UserId, days: 14);

        result.Should().NotContain(r => r.Type == MessageType.Expiry);
    }

    [Fact]
    public async Task GetUpcoming_IncompleteTodo_ProducesTaskDigest()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
            db.TodoItems.Add(new TodoItem
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Reason = "Take out trash",
                DateEntered = DateTime.UtcNow,
                IsCompleted = false
            });
            db.SaveChanges();
        }

        using var scope2 = _serviceProvider.CreateScope();
        var result = await CreateService(scope2).GetUpcomingAsync(TenantId, UserId, days: 14);

        result.Should().Contain(r => r.Type == MessageType.TaskSummary && r.Key.StartsWith("task:"));
    }

    [Fact]
    public async Task GetUpcoming_ResultsAreOrderedByFireTime()
    {
        using var scope = _serviceProvider.CreateScope();
        var result = await CreateService(scope).GetUpcomingAsync(TenantId, UserId, days: 14);

        result.Should().BeInAscendingOrder(r => r.FireAtUtc);
    }
}
