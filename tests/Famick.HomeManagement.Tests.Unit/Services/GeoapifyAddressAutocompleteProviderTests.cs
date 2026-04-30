using System.Net;
using System.Text;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Tests.Unit.Services;

public class GeoapifyAddressAutocompleteProviderTests
{
    private static GeoapifyOptions DefaultOptions() => new()
    {
        ApiKey = "test-key",
        BaseUrl = "https://api.geoapify.com/v1/geocode"
    };

    private static GeoapifyAddressAutocompleteProvider CreateProvider(GeoapifyOptions options, StubMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new GeoapifyAddressAutocompleteProvider(
            httpClient,
            Options.Create(options),
            NullLogger<GeoapifyAddressAutocompleteProvider>.Instance);
    }

    [Fact]
    public void ProviderName_IsGeoapify()
    {
        var provider = CreateProvider(DefaultOptions(), StubMessageHandler.AlwaysThrow());
        provider.ProviderName.Should().Be("Geoapify");
    }

    [Fact]
    public async Task AutocompleteAsync_ReturnsMappedSuggestions_OnSuccess()
    {
        var json = """
        { "results": [
            {
                "housenumber": "123",
                "street": "Main St",
                "address_line1": "123 Main St",
                "city": "Springfield",
                "state": "Illinois",
                "postcode": "62701",
                "country": "United States",
                "country_code": "us",
                "lat": 39.78,
                "lon": -89.65,
                "place_id": "abc-123",
                "formatted": "123 Main St, Springfield, IL 62701, United States"
            }
        ] }
        """;
        var handler = StubMessageHandler.Returning(HttpStatusCode.OK, json);
        var provider = CreateProvider(DefaultOptions(), handler);

        var result = await provider.AutocompleteAsync("123 Main", 5);

        result.Should().HaveCount(1);
        result[0].Line1.Should().Be("123 Main St");
        result[0].City.Should().Be("Springfield");
        result[0].State.Should().Be("Illinois");
        result[0].PostalCode.Should().Be("62701");
        result[0].Country.Should().Be("United States");
        result[0].CountryCode.Should().Be("US");
        result[0].Latitude.Should().Be(39.78);
        result[0].Longitude.Should().Be(-89.65);
        result[0].ProviderPlaceId.Should().Be("abc-123");

        var requestUri = handler.LastRequest!.RequestUri!.ToString();
        requestUri.Should().Contain("autocomplete")
            .And.Contain("apiKey=test-key")
            .And.Contain("123 Main");
    }

    [Fact]
    public async Task AutocompleteAsync_FallsBackToAddressLine1_WhenHousenumberMissing()
    {
        var json = """
        { "results": [
            { "address_line1": "Just The Street", "city": "Somewhere", "country_code": "us" }
        ] }
        """;
        var handler = StubMessageHandler.Returning(HttpStatusCode.OK, json);
        var provider = CreateProvider(DefaultOptions(), handler);

        var result = await provider.AutocompleteAsync("just");

        result.Should().HaveCount(1);
        result[0].Line1.Should().Be("Just The Street");
    }

    [Fact]
    public async Task AutocompleteAsync_ReturnsEmpty_WhenApiKeyMissing()
    {
        var handler = StubMessageHandler.AlwaysThrow();
        var provider = CreateProvider(new GeoapifyOptions { ApiKey = "" }, handler);

        var result = await provider.AutocompleteAsync("123 Main");

        result.Should().BeEmpty();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task AutocompleteAsync_ReturnsEmpty_OnNon2xxStatus()
    {
        var handler = StubMessageHandler.Returning(HttpStatusCode.Forbidden, "");
        var provider = CreateProvider(DefaultOptions(), handler);

        var result = await provider.AutocompleteAsync("123 Main");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AutocompleteAsync_ReturnsEmpty_OnTransportException()
    {
        var handler = StubMessageHandler.AlwaysThrow();
        var provider = CreateProvider(DefaultOptions(), handler);

        var result = await provider.AutocompleteAsync("123 Main");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AutocompleteAsync_PropagatesCancellation()
    {
        var handler = StubMessageHandler.AlwaysThrowCanceled();
        var provider = CreateProvider(DefaultOptions(), handler);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => provider.AutocompleteAsync("123 Main", 5, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task StandardizeAsync_UsesPlaceDetails_WhenPlaceIdSupplied()
    {
        var placeDetailsJson = """
        { "features": [ { "properties": {
            "address_line1": "200 Market St",
            "city": "Portland",
            "state": "Oregon",
            "postcode": "97201",
            "country": "United States",
            "country_code": "us",
            "lat": 45.52,
            "lon": -122.68,
            "place_id": "pid-abc",
            "formatted": "200 Market St, Portland, OR 97201, United States"
        } } ] }
        """;
        var handler = StubMessageHandler.Returning(HttpStatusCode.OK, placeDetailsJson);
        var provider = CreateProvider(DefaultOptions(), handler);

        var result = await provider.StandardizeAsync(new ExternalStandardizeInput
        {
            ProviderPlaceId = "pid-abc"
        });

        result.Should().NotBeNull();
        result!.Line1.Should().Be("200 Market St");
        result.City.Should().Be("Portland");
        result.PostalCode.Should().Be("97201");
        result.CountryCode.Should().Be("US");
        result.Latitude.Should().Be(45.52);
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("place-details")
            .And.Contain("id=pid-abc");
    }

    [Fact]
    public async Task StandardizeAsync_FallsBackToSearch_WhenNoPlaceId()
    {
        var searchJson = """
        { "results": [ {
            "address_line1": "221B Baker St",
            "city": "London",
            "country": "United Kingdom",
            "country_code": "gb",
            "formatted": "221B Baker St, London, United Kingdom"
        } ] }
        """;
        var handler = StubMessageHandler.Returning(HttpStatusCode.OK, searchJson);
        var provider = CreateProvider(DefaultOptions(), handler);

        var result = await provider.StandardizeAsync(new ExternalStandardizeInput
        {
            Line1 = "221b baker st",
            City = "London",
            Country = "UK"
        });

        result.Should().NotBeNull();
        result!.Line1.Should().Be("221B Baker St");
        result.Country.Should().Be("United Kingdom");
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("search");
    }

    [Fact]
    public async Task StandardizeAsync_ReturnsNull_WhenNoResults()
    {
        var handler = StubMessageHandler.Returning(HttpStatusCode.OK, "{ \"results\": [] }");
        var provider = CreateProvider(DefaultOptions(), handler);

        var result = await provider.StandardizeAsync(new ExternalStandardizeInput { Line1 = "nowhere" });

        result.Should().BeNull();
    }

    [Fact]
    public async Task StandardizeAsync_ReturnsNull_WhenApiKeyMissing()
    {
        var handler = StubMessageHandler.AlwaysThrow();
        var provider = CreateProvider(new GeoapifyOptions { ApiKey = "" }, handler);

        var result = await provider.StandardizeAsync(new ExternalStandardizeInput { Line1 = "123 Main" });

        result.Should().BeNull();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task StandardizeAsync_ReturnsNull_OnHttpFailure()
    {
        var handler = StubMessageHandler.Returning(HttpStatusCode.ServiceUnavailable, "");
        var provider = CreateProvider(DefaultOptions(), handler);

        var result = await provider.StandardizeAsync(new ExternalStandardizeInput { Line1 = "123 Main" });

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExpandSecondariesAsync_ReturnsEmpty_BecauseGeoapifyHasNoSecondaryContract()
    {
        var handler = StubMessageHandler.AlwaysThrow();
        var provider = CreateProvider(DefaultOptions(), handler);

        var result = await provider.ExpandSecondariesAsync(new ExternalAddressSuggestion
        {
            Line1 = "100 Tower Pl",
            SecondaryCount = 12
        });

        result.Should().BeEmpty();
        handler.CallCount.Should().Be(0);
    }

    private sealed class StubMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public int CallCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        private StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        public static StubMessageHandler Returning(HttpStatusCode status, string content) =>
            new(_ => new HttpResponseMessage(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });

        public static StubMessageHandler AlwaysThrow() =>
            new(_ => throw new HttpRequestException("boom"));

        public static StubMessageHandler AlwaysThrowCanceled() =>
            new(_ => throw new OperationCanceledException());

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_responder(request));
        }
    }
}
