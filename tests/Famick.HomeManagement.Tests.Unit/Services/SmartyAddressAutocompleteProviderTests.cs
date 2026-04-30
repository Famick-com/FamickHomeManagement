using System.Net;
using System.Text;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Tests.Unit.Services;

public class SmartyAddressAutocompleteProviderTests
{
    private static SmartyOptions DefaultOptions() => new()
    {
        AuthId = "test-id",
        AuthToken = "test-token",
        AutocompleteBaseUrl = "https://us-autocomplete-pro.api.smarty.com",
        StreetBaseUrl = "https://us-street.api.smarty.com"
    };

    private static SmartyAddressAutocompleteProvider CreateProvider(SmartyOptions options, StubMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new SmartyAddressAutocompleteProvider(
            httpClient,
            Options.Create(options),
            NullLogger<SmartyAddressAutocompleteProvider>.Instance);
    }

    [Fact]
    public void ProviderName_IsSmarty()
    {
        var provider = CreateProvider(DefaultOptions(), StubMessageHandler.AlwaysThrow());
        provider.ProviderName.Should().Be("Smarty");
    }

    [Fact]
    public async Task AutocompleteAsync_ReturnsMappedSuggestions_OnSuccess()
    {
        var json = """
        { "suggestions": [
            { "street_line": "123 Main St", "secondary": "Apt 4", "city": "Springfield", "state": "IL", "zipcode": "62701", "entries": 1 },
            { "street_line": "124 Main St", "secondary": "", "city": "Springfield", "state": "IL", "zipcode": "62701", "entries": 5 }
        ] }
        """;
        var handler = StubMessageHandler.Returning(HttpStatusCode.OK, json);
        var provider = CreateProvider(DefaultOptions(), handler);

        var result = await provider.AutocompleteAsync("123 Main", 5);

        result.Should().HaveCount(2);
        result[0].Line1.Should().Be("123 Main St");
        result[0].Line2.Should().Be("Apt 4");
        result[0].City.Should().Be("Springfield");
        result[0].State.Should().Be("IL");
        result[0].PostalCode.Should().Be("62701");
        result[0].Country.Should().Be("USA");
        result[0].CountryCode.Should().Be("US");
        result[0].SecondaryCount.Should().Be(1);

        result[1].Line2.Should().BeNull();
        result[1].SecondaryCount.Should().Be(5);

        var requestUri = handler.LastRequest!.RequestUri!.ToString();
        requestUri.Should().Contain("123 Main").And.Contain("auth-id=test-id");
    }

    [Fact]
    public async Task AutocompleteAsync_ReturnsEmpty_WhenCredentialsMissing()
    {
        var handler = StubMessageHandler.AlwaysThrow();
        var provider = CreateProvider(new SmartyOptions { AuthId = "", AuthToken = "" }, handler);

        var result = await provider.AutocompleteAsync("123 Main");

        result.Should().BeEmpty();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task AutocompleteAsync_ReturnsEmpty_OnNon2xxStatus()
    {
        var handler = StubMessageHandler.Returning(HttpStatusCode.Unauthorized, "");
        var provider = CreateProvider(DefaultOptions(), handler);

        var result = await provider.AutocompleteAsync("123 Main");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AutocompleteAsync_ReturnsEmpty_OnMalformedJson()
    {
        var handler = StubMessageHandler.Returning(HttpStatusCode.OK, "not json");
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
    public async Task StandardizeAsync_MapsFirstCandidate_OnSuccess()
    {
        var json = """
        [
          {
            "delivery_line_1": "123 Main St",
            "delivery_line_2": "Apt 4",
            "components": { "city_name": "Springfield", "state_abbreviation": "IL", "zipcode": "62701", "plus4_code": "1234" },
            "metadata": { "latitude": 39.78, "longitude": -89.65 }
          }
        ]
        """;
        var handler = StubMessageHandler.Returning(HttpStatusCode.OK, json);
        var provider = CreateProvider(DefaultOptions(), handler);

        var result = await provider.StandardizeAsync(new ExternalStandardizeInput
        {
            Line1 = "123 Main",
            City = "Springfield",
            State = "IL"
        });

        result.Should().NotBeNull();
        result!.Line1.Should().Be("123 Main St");
        result.Line2.Should().Be("Apt 4");
        result.City.Should().Be("Springfield");
        result.State.Should().Be("IL");
        result.PostalCode.Should().Be("62701-1234");
        result.Country.Should().Be("USA");
        result.CountryCode.Should().Be("US");
        result.Latitude.Should().Be(39.78);
        result.Longitude.Should().Be(-89.65);
        result.FormattedAddress.Should().Contain("123 Main St");
    }

    [Fact]
    public async Task StandardizeAsync_ReturnsNull_WhenNoCandidates()
    {
        var handler = StubMessageHandler.Returning(HttpStatusCode.OK, "[]");
        var provider = CreateProvider(DefaultOptions(), handler);

        var result = await provider.StandardizeAsync(new ExternalStandardizeInput { Line1 = "123 Main" });

        result.Should().BeNull();
    }

    [Fact]
    public async Task StandardizeAsync_ReturnsNull_WhenCredentialsMissing()
    {
        var handler = StubMessageHandler.AlwaysThrow();
        var provider = CreateProvider(new SmartyOptions(), handler);

        var result = await provider.StandardizeAsync(new ExternalStandardizeInput { Line1 = "123 Main" });

        result.Should().BeNull();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task StandardizeAsync_ReturnsNull_OnHttpFailure()
    {
        var handler = StubMessageHandler.Returning(HttpStatusCode.InternalServerError, "");
        var provider = CreateProvider(DefaultOptions(), handler);

        var result = await provider.StandardizeAsync(new ExternalStandardizeInput { Line1 = "123 Main" });

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExpandSecondariesAsync_BuildsSelectedQueryParameter()
    {
        var json = """
        { "suggestions": [
            { "street_line": "100 Tower Pl", "secondary": "APT 1", "city": "Atlanta", "state": "GA", "zipcode": "30303", "entries": 1 },
            { "street_line": "100 Tower Pl", "secondary": "APT 2", "city": "Atlanta", "state": "GA", "zipcode": "30303", "entries": 1 }
        ] }
        """;
        var handler = StubMessageHandler.Returning(HttpStatusCode.OK, json);
        var provider = CreateProvider(DefaultOptions(), handler);

        var parent = new ExternalAddressSuggestion
        {
            Line1 = "100 Tower Pl",
            City = "Atlanta",
            State = "GA",
            PostalCode = "30303",
            Country = "USA",
            SecondaryCount = 2
        };

        var result = await provider.ExpandSecondariesAsync(parent);

        result.Should().HaveCount(2);
        result[0].Line1.Should().Be("100 Tower Pl");
        result[0].Line2.Should().Be("APT 1");
        result[1].Line2.Should().Be("APT 2");

        var requestUri = handler.LastRequest!.RequestUri!.ToString();
        requestUri.Should().Contain("selected=");
        // The Smarty Pro Secondary Expansion contract: the selected value
        // round-trips the parent's components plus the entries count.
        var decoded = Uri.UnescapeDataString(requestUri);
        decoded.Should().Contain("100 Tower Pl");
        decoded.Should().Contain("(2)");
        decoded.Should().Contain("Atlanta");
        decoded.Should().Contain("GA");
        decoded.Should().Contain("30303");
    }

    [Fact]
    public async Task ExpandSecondariesAsync_ReturnsEmpty_WhenCredentialsMissing()
    {
        var handler = StubMessageHandler.AlwaysThrow();
        var provider = CreateProvider(new SmartyOptions { AuthId = "", AuthToken = "" }, handler);

        var result = await provider.ExpandSecondariesAsync(new ExternalAddressSuggestion
        {
            Line1 = "100 Tower Pl",
            SecondaryCount = 2
        });

        result.Should().BeEmpty();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ExpandSecondariesAsync_ReturnsEmpty_OnTransportError()
    {
        var handler = StubMessageHandler.AlwaysThrow();
        var provider = CreateProvider(DefaultOptions(), handler);

        var result = await provider.ExpandSecondariesAsync(new ExternalAddressSuggestion
        {
            Line1 = "100 Tower Pl",
            SecondaryCount = 2
        });

        result.Should().BeEmpty();
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
