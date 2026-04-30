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

    [Fact]
    public async Task ProviderName_IsLibpostal()
    {
        var (canonicalizer, _, _) = Create();
        canonicalizer.ProviderName.Should().Be("Libpostal");
    }

    [Fact]
    public async Task CanonicalizeAsync_ParsesAndCanonicalizesAddress()
    {
        var responder = (HttpRequestMessage req) =>
        {
            // /expand → returns multiple expansions, we pick the first
            if (req.RequestUri!.AbsolutePath.EndsWith("/expand"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """["123 north main street springfield illinois 62701 usa", "another expansion"]""",
                        Encoding.UTF8, "application/json")
                };
            }
            // /parser → splits the canonical form into labeled components
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "[" +
                    "{\"label\":\"house_number\",\"value\":\"123\"}," +
                    "{\"label\":\"road\",\"value\":\"north main street\"}," +
                    "{\"label\":\"city\",\"value\":\"springfield\"}," +
                    "{\"label\":\"state\",\"value\":\"illinois\"}," +
                    "{\"label\":\"postcode\",\"value\":\"62701\"}," +
                    "{\"label\":\"country\",\"value\":\"usa\"}]",
                    Encoding.UTF8, "application/json")
            };
        };
        var (canonicalizer, handler, _) = Create(responder: responder);

        var result = await canonicalizer.CanonicalizeAsync(
            new AddressComponentsInput("123 N Main St", "Springfield", "IL", "62701", "USA"));

        result.Line1.Should().Be("123 north main street");
        result.City.Should().Be("springfield");
        result.State.Should().Be("illinois");
        result.PostalCode.Should().Be("62701");
        result.Country.Should().Be("usa");
        handler.CallCount.Should().Be(2); // /expand + /parser
    }

    [Fact]
    public async Task CanonicalizeAsync_PicksFirstExpansion_Deterministically()
    {
        // Two different addresses with the same first expansion must produce
        // identical canonical components — that's how the hash stays stable.
        var responder = (HttpRequestMessage req) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/expand"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """["123 main street", "alternate", "yet another"]""",
                        Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """[{"label":"house_number","value":"123"},{"label":"road","value":"main street"}]""",
                    Encoding.UTF8, "application/json")
            };
        };
        var (canonicalizer, _, _) = Create(responder: responder);

        var first = await canonicalizer.CanonicalizeAsync(
            new AddressComponentsInput("123 Main St", null, null, null, null));
        var second = await canonicalizer.CanonicalizeAsync(
            new AddressComponentsInput("123 Main St", null, null, null, null));

        first.Line1.Should().Be("123 main street");
        second.Line1.Should().Be("123 main street");
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
        var responder = (HttpRequestMessage req) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/expand"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""["123 main street"]""", Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """[{"label":"house_number","value":"123"},{"label":"road","value":"main street"}]""",
                    Encoding.UTF8, "application/json")
            };
        };
        var (canonicalizer, handler, _) = Create(responder: responder);

        var input = new AddressComponentsInput("123 Main St", null, null, null, null);
        var first = await canonicalizer.CanonicalizeAsync(input);
        var second = await canonicalizer.CanonicalizeAsync(input);

        first.Line1.Should().Be("123 main street");
        second.Line1.Should().Be("123 main street");
        handler.CallCount.Should().Be(2); // First call only — second is cached
    }

    [Fact]
    public async Task CanonicalizeAsync_ReturnsInputUnchanged_OnEmptyExpansionList()
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
