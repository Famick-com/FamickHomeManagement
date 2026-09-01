using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Messaging.DTOs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// The expiry alert's heading and counts, which a reader takes at face value.
/// </summary>
public class ExpiryEvaluatorTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    public ExpiryEvaluatorTests()
    {
        _context = new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        _context.Locations.Add(new Location
        {
            Id = _locationId, TenantId = _tenantId, Name = "Pantry", IsActive = true
        });
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(), TenantId = _tenantId,
            Email = "mike@example.com", Username = "mike", IsActive = true
        });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    /// <summary>
    /// The heading used to say "expiring soon" whatever the list held, so a message about
    /// 144 long-expired items opened by describing them all as upcoming — directly
    /// contradicting its own summary one line below.
    /// </summary>
    [Fact]
    public async Task EverythingExpiredIsNotDescribedAsExpiringSoon()
    {
        AddStock("Marshmallows", daysFromToday: -30);
        AddStock("Cumin", daysFromToday: -10);

        var data = await EvaluateAsync();

        data.Title.Should().Be("2 item(s) expired");
        data.Title.Should().NotContain("expiring soon");
    }

    [Fact]
    public async Task NothingExpiredYetReadsAsExpiringSoon()
    {
        AddStock("Yoghurt", daysFromToday: 2);

        (await EvaluateAsync()).Title.Should().Be("1 item(s) expiring soon");
    }

    [Fact]
    public async Task AMixOfBothAvoidsClaimingEitherOne()
    {
        AddStock("Marshmallows", daysFromToday: -30);
        AddStock("Yoghurt", daysFromToday: 2);

        var data = await EvaluateAsync();

        data.Title.Should().Be("2 item(s) need attention");
        data.Summary.Should().Be("1 expired; 1 expiring soon");
    }

    /// <summary>
    /// Two of the same thing bought together are two stock entries, and were printed as two
    /// identical lines. They now share one line, but still count as two items — the totals
    /// describe the cupboard, not the layout.
    /// </summary>
    [Fact]
    public async Task RepeatedEntriesShareALineWithoutChangingTheCount()
    {
        AddStock("Marshmallows", daysFromToday: -30);
        AddStock("Marshmallows", daysFromToday: -30);

        var data = await EvaluateAsync();

        data.ExpiringItems.Should().ContainSingle("identical entries belong on one line");
        data.ExpiringItems[0].Quantity.Should().Be(2);
        data.ExpiringItems[0].QuantitySuffix.Should().Be(" × 2");

        data.Title.Should().Be("2 item(s) expired", "there are still two jars in the pantry");
        data.ExpiredCount.Should().Be(2);
    }

    [Fact]
    public async Task TheSameProductInTwoPlacesStaysOnSeparateLines()
    {
        var basement = Guid.NewGuid();
        _context.Locations.Add(new Location
        {
            Id = basement, TenantId = _tenantId, Name = "Basement", IsActive = true
        });
        await _context.SaveChangesAsync();

        AddStock("Marshmallows", daysFromToday: -30);
        AddStock("Marshmallows", daysFromToday: -30, locationId: basement);

        // Where a thing is stored is the point of the message — someone has to go and find
        // it — so two places are two lines however alike they otherwise look.
        (await EvaluateAsync()).ExpiringItems.Should().HaveCount(2);
    }

    private void AddStock(string productName, int daysFromToday, Guid? locationId = null)
    {
        var productId = Guid.NewGuid();

        _context.Products.Add(new Product
        {
            Id = productId, TenantId = _tenantId, Name = productName, IsActive = true
        });
        _context.Stock.Add(new StockEntry
        {
            Id = Guid.NewGuid(), TenantId = _tenantId,
            ProductId = productId,
            LocationId = locationId ?? _locationId,
            Amount = 1,
            BestBeforeDate = DateTime.UtcNow.Date.AddDays(daysFromToday)
        });
        _context.SaveChanges();
    }

    private async Task<ExpiryData> EvaluateAsync()
    {
        var evaluator = new ExpiryEvaluator(
            _context,
            Options.Create(new NotificationSettings { DefaultExpiryWarningDays = 7 }),
            Mock.Of<ILogger<ExpiryEvaluator>>());

        var items = await evaluator.EvaluateAsync(_tenantId, CancellationToken.None);

        return items.Select(i => i.Data).OfType<ExpiryData>().First();
    }
}
