using System.Net;
using System.Text;
using Famick.HomeManagement.Core.Messaging;
using Famick.HomeManagement.Core.Messaging.Messages;
using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// What a failed session refresh does to the session. Only a 401 or 403 from the refresh endpoint
/// ends it; any other failure keeps the tokens so a later request can refresh again.
/// </summary>
public class HttpApiClientRefreshTests
{
    private const string TokenResponse = "{\"accessToken\":\"new-access\",\"refreshToken\":\"new-refresh\"}";

    private sealed class QueuedHandler : HttpMessageHandler
    {
        public Queue<Func<HttpRequestMessage, HttpResponseMessage>> Responses { get; } = new();
        public List<string> Paths { get; } = new();

        /// <summary>When set, every response waits for it.</summary>
        public TaskCompletionSource? Gate { get; set; }

        public void Enqueue(HttpStatusCode status, string json = "{}")
        {
            Responses.Enqueue(_ => new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            var respond = Responses.Dequeue();
            if (Gate is not null)
            {
                await Gate.Task;
            }
            return respond(request);
        }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("https://test/", "https://test/api/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
        }
    }

    private static (HttpApiClient Client, QueuedHandler Handler, Mock<ITokenStorage> Tokens, Mock<IMessageBus> Bus) BuildClient()
    {
        var handler = new QueuedHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test/") };

        var tokens = new Mock<ITokenStorage>(MockBehavior.Loose);
        tokens.Setup(t => t.GetAccessTokenAsync()).ReturnsAsync("expired-access");
        tokens.Setup(t => t.GetRefreshTokenAsync()).ReturnsAsync("legacy-refresh");

        var bus = new Mock<IMessageBus>(MockBehavior.Loose);

        var client = new HttpApiClient(
            http,
            tokens.Object,
            NullLogger<HttpApiClient>.Instance,
            new TestNavigationManager(),
            bus.Object,
            null);

        return (client, handler, tokens, bus);
    }

    private static void VerifySessionExpiredPublished(Mock<IMessageBus> bus, Times times) =>
        bus.Verify(b => b.Publish(It.IsAny<SessionExpiredMessage>()), times);

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Refresh_refused_by_the_server_ends_the_session(HttpStatusCode refusal)
    {
        var (client, handler, tokens, bus) = BuildClient();
        handler.Enqueue(HttpStatusCode.Unauthorized);
        handler.Enqueue(refusal);

        var result = await client.GetAsync<string>("api/v1/probe");

        result.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
        tokens.Verify(t => t.ClearTokensAsync(), Times.Once);
        VerifySessionExpiredPublished(bus, Times.Once());
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Refresh_failing_with_another_status_keeps_the_session(HttpStatusCode failure)
    {
        var (client, handler, tokens, bus) = BuildClient();
        handler.Enqueue(HttpStatusCode.Unauthorized);
        handler.Enqueue(failure);

        var result = await client.GetAsync<string>("api/v1/probe");

        result.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
        tokens.Verify(t => t.ClearTokensAsync(), Times.Never);
        VerifySessionExpiredPublished(bus, Times.Never());
    }

    [Fact]
    public async Task Refresh_losing_the_connection_keeps_the_session()
    {
        var (client, handler, tokens, bus) = BuildClient();
        handler.Enqueue(HttpStatusCode.Unauthorized);
        handler.Responses.Enqueue(_ => throw new HttpRequestException("connection lost"));

        await client.GetAsync<string>("api/v1/probe");

        tokens.Verify(t => t.ClearTokensAsync(), Times.Never);
        VerifySessionExpiredPublished(bus, Times.Never());
    }

    [Fact]
    public async Task Request_after_a_temporary_failure_refreshes_again_and_succeeds()
    {
        var (client, handler, tokens, _) = BuildClient();
        handler.Enqueue(HttpStatusCode.Unauthorized);
        handler.Enqueue(HttpStatusCode.InternalServerError);
        await client.GetAsync<string>("api/v1/probe");

        handler.Enqueue(HttpStatusCode.Unauthorized);
        handler.Enqueue(HttpStatusCode.OK, TokenResponse);
        handler.Enqueue(HttpStatusCode.OK, "\"ok\"");
        var result = await client.GetAsync<string>("api/v1/probe");

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("ok");
        tokens.Verify(t => t.SetTokensAsync("new-access", "new-refresh"), Times.Once);
        handler.Paths.Count(p => p == "/api/auth/refresh").Should().Be(2);
    }

    [Fact]
    public async Task Refreshes_one_after_another_each_reach_the_server()
    {
        // Each client refreshes in sequence; several run at once so a refresh can finish on one
        // thread while the call that started it is still on another. Not every run gets there.
        const int refreshes = 5_000;
        var clients = Enumerable.Range(0, Math.Max(4, Environment.ProcessorCount)).Select(_ =>
        {
            var (client, handler, _, _) = BuildClient();
            for (var i = 0; i < refreshes; i++)
            {
                handler.Enqueue(HttpStatusCode.OK, TokenResponse);
            }
            return (client, handler);
        }).ToList();

        await Task.WhenAll(clients.Select(c => Task.Run(async () =>
        {
            for (var i = 0; i < refreshes; i++)
            {
                await c.client.RefreshSessionAsync();
            }
        })));

        clients.Should().AllSatisfy(c =>
            c.handler.Paths.Count(p => p == "/api/auth/refresh").Should().Be(refreshes));
    }

    [Fact]
    public async Task Refreshes_asked_for_while_one_runs_share_it()
    {
        var (client, handler, tokens, _) = BuildClient();
        handler.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Enqueue(HttpStatusCode.OK, TokenResponse);

        var waiting = Enumerable.Range(0, 3).Select(_ => client.RefreshSessionAsync()).ToArray();
        handler.Gate.SetResult();

        (await Task.WhenAll(waiting)).Should().AllBeEquivalentTo(true);
        handler.Paths.Count(p => p == "/api/auth/refresh").Should().Be(1);
        tokens.Verify(t => t.SetTokensAsync("new-access", "new-refresh"), Times.Once);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    public async Task RefreshSessionAsync_reports_whether_the_refresh_worked(HttpStatusCode status, bool expected)
    {
        var (client, handler, _, _) = BuildClient();
        handler.Enqueue(status, status == HttpStatusCode.OK ? TokenResponse : "{}");

        var refreshed = await client.RefreshSessionAsync();

        refreshed.Should().Be(expected);
    }
}
