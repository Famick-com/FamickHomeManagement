using System.Net;
using System.Net.Sockets;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Net;

public class OutboundHttpFetcherTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requested { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static OutboundHttpFetcher Fetcher(HttpMessageHandler handler, bool allowPrivateNetworks = false) =>
        new(new SingleClientFactory(handler), new OutboundFetchOptions(), allowPrivateNetworks,
            NullLogger<OutboundHttpFetcher>.Instance);

    private static HttpResponseMessage Redirect(string location) =>
        new(HttpStatusCode.Found) { Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) } };

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/cal.ics")]
    [InlineData("gopher://example.com/")]
    [InlineData("http://example.com:8080/cal.ics")]
    [InlineData("https://example.com:6379/")]
    public void Strict_mode_refuses_other_schemes_and_ports(string url)
    {
        Fetcher(new StubHandler(_ => Ok(""))).CheckUrl(new Uri(url))
            .Should().Be(OutboundFetchFailure.UrlNotAllowed);
    }

    [Fact]
    public void Self_hosted_mode_allows_other_ports_but_still_only_http()
    {
        var fetcher = Fetcher(new StubHandler(_ => Ok("")), allowPrivateNetworks: true);

        fetcher.CheckUrl(new Uri("http://nas.local:5232/cal.ics")).Should().BeNull();
        fetcher.CheckUrl(new Uri("file:///etc/passwd")).Should().Be(OutboundFetchFailure.UrlNotAllowed);
    }

    [Fact]
    public async Task Returns_the_body_after_following_a_redirect()
    {
        var handler = new StubHandler(r => r.RequestUri!.AbsolutePath == "/old"
            ? Redirect("/new")
            : Ok("BEGIN:VCALENDAR"));

        var result = await Fetcher(handler).GetAsync(new Uri("https://example.com/old"), 1024);

        System.Text.Encoding.UTF8.GetString(result.Body).Should().Be("BEGIN:VCALENDAR");
        result.FinalUrl.Should().Be(new Uri("https://example.com/new"));
    }

    [Theory]
    [InlineData("http://example.com:8080/internal")]
    [InlineData("file:///etc/passwd")]
    public async Task Checks_every_redirect_hop(string target)
    {
        var handler = new StubHandler(_ => Redirect(target));

        var fetch = () => Fetcher(handler).GetAsync(new Uri("https://example.com/cal.ics"), 1024);

        (await fetch.Should().ThrowAsync<OutboundFetchException>())
            .Which.Failure.Should().Be(OutboundFetchFailure.UrlNotAllowed);
        handler.Requested.Should().ContainSingle("the refused hop must never be requested");
    }

    [Fact]
    public async Task Gives_up_on_a_redirect_loop()
    {
        var handler = new StubHandler(_ => Redirect("https://example.com/again"));

        var fetch = () => Fetcher(handler).GetAsync(new Uri("https://example.com/"), 1024);

        (await fetch.Should().ThrowAsync<OutboundFetchException>())
            .Which.Failure.Should().Be(OutboundFetchFailure.TooManyRedirects);
    }

    [Fact]
    public async Task Refuses_a_body_over_the_cap_even_without_a_content_length()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            // A stream content reports no length up front.
            Content = new StreamContent(new MemoryStream(new byte[300_000]))
        });

        var fetch = () => Fetcher(handler).GetAsync(new Uri("https://example.com/big.ics"), 200_000);

        (await fetch.Should().ThrowAsync<OutboundFetchException>())
            .Which.Failure.Should().Be(OutboundFetchFailure.TooLarge);
    }

    [Fact]
    public async Task Refuses_a_declared_length_over_the_cap_before_reading()
    {
        var handler = new StubHandler(_ => Ok(new string('x', 2048)));

        var fetch = () => Fetcher(handler).GetAsync(new Uri("https://example.com/big.ics"), 1024);

        (await fetch.Should().ThrowAsync<OutboundFetchException>())
            .Which.Failure.Should().Be(OutboundFetchFailure.TooLarge);
    }

    [Fact]
    public async Task Reports_the_status_of_a_failed_response()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var fetch = () => Fetcher(handler).GetAsync(new Uri("https://example.com/missing.ics"), 1024);

        var failure = (await fetch.Should().ThrowAsync<OutboundFetchException>()).Which;
        failure.Failure.Should().Be(OutboundFetchFailure.HttpError);
        failure.StatusCode.Should().Be(404);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    public async Task The_real_handler_refuses_loopback_before_connecting(string host)
    {
        // Something really is listening, so a refusal can only come from the policy.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var handler = OutboundHttpFetcher.CreateHandler(allowPrivateNetworks: true, TimeSpan.FromSeconds(5));
        var fetch = () => Fetcher(handler, allowPrivateNetworks: true)
            .GetAsync(new Uri($"http://{host}:{port}/"), 1024);

        (await fetch.Should().ThrowAsync<OutboundFetchException>())
            .Which.Failure.Should().Be(OutboundFetchFailure.AddressNotAllowed);
        listener.Pending().Should().BeFalse("no connection may be opened to a refused address");
    }
}
