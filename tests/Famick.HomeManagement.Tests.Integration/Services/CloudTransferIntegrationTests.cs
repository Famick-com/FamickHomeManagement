using System.Net;
using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.Transfer;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Web.Data;
using Famick.HomeManagement.Web.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Tests.Integration.Services;

/// <summary>
/// Integration tests for the cloud transfer flow using InMemory databases
/// and a mock HTTP handler simulating cloud API responses.
/// </summary>
public class CloudTransferIntegrationTests : IDisposable
{
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ServiceProvider _serviceProvider;
    private readonly CloudTransferService _service;
    private readonly CloudMockHandler _cloudHandler;

    public CloudTransferIntegrationTests()
    {
        var dbName = Guid.NewGuid().ToString();
        _cloudHandler = new CloudMockHandler();

        var services = new ServiceCollection();
        services.AddDbContext<HomeManagementDbContext>(opt =>
            opt.UseInMemoryDatabase($"hm-{dbName}"));
        services.AddDbContext<TransferDbContext>(opt =>
            opt.UseInMemoryDatabase($"transfer-{dbName}"));
        services.AddLogging();

        // Register IHttpClientFactory that returns our mock handler
        var httpClient = new HttpClient(_cloudHandler) { BaseAddress = new Uri("https://app.famick.com/") };
        var mockFactory = new TestHttpClientFactory(httpClient);
        services.AddSingleton<IHttpClientFactory>(mockFactory);

        _serviceProvider = services.BuildServiceProvider();
        _service = new CloudTransferService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _serviceProvider.GetRequiredService<ILogger<CloudTransferService>>());
    }

    public void Dispose() => _serviceProvider.Dispose();

    #region Full Transfer Flow

    [Fact]
    public async Task FullFlow_EmptyDatabase_CompletesWithZeroItems()
    {
        // Authenticate
        _cloudHandler.SetLoginResponse(new LoginResponse
        {
            AccessToken = "token",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = new UserDto { Id = Guid.NewGuid(), Email = "test@example.com" },
            Tenant = new TenantInfoDto { Id = Guid.NewGuid(), Name = "Test" }
        });

        var authResult = await _service.AuthenticateAsync(new TransferAuthenticateRequest
        {
            Email = "test@example.com",
            Password = "password"
        }, CancellationToken.None);
        authResult.Success.Should().BeTrue();

        // Get summary
        var summary = await _service.GetSummaryAsync(CancellationToken.None);
        summary.Locations.Should().Be(0);
        summary.Products.Should().Be(0);

        // Start transfer
        var startResult = await _service.StartTransferAsync(new TransferStartRequest
        {
            IncludeHistory = false
        }, CancellationToken.None);
        startResult.SessionId.Should().NotBeEmpty();

        // Wait for background task to complete
        await WaitForTransferCompletion(TimeSpan.FromSeconds(10));

        // Verify completion
        var progress = _service.GetCurrentProgress();
        progress.Should().NotBeNull();
        progress!.SessionStatus.Should().Be(TransferSessionStatus.Completed);

        // Verify session persisted
        using var scope = _serviceProvider.CreateScope();
        var transferDb = scope.ServiceProvider.GetRequiredService<TransferDbContext>();
        var session = await transferDb.TransferSessions.FirstAsync();
        session.Status.Should().Be(TransferSessionStatus.Completed);
        session.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task FullFlow_WithLocations_CreatesInCloud()
    {
        // Seed local data
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
            db.Locations.AddRange(
                new Location { Id = Guid.NewGuid(), TenantId = TestTenantId, Name = "Kitchen" },
                new Location { Id = Guid.NewGuid(), TenantId = TestTenantId, Name = "Garage" }
            );
            await db.SaveChangesAsync();
        }

        // Setup cloud mock — empty cloud, returns created IDs
        _cloudHandler.SetLoginResponse(CreateLoginResponse());
        _cloudHandler.SetEmptyListFor("api/v1/locations");
        _cloudHandler.SetCreateResponseFor("api/v1/locations");

        // Authenticate and start
        await AuthenticateAndStart(includeHistory: false);
        await WaitForTransferCompletion(TimeSpan.FromSeconds(10));

        // Verify results
        var results = await _service.GetResultsAsync(CancellationToken.None);
        var locationResult = results.FirstOrDefault(r => r.Category == "Locations");
        locationResult.Should().NotBeNull();
        locationResult!.CreatedCount.Should().Be(2);
        locationResult.SkippedCount.Should().Be(0);

        // Verify item logs persisted
        using var scope2 = _serviceProvider.CreateScope();
        var transferDb = scope2.ServiceProvider.GetRequiredService<TransferDbContext>();
        var logs = await transferDb.TransferItemLogs.Where(l => l.Category == "Locations").ToListAsync();
        logs.Should().HaveCount(2);
        logs.Should().AllSatisfy(l => l.Status.Should().Be(TransferItemStatus.Created));
    }

    [Fact]
    public async Task FullFlow_WithDuplicates_SkipsDuplicateItems()
    {
        var localId = Guid.NewGuid();
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
            db.Locations.AddRange(
                new Location { Id = localId, TenantId = TestTenantId, Name = "Kitchen" },
                new Location { Id = Guid.NewGuid(), TenantId = TestTenantId, Name = "New Room" }
            );
            await db.SaveChangesAsync();
        }

        // Cloud already has "Kitchen"
        _cloudHandler.SetLoginResponse(CreateLoginResponse());
        _cloudHandler.SetListResponseFor("api/v1/locations", new[]
        {
            new { Id = Guid.NewGuid(), Name = "Kitchen" }
        });
        _cloudHandler.SetCreateResponseFor("api/v1/locations");

        await AuthenticateAndStart(includeHistory: false);
        await WaitForTransferCompletion(TimeSpan.FromSeconds(10));

        var results = await _service.GetResultsAsync(CancellationToken.None);
        var locationResult = results.First(r => r.Category == "Locations");
        locationResult.CreatedCount.Should().Be(1);
        locationResult.SkippedCount.Should().Be(1);

        // Verify the skipped item has the right name
        var skippedItem = locationResult.Items.First(i => i.Status == TransferItemStatus.Skipped);
        skippedItem.Name.Should().Be("Kitchen");
    }

    [Fact]
    public async Task FullFlow_WithCloudError_LogsFailedItems()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
            db.Locations.Add(new Location { Id = Guid.NewGuid(), TenantId = TestTenantId, Name = "Bad Location" });
            await db.SaveChangesAsync();
        }

        _cloudHandler.SetLoginResponse(CreateLoginResponse());
        _cloudHandler.SetEmptyListFor("api/v1/locations");
        _cloudHandler.SetErrorResponseFor("api/v1/locations", HttpStatusCode.InternalServerError, "Database error");

        await AuthenticateAndStart(includeHistory: false);
        await WaitForTransferCompletion(TimeSpan.FromSeconds(10));

        var results = await _service.GetResultsAsync(CancellationToken.None);
        var locationResult = results.First(r => r.Category == "Locations");
        locationResult.FailedCount.Should().Be(1);
        locationResult.Items.First().ErrorMessage.Should().Contain("Database error");
    }

    #endregion

    #region History Toggle

    [Fact]
    public async Task FullFlow_WithoutHistory_SkipsChoreLogs()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
            var choreId = Guid.NewGuid();
            db.Chores.Add(new Chore { Id = choreId, TenantId = TestTenantId, Name = "Vacuum" });
            db.ChoresLog.Add(new ChoreLog { Id = Guid.NewGuid(), TenantId = TestTenantId, ChoreId = choreId, TrackedTime = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        _cloudHandler.SetLoginResponse(CreateLoginResponse());
        _cloudHandler.SetDefaultEmptyListResponse();
        _cloudHandler.SetDefaultCreateResponse();

        await AuthenticateAndStart(includeHistory: false);
        await WaitForTransferCompletion(TimeSpan.FromSeconds(10));

        var results = await _service.GetResultsAsync(CancellationToken.None);
        results.Should().NotContain(r => r.Category == "Chore Logs");
    }

    [Fact]
    public async Task FullFlow_WithHistory_IncludesChoreLogs()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
            var choreId = Guid.NewGuid();
            db.Chores.Add(new Chore { Id = choreId, TenantId = TestTenantId, Name = "Vacuum" });
            db.ChoresLog.Add(new ChoreLog { Id = Guid.NewGuid(), TenantId = TestTenantId, ChoreId = choreId, TrackedTime = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        _cloudHandler.SetLoginResponse(CreateLoginResponse());
        _cloudHandler.SetDefaultEmptyListResponse();
        _cloudHandler.SetDefaultCreateResponse();

        await AuthenticateAndStart(includeHistory: true);
        await WaitForTransferCompletion(TimeSpan.FromSeconds(10));

        var results = await _service.GetResultsAsync(CancellationToken.None);
        // Chores should be transferred, and chore logs should appear as a category
        results.Should().Contain(r => r.Category == "Chores");
        results.Should().Contain(r => r.Category == "Chore Logs");
    }

    #endregion

    #region Session Persistence

    [Fact]
    public async Task Transfer_PersistsSessionWithStatus()
    {
        _cloudHandler.SetLoginResponse(CreateLoginResponse());
        _cloudHandler.SetDefaultEmptyListResponse();
        _cloudHandler.SetDefaultCreateResponse();

        await AuthenticateAndStart(includeHistory: false);
        await WaitForTransferCompletion(TimeSpan.FromSeconds(10));

        using var scope = _serviceProvider.CreateScope();
        var transferDb = scope.ServiceProvider.GetRequiredService<TransferDbContext>();
        var sessions = await transferDb.TransferSessions.ToListAsync();
        sessions.Should().HaveCount(1);
        sessions[0].Status.Should().Be(TransferSessionStatus.Completed);
    }

    [Fact]
    public async Task StartNewTransfer_CancelsExistingIncompleteSessions()
    {
        // Create an existing in-progress session
        using (var scope = _serviceProvider.CreateScope())
        {
            var transferDb = scope.ServiceProvider.GetRequiredService<TransferDbContext>();
            transferDb.TransferSessions.Add(new TransferSession
            {
                Id = Guid.NewGuid(),
                CloudEmail = "old@example.com",
                Status = TransferSessionStatus.InProgress,
                StartedAt = DateTime.UtcNow.AddHours(-1)
            });
            await transferDb.SaveChangesAsync();
        }

        _cloudHandler.SetLoginResponse(CreateLoginResponse());
        _cloudHandler.SetDefaultEmptyListResponse();
        _cloudHandler.SetDefaultCreateResponse();

        await AuthenticateAndStart(includeHistory: false);
        await WaitForTransferCompletion(TimeSpan.FromSeconds(10));

        using var scope2 = _serviceProvider.CreateScope();
        var transferDb2 = scope2.ServiceProvider.GetRequiredService<TransferDbContext>();
        var sessions = await transferDb2.TransferSessions.OrderBy(s => s.StartedAt).ToListAsync();
        sessions.Should().HaveCount(2);
        sessions[0].Status.Should().Be(TransferSessionStatus.Cancelled); // Old one cancelled
        sessions[1].Status.Should().Be(TransferSessionStatus.Completed); // New one completed
    }

    #endregion

    #region ID Mapping

    [Fact]
    public async Task Transfer_MapsLocalIdsToCloudIds_ForProducts()
    {
        var localLocationId = Guid.NewGuid();
        var cloudLocationId = Guid.NewGuid();

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
            db.Locations.Add(new Location { Id = localLocationId, TenantId = TestTenantId, Name = "Kitchen" });
            db.Products.Add(new Product
            {
                Id = Guid.NewGuid(),
                TenantId = TestTenantId,
                Name = "Milk",
                LocationId = localLocationId
            });
            await db.SaveChangesAsync();
        }

        _cloudHandler.SetLoginResponse(CreateLoginResponse());
        _cloudHandler.SetEmptyListFor("api/v1/locations");
        _cloudHandler.SetEmptyListFor("api/v1/products");
        // Location creation returns a specific cloud ID
        _cloudHandler.SetCreateResponseForWithId("api/v1/locations", cloudLocationId);
        _cloudHandler.SetCreateResponseFor("api/v1/products");
        _cloudHandler.SetDefaultEmptyListResponse();
        _cloudHandler.SetDefaultCreateResponse();

        await AuthenticateAndStart(includeHistory: false);
        await WaitForTransferCompletion(TimeSpan.FromSeconds(10));

        // Verify location was created with the cloud ID
        using var scope2 = _serviceProvider.CreateScope();
        var transferDb = scope2.ServiceProvider.GetRequiredService<TransferDbContext>();
        var locationLog = await transferDb.TransferItemLogs
            .FirstAsync(l => l.Category == "Locations" && l.Status == TransferItemStatus.Created);
        locationLog.CloudId.Should().Be(cloudLocationId);

        // Verify the product POST included the mapped cloud location ID
        var productPostRequest = _cloudHandler.CapturedRequests
            .FirstOrDefault(r => r.Method == HttpMethod.Post && r.RequestUri!.PathAndQuery.Contains("api/v1/products"));
        productPostRequest.Should().NotBeNull();
        var productBody = await productPostRequest!.Content!.ReadAsStringAsync();
        productBody.Should().Contain(cloudLocationId.ToString());
    }

    #endregion

    #region Cancel Transfer

    [Fact]
    public async Task CancelTransfer_StopsAndPersistsCancelledStatus()
    {
        // Add lots of data to give us time to cancel
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
            for (var i = 0; i < 50; i++)
                db.Locations.Add(new Location { Id = Guid.NewGuid(), TenantId = TestTenantId, Name = $"Location {i}" });
            await db.SaveChangesAsync();
        }

        _cloudHandler.SetLoginResponse(CreateLoginResponse());
        _cloudHandler.SetEmptyListFor("api/v1/locations");
        _cloudHandler.SetCreateResponseFor("api/v1/locations");
        _cloudHandler.SetDefaultEmptyListResponse();
        _cloudHandler.SetDefaultCreateResponse();
        _cloudHandler.AddDelay(TimeSpan.FromMilliseconds(50)); // Slow down cloud responses

        await AuthenticateAndStart(includeHistory: false);

        // Wait briefly then cancel
        await Task.Delay(200);
        _service.CancelTransfer();

        await WaitForTransferCompletion(TimeSpan.FromSeconds(10));

        var progress = _service.GetCurrentProgress();
        progress.Should().NotBeNull();
        progress!.SessionStatus.Should().Be(TransferSessionStatus.Cancelled);

        using var scope2 = _serviceProvider.CreateScope();
        var transferDb = scope2.ServiceProvider.GetRequiredService<TransferDbContext>();
        var session = await transferDb.TransferSessions.FirstAsync();
        session.Status.Should().Be(TransferSessionStatus.Cancelled);
    }

    #endregion

    #region Helpers

    private async Task AuthenticateAndStart(bool includeHistory)
    {
        var authResult = await _service.AuthenticateAsync(new TransferAuthenticateRequest
        {
            Email = "test@example.com",
            Password = "password"
        }, CancellationToken.None);
        authResult.Success.Should().BeTrue();

        await _service.StartTransferAsync(new TransferStartRequest
        {
            IncludeHistory = includeHistory
        }, CancellationToken.None);
    }

    private async Task WaitForTransferCompletion(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var progress = _service.GetCurrentProgress();
            if (progress?.SessionStatus is TransferSessionStatus.Completed
                or TransferSessionStatus.Failed
                or TransferSessionStatus.Cancelled)
            {
                return;
            }
            await Task.Delay(100);
        }

        throw new TimeoutException("Transfer did not complete within timeout");
    }

    private static LoginResponse CreateLoginResponse() => new()
    {
        AccessToken = "token",
        RefreshToken = "refresh",
        ExpiresAt = DateTime.UtcNow.AddHours(1),
        User = new UserDto { Id = Guid.NewGuid(), Email = "test@example.com" },
        Tenant = new TenantInfoDto { Id = Guid.NewGuid(), Name = "Test" }
    };

    /// <summary>
    /// Mock HTTP handler that simulates cloud API responses with configurable
    /// per-endpoint behavior.
    /// </summary>
    private class CloudMockHandler : HttpMessageHandler
    {
        private LoginResponse? _loginResponse;
        private readonly Dictionary<string, object> _listResponses = new();
        private readonly Dictionary<string, Guid?> _createResponses = new();
        private readonly Dictionary<string, (HttpStatusCode code, string message)> _errorResponses = new();
        private bool _defaultEmptyList;
        private bool _defaultCreate;
        private TimeSpan _delay = TimeSpan.Zero;
        public List<HttpRequestMessage> CapturedRequests { get; } = new();

        public void SetLoginResponse(LoginResponse response) => _loginResponse = response;
        public void SetEmptyListFor(string endpoint) => _listResponses[endpoint] = Array.Empty<object>();
        public void SetListResponseFor(string endpoint, object items) => _listResponses[endpoint] = items;
        public void SetCreateResponseFor(string endpoint) => _createResponses[endpoint] = null;
        public void SetCreateResponseForWithId(string endpoint, Guid id) => _createResponses[endpoint] = id;
        public void SetErrorResponseFor(string endpoint, HttpStatusCode code, string message)
            => _errorResponses[endpoint] = (code, message);
        public void SetDefaultEmptyListResponse() => _defaultEmptyList = true;
        public void SetDefaultCreateResponse() => _defaultCreate = true;
        public void AddDelay(TimeSpan delay) => _delay = delay;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            // Clone the request content before it's disposed
            var clonedRequest = new HttpRequestMessage(request.Method, request.RequestUri);
            if (request.Content != null)
            {
                var content = await request.Content.ReadAsStringAsync(ct);
                clonedRequest.Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json");
            }
            CapturedRequests.Add(clonedRequest);

            if (_delay > TimeSpan.Zero)
                await Task.Delay(_delay, ct);

            var path = request.RequestUri!.PathAndQuery.TrimStart('/');

            // Login
            if (path.StartsWith("api/auth/login") && _loginResponse != null)
                return JsonResponse(HttpStatusCode.OK, _loginResponse);

            // Token refresh
            if (path.StartsWith("api/auth/refresh") && _loginResponse != null)
                return JsonResponse(HttpStatusCode.OK, _loginResponse);

            // Check for error responses (POST only)
            if (request.Method == HttpMethod.Post)
            {
                foreach (var (endpoint, error) in _errorResponses)
                {
                    if (path.StartsWith(endpoint))
                        return JsonResponse(error.code, new { error_message = error.message });
                }
            }

            // Check for configured list responses (GET)
            if (request.Method == HttpMethod.Get)
            {
                foreach (var (endpoint, items) in _listResponses)
                {
                    if (path.StartsWith(endpoint))
                        return JsonResponse(HttpStatusCode.OK, items);
                }
                if (_defaultEmptyList)
                    return JsonResponse(HttpStatusCode.OK, Array.Empty<object>());
            }

            // Check for configured create responses (POST)
            if (request.Method == HttpMethod.Post)
            {
                foreach (var (endpoint, id) in _createResponses)
                {
                    if (path.StartsWith(endpoint))
                        return JsonResponse(HttpStatusCode.OK, new { Id = id ?? Guid.NewGuid() });
                }
                if (_defaultCreate)
                    return JsonResponse(HttpStatusCode.OK, new { Id = Guid.NewGuid() });
            }

            // PUT (e.g., home)
            if (request.Method == HttpMethod.Put)
                return JsonResponse(HttpStatusCode.OK, new { Id = Guid.NewGuid() });

            return JsonResponse(HttpStatusCode.NotFound, new { error_message = $"No mock for {request.Method} {path}" });
        }

        private static HttpResponseMessage JsonResponse(HttpStatusCode code, object body)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            return new HttpResponseMessage(code)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public TestHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    #endregion
}
