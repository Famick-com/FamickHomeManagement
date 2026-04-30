using System.Net;
using System.Text;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Tests.Unit.Services;

public class LibpostalRestCanonicalizerTests
{
    private static LibpostalOptions DefaultOptions() => new()
    {
        BaseUrl = "http://libpostal:8080",
        TimeoutSeconds = 5
    };

    private static (LibpostalRestCanonicalizer canonicalizer, StubMessageHandler handler, IMemoryCache cache)
        Create(LibpostalOptions? options = null, Func<HttpRequestMessage, HttpResponseMessage>? responder = null)
    {
        var handler = responder == null
            ? StubMessageHandler.AlwaysThrow()
            : new StubMessageHandler(responder);
        var httpClient = new HttpClient(handler);
        var cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        var canonicalizer = new LibpostalRestCanonicalizer(
            httpClient,
            Options.Create(options ?? DefaultOptions()),
            cache,
            NullLogger<LibpostalRestCanonicalizer>.Instance);
        return (canonicalizer, handler, cache);
    }

    /// <summary>
    /// Helper for /expandparser response bodies. Each entry is one
    /// expansion (or the original query) with its parse.
    /// </summary>
    private static string ExpandParseResponse(params (string Type, string Data, (string Label, string Value)[] Parsed)[] entries)
    {
        var items = entries.Select(e =>
        {
            var parsedJson = string.Join(",", e.Parsed.Select(p =>
                $"{{\"label\":\"{p.Label}\",\"value\":\"{p.Value}\"}}"));
            return $"{{\"type\":\"{e.Type}\",\"data\":\"{e.Data}\",\"parsed\":[{parsedJson}]}}";
        });
        return $"[{string.Join(",", items)}]";
    }

    [Fact]
    public void ProviderName_IsLibpostal()
    {
        var (canonicalizer, _, _) = Create();
        canonicalizer.ProviderName.Should().Be("Libpostal");
    }

    [Fact]
    public async Task CanonicalizeAsync_ParsesAndCanonicalizesAddress()
    {
        var responder = (HttpRequestMessage req) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                ExpandParseResponse(
                    ("query", "123 N Main St, Springfield, IL 62701",
                        new[] { ("house_number", "123"), ("road", "n main st"), ("city", "springfield"), ("state", "il"), ("postcode", "62701") }),
                    ("expansion", "123 north main street springfield illinois 62701",
                        new[] { ("house_number", "123"), ("road", "north main street"), ("city", "springfield"), ("state", "illinois"), ("postcode", "62701") })),
                Encoding.UTF8, "application/json")
        };
        var (canonicalizer, handler, _) = Create(responder: responder);

        var result = await canonicalizer.CanonicalizeAsync(
            new AddressComponentsInput("123 N Main St", "Springfield", "IL", "62701", "USA"));

        result.Line1.Should().Be("123 north main street");
        result.City.Should().Be("springfield");
        result.State.Should().Be("illinois");
        result.PostalCode.Should().Be("62701");
        handler.CallCount.Should().Be(1); // Single /expandparser call
    }

    [Fact]
    public async Task CanonicalizeAsync_FiltersOutSaintExpansion_PreferringLongerRoad()
    {
        // The bug we're guarding against: libpostal expands "St" to both
        // "saint" (which reassigns the suffix to the city, shortening the
        // road) and "street" (correct, keeps the road intact). The
        // canonicalizer must pick the latter — longest-road wins.
        var responder = (HttpRequestMessage req) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                ExpandParseResponse(
                    ("query", "123 N Main St, Springfield, IL 62701",
                        new[] { ("house_number", "123"), ("road", "n main st"), ("city", "springfield"), ("state", "il"), ("postcode", "62701") }),
                    // Bad expansion: "saint" reassigns the suffix → shorter road, wrong city.
                    ("expansion", "123 north main saint springfield il 62701",
                        new[] { ("house_number", "123"), ("road", "north main"), ("city", "saint springfield"), ("state", "il"), ("postcode", "62701") }),
                    // Good expansion: full road preserved, city correct.
                    ("expansion", "123 north main street springfield il 62701",
                        new[] { ("house_number", "123"), ("road", "north main street"), ("city", "springfield"), ("state", "il"), ("postcode", "62701") })),
                Encoding.UTF8, "application/json")
        };
        var (canonicalizer, _, _) = Create(responder: responder);

        var result = await canonicalizer.CanonicalizeAsync(
            new AddressComponentsInput("123 N Main St", "Springfield", "IL", "62701", "USA"));

        result.Line1.Should().Be("123 north main street");
        result.City.Should().Be("springfield"); // not "saint springfield"
    }

    [Fact]
    public async Task CanonicalizeAsync_FallsBackToQueryParse_WhenNoExpansionsReturned()
    {
        // /expandparser only returns the type=query entry — no expansions.
        // Should still pull components from the query parse so we get
        // case-normalization at minimum.
        var responder = (HttpRequestMessage req) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                ExpandParseResponse(
                    ("query", "123 main st",
                        new[] { ("house_number", "123"), ("road", "main st") })),
                Encoding.UTF8, "application/json")
        };
        var (canonicalizer, _, _) = Create(responder: responder);

        var result = await canonicalizer.CanonicalizeAsync(
            new AddressComponentsInput("123 Main St", null, null, null, null));

        result.Line1.Should().Be("123 main st");
    }

    [Fact]
    public async Task CanonicalizeAsync_ReturnsInputUnchanged_OnHttpFailure()
    {
        var (canonicalizer, _, _) = Create(responder: _ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await canonicalizer.CanonicalizeAsync(
            new AddressComponentsInput("123 N Main St", "Springfield", "IL", "62701", "USA"));

        result.Line1.Should().Be("123 N Main St");
        result.City.Should().Be("Springfield");
        result.State.Should().Be("IL");
    }

    [Fact]
    public async Task CanonicalizeAsync_ReturnsInputUnchanged_OnTransportException()
    {
        var (canonicalizer, _, _) = Create(); // AlwaysThrow handler

        var result = await canonicalizer.CanonicalizeAsync(
            new AddressComponentsInput("123 Main St", "Springfield", "IL", "62701", "USA"));

        result.Line1.Should().Be("123 Main St");
        result.City.Should().Be("Springfield");
    }

    [Fact]
    public async Task CanonicalizeAsync_CachesByRawInput()
    {
        var responder = (HttpRequestMessage req) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                ExpandParseResponse(
                    ("expansion", "123 main street",
                        new[] { ("house_number", "123"), ("road", "main street") })),
                Encoding.UTF8, "application/json")
        };
        var (canonicalizer, handler, _) = Create(responder: responder);

        var input = new AddressComponentsInput("123 Main St", null, null, null, null);
        var first = await canonicalizer.CanonicalizeAsync(input);
        var second = await canonicalizer.CanonicalizeAsync(input);

        first.Line1.Should().Be("123 main street");
        second.Line1.Should().Be("123 main street");
        handler.CallCount.Should().Be(1); // First call only — second is cached
    }

    [Fact]
    public async Task CanonicalizeAsync_ReturnsInputUnchanged_OnEmptyResponse()
    {
        var (canonicalizer, _, _) = Create(responder: _ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });

        var result = await canonicalizer.CanonicalizeAsync(
            new AddressComponentsInput("123 Main St", "City", null, null, null));

        result.Line1.Should().Be("123 Main St");
        result.City.Should().Be("City");
    }

    [Fact]
    public async Task CanonicalizeAsync_ReturnsInputUnchanged_OnAllNullInputs()
    {
        var (canonicalizer, handler, _) = Create();

        var result = await canonicalizer.CanonicalizeAsync(
            new AddressComponentsInput(null, null, null, null, null));

        result.Line1.Should().BeNull();
        handler.CallCount.Should().Be(0); // No HTTP call — short-circuit on empty assembled query.
    }

    [Fact]
    public async Task CanonicalizeAsync_PropagatesUserCancellation()
    {
        var (canonicalizer, _, _) = Create(); // AlwaysThrow — but cancellation should fire first

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => canonicalizer.CanonicalizeAsync(
            new AddressComponentsInput("123 Main St", "City", null, null, null), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class StubMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public int CallCount { get; private set; }

        public StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        public static StubMessageHandler AlwaysThrow() =>
            new(_ => throw new HttpRequestException("boom"));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(_responder(request));
        }
    }
}
