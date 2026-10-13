using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Calendar;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Net;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Issue #107: subscriptions fetch only through the guarded fetcher, the request is
/// validated in the service, and a failed sync never echoes exception text.
/// </summary>
public class ExternalCalendarServiceFetchTests
{
    private sealed class FakeFetcher(bool allowPrivateNetworks = false) : IOutboundHttpFetcher
    {
        private readonly OutboundHttpFetcher _urlRules = new(
            null!, new OutboundFetchOptions(), allowPrivateNetworks, NullLogger<OutboundHttpFetcher>.Instance);

        public Func<Uri, OutboundFetchResult> Respond { get; set; } = _ => throw new InvalidOperationException();
        public long? LastMaxBytes { get; private set; }

        public OutboundFetchFailure? CheckUrl(Uri url) => _urlRules.CheckUrl(url);

        public Task<OutboundFetchResult> GetAsync(Uri url, long maxBytes, CancellationToken cancellationToken = default)
        {
            LastMaxBytes = maxBytes;
            return Task.FromResult(Respond(url));
        }
    }

    private readonly HomeManagementDbContext _context = new(
        new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private readonly FakeFetcher _fetcher = new();

    private ExternalCalendarService Service() => new(
        _context, _fetcher, Options.Create(new CalendarSettings()), NullLogger<ExternalCalendarService>.Instance);

    private static CreateExternalCalendarSubscriptionRequest Request(string url, string name = "Work") =>
        new() { Name = name, IcsUrl = url, SyncIntervalMinutes = 60 };

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://example.com/")]
    [InlineData("http://example.com:8080/cal.ics")]
    [InlineData("webcal://example.com:6379/cal.ics")]
    public async Task Create_refuses_a_url_the_fetcher_will_not_fetch(string url)
    {
        var create = () => Service().CreateSubscriptionAsync(Request(url), Guid.NewGuid());

        await create.Should().ThrowAsync<ValidationException>();
        (await _context.ExternalCalendarSubscriptions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_runs_the_request_validator()
    {
        var create = () => Service().CreateSubscriptionAsync(Request("https://example.com/cal.ics", name: ""), Guid.NewGuid());

        await create.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Create_turns_webcal_into_https()
    {
        var dto = await Service().CreateSubscriptionAsync(Request("webcal://example.com/cal.ics"), Guid.NewGuid());

        dto.IcsUrl.Should().Be("https://example.com/cal.ics");
    }

    [Fact]
    public async Task A_refused_fetch_stores_a_generic_status()
    {
        var subscription = await SeedAsync();
        _fetcher.Respond = _ => throw new OutboundFetchException(OutboundFetchFailure.AddressNotAllowed);

        await Service().SyncSubscriptionAsync(subscription.Id);

        subscription.LastSyncStatus.Should().Be("Error: this address cannot be fetched");
        _fetcher.LastMaxBytes.Should().Be(new CalendarSettings().MaxIcsFeedBytes);
    }

    [Fact]
    public async Task An_unexpected_failure_does_not_echo_the_exception_message()
    {
        var subscription = await SeedAsync();
        _fetcher.Respond = _ => throw new InvalidOperationException("connect to 10.0.3.7:5432 failed");

        await Service().SyncSubscriptionAsync(subscription.Id);

        subscription.LastSyncStatus.Should().Be("Error: the calendar feed could not be read");
        subscription.LastSyncStatus.Should().NotContain("10.0.3.7");
    }

    [Fact]
    public async Task A_successful_fetch_is_parsed()
    {
        var subscription = await SeedAsync();
        _fetcher.Respond = url => new OutboundFetchResult(System.Text.Encoding.UTF8.GetBytes(
            "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//test//EN\r\nBEGIN:VEVENT\r\nUID:1@test\r\n" +
            "DTSTAMP:20261013T120000Z\r\nDTSTART:20261014T120000Z\r\nDTEND:20261014T130000Z\r\n" +
            "SUMMARY:Standup\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n"), "text/calendar", url);

        await Service().SyncSubscriptionAsync(subscription.Id);

        subscription.LastSyncStatus.Should().Be("Success (1 events)");
    }

    private async Task<ExternalCalendarSubscription> SeedAsync()
    {
        var subscription = new ExternalCalendarSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Work",
            IcsUrl = "https://example.com/cal.ics",
            IsActive = true
        };
        _context.ExternalCalendarSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();
        return subscription;
    }
}
