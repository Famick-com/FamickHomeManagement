using System.Net;
using System.Text;
using Famick.HomeManagement.Core.Messaging;
using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Phase 2.5 chunk 5 — unit tests for HttpApiClient's STEP_UP_REQUIRED handling.
/// Covers the new 403 branch in <c>ExecuteWithRetry</c> / <c>TryStepUpAsync</c>:
///
///   1. 403 STEP_UP_REQUIRED triggers the coordinator and retries on success.
///   2. Coordinator cancel → original 403 surfaces to caller.
///   3. No coordinator registered → 403 surfaces (graceful no-op).
///   4. Other 403 codes do NOT invoke the coordinator (avoids spurious modals).
///   5. Concurrent step-up requests share one coordinator invocation (semaphore).
/// </summary>
public class HttpApiClientStepUpTests
{
    /// <summary>
    /// Programmable HttpMessageHandler — each SendAsync dequeues the next
    /// response factory. Lets us script a 403→200 sequence per test.
    /// </summary>
    private sealed class QueuedHandler : HttpMessageHandler
    {
        public Queue<Func<HttpRequestMessage, HttpResponseMessage>> Responses { get; } = new();
        public List<HttpRequestMessage> Requests { get; } = new();

        public void EnqueueStepUpForbidden()
        {
            Responses.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(
                    "{\"error_message\":\"Step-up authentication required\",\"code\":\"STEP_UP_REQUIRED\"}",
                    Encoding.UTF8,
                    "application/json")
            });
        }

        public void EnqueueForbiddenWithCode(string code)
        {
            Responses.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(
                    $"{{\"error_message\":\"forbidden\",\"code\":\"{code}\"}}",
                    Encoding.UTF8,
                    "application/json")
            });
        }

        public void EnqueueSuccessJson(string json)
        {
            Responses.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (Responses.Count == 0)
            {
                throw new InvalidOperationException(
                    $"QueuedHandler exhausted — unexpected request to {request.RequestUri}");
            }
            var factory = Responses.Dequeue();
            return Task.FromResult(factory(request));
        }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public string? LastNavigatedTo { get; private set; }

        public TestNavigationManager()
        {
            Initialize("https://test/", "https://test/api/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            LastNavigatedTo = uri;
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            LastNavigatedTo = uri;
        }
    }

    private static (HttpApiClient Client, QueuedHandler Handler, Mock<IStepUpReauthCoordinator> Coordinator)
        BuildClient(bool registerCoordinator = true)
    {
        var handler = new QueuedHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://test/") };

        var tokenStorage = new Mock<ITokenStorage>(MockBehavior.Loose);
        tokenStorage.Setup(t => t.GetAccessTokenAsync()).ReturnsAsync("test-token");
        tokenStorage.Setup(t => t.GetRefreshTokenAsync()).ReturnsAsync("refresh-token");

        var messageBus = new Mock<IMessageBus>(MockBehavior.Loose);

        IStepUpReauthCoordinator? coordinatorImpl = null;
        var coordinator = new Mock<IStepUpReauthCoordinator>(MockBehavior.Strict);
        if (registerCoordinator)
        {
            coordinatorImpl = coordinator.Object;
        }

        var client = new HttpApiClient(
            http,
            tokenStorage.Object,
            NullLogger<HttpApiClient>.Instance,
            new TestNavigationManager(),
            messageBus.Object,
            coordinatorImpl);

        return (client, handler, coordinator);
    }

    [Fact]
    public async Task StepUp_required_coordinator_succeeds_retries_and_succeeds()
    {
        var (client, handler, coordinator) = BuildClient();
        handler.EnqueueStepUpForbidden();           // first request: 403
        handler.EnqueueSuccessJson("\"ok\"");       // retry: 200
        coordinator.Setup(c => c.RequestStepUpAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await client.GetAsync<string>("api/v1/probe");

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("ok");
        coordinator.Verify(c => c.RequestStepUpAsync(It.IsAny<CancellationToken>()), Times.Once);
        handler.Requests.Count.Should().Be(2, "the original request should retry after step-up");
    }

    [Fact]
    public async Task StepUp_required_coordinator_cancelled_original_403_surfaces()
    {
        var (client, handler, coordinator) = BuildClient();
        handler.EnqueueStepUpForbidden();
        coordinator.Setup(c => c.RequestStepUpAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await client.GetAsync<string>("api/v1/probe");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be("STEP_UP_REQUIRED");
        coordinator.Verify(c => c.RequestStepUpAsync(It.IsAny<CancellationToken>()), Times.Once);
        handler.Requests.Count.Should().Be(1, "no retry on cancel");
    }

    [Fact]
    public async Task No_coordinator_registered_403_step_up_surfaces_normally()
    {
        var (client, handler, _) = BuildClient(registerCoordinator: false);
        handler.EnqueueStepUpForbidden();

        var result = await client.GetAsync<string>("api/v1/probe");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be("STEP_UP_REQUIRED");
        handler.Requests.Count.Should().Be(1);
    }

    [Fact]
    public async Task Forbidden_with_other_code_does_not_invoke_coordinator()
    {
        var (client, handler, coordinator) = BuildClient();
        handler.EnqueueForbiddenWithCode("MUST_CHANGE_PASSWORD");

        var result = await client.GetAsync<string>("api/v1/probe");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MUST_CHANGE_PASSWORD");
        coordinator.Verify(c => c.RequestStepUpAsync(It.IsAny<CancellationToken>()), Times.Never);
        handler.Requests.Count.Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_step_up_only_one_modal_opens()
    {
        // Two concurrent requests both get 403 STEP_UP_REQUIRED. The
        // _isInStepUp guard means only the first opens the coordinator;
        // the second short-circuits and surfaces its original 403 rather
        // than queueing for the in-flight modal. This is deliberate:
        // showing two stacked modals or silently queueing would be worse
        // UX than letting the caller observe + retry. The first request
        // succeeds after step-up.
        var (client, handler, coordinator) = BuildClient();

        handler.EnqueueStepUpForbidden();           // task A first
        handler.EnqueueStepUpForbidden();           // task B first
        handler.EnqueueSuccessJson("\"retried\""); // task A retry only

        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator
            .Setup(c => c.RequestStepUpAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await release.Task;
                return true;
            });

        var taskA = client.GetAsync<string>("api/v1/probeA");
        // Let task A acquire the semaphore + set _isInStepUp = true
        // before task B's request fires, so the guard reliably trips.
        await Task.Delay(50);
        var taskB = client.GetAsync<string>("api/v1/probeB");
        await Task.Delay(50);

        release.SetResult(true);
        var results = await Task.WhenAll(taskA, taskB);

        // Exactly one modal opened.
        coordinator.Verify(
            c => c.RequestStepUpAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        // First request succeeded after retry; second surfaced its 403.
        results[0].IsSuccess.Should().BeTrue("task A retried after the step-up");
        results[0].Data.Should().Be("retried");
        results[1].IsSuccess.Should().BeFalse("task B saw _isInStepUp=true and short-circuited");
        results[1].ErrorCode.Should().Be("STEP_UP_REQUIRED");
    }
}
