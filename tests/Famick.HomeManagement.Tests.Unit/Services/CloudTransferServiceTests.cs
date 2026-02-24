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
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Services;

public class CloudTransferServiceTests : IDisposable
{
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ServiceProvider _serviceProvider;
    private readonly CloudTransferService _service;

    public CloudTransferServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContext<HomeManagementDbContext>(opt =>
            opt.UseInMemoryDatabase($"hm-{dbName}"));
        services.AddDbContext<TransferDbContext>(opt =>
            opt.UseInMemoryDatabase($"transfer-{dbName}"));
        services.AddLogging();

        // Register a mock IHttpClientFactory that creates clients with our test handler
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        services.AddSingleton(mockHttpClientFactory.Object);

        _serviceProvider = services.BuildServiceProvider();
        _service = new CloudTransferService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _serviceProvider.GetRequiredService<ILogger<CloudTransferService>>());
    }

    public void Dispose() => _serviceProvider.Dispose();

    #region GetSummaryAsync Tests

    [Fact]
    public async Task GetSummaryAsync_WhenEmpty_ReturnsZeroCounts()
    {
        var summary = await _service.GetSummaryAsync(CancellationToken.None);

        summary.Locations.Should().Be(0);
        summary.Products.Should().Be(0);
        summary.Contacts.Should().Be(0);
        summary.Vehicles.Should().Be(0);
    }

    [Fact]
    public async Task GetSummaryAsync_WithData_ReturnsCorrectCounts()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
            db.Locations.Add(new Location { Id = Guid.NewGuid(), TenantId = TestTenantId, Name = "Kitchen" });
            db.Locations.Add(new Location { Id = Guid.NewGuid(), TenantId = TestTenantId, Name = "Garage" });
            db.Products.Add(new Product { Id = Guid.NewGuid(), TenantId = TestTenantId, Name = "Milk" });
            await db.SaveChangesAsync();
        }

        var summary = await _service.GetSummaryAsync(CancellationToken.None);

        summary.Locations.Should().Be(2);
        summary.Products.Should().Be(1);
    }

    #endregion

    #region GetSessionInfoAsync Tests

    [Fact]
    public async Task GetSessionInfoAsync_WhenNoSession_ReturnsNoIncomplete()
    {
        var info = await _service.GetSessionInfoAsync(CancellationToken.None);

        info.HasIncompleteSession.Should().BeFalse();
    }

    [Fact]
    public async Task GetSessionInfoAsync_WhenIncompleteSession_ReturnsSessionInfo()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var transferDb = scope.ServiceProvider.GetRequiredService<TransferDbContext>();
            transferDb.TransferSessions.Add(new TransferSession
            {
                Id = Guid.NewGuid(),
                CloudEmail = "test@example.com",
                Status = TransferSessionStatus.InProgress,
                CurrentCategory = "Products",
                StartedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await transferDb.SaveChangesAsync();
        }

        var info = await _service.GetSessionInfoAsync(CancellationToken.None);

        info.HasIncompleteSession.Should().BeTrue();
        info.CurrentCategory.Should().Be("Products");
    }

    [Fact]
    public async Task GetSessionInfoAsync_WhenCompletedSession_ReturnsNoIncomplete()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var transferDb = scope.ServiceProvider.GetRequiredService<TransferDbContext>();
            transferDb.TransferSessions.Add(new TransferSession
            {
                Id = Guid.NewGuid(),
                CloudEmail = "test@example.com",
                Status = TransferSessionStatus.Completed,
                StartedAt = DateTime.UtcNow.AddMinutes(-10),
                CompletedAt = DateTime.UtcNow
            });
            await transferDb.SaveChangesAsync();
        }

        var info = await _service.GetSessionInfoAsync(CancellationToken.None);

        info.HasIncompleteSession.Should().BeFalse();
    }

    #endregion

    #region GetCurrentProgress Tests

    [Fact]
    public void GetCurrentProgress_WhenNoTransfer_ReturnsNull()
    {
        var progress = _service.GetCurrentProgress();

        progress.Should().BeNull();
    }

    #endregion

    #region StartTransferAsync Tests

    [Fact]
    public async Task StartTransferAsync_WhenNotAuthenticated_Throws()
    {
        var request = new TransferStartRequest { IncludeHistory = false };

        var act = async () => await _service.StartTransferAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*authenticate*");
    }

    #endregion

    #region GetResultsAsync Tests

    [Fact]
    public async Task GetResultsAsync_WhenNoSession_ReturnsEmptyList()
    {
        var results = await _service.GetResultsAsync(CancellationToken.None);

        results.Should().BeEmpty();
    }

    #endregion

    #region CancelTransfer Tests

    [Fact]
    public void CancelTransfer_WhenNoActiveTransfer_DoesNotThrow()
    {
        var act = () => _service.CancelTransfer();

        act.Should().NotThrow();
    }

    #endregion

    #region AuthenticateAsync Tests

    [Fact]
    public async Task AuthenticateAsync_Registration_RequiresFirstAndLastName()
    {
        // Setup mock HTTP client factory for this test
        SetupMockHttpClientFactory(HttpStatusCode.OK, new RegisterResponse
        {
            UserId = Guid.NewGuid(),
            Email = "test@example.com",
            AccessToken = "token",
            RefreshToken = "refresh"
        });

        var request = new TransferAuthenticateRequest
        {
            Email = "test@example.com",
            Password = "password",
            IsRegistration = true,
            FirstName = "",
            LastName = ""
        };

        var result = await _service.AuthenticateAsync(request, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("First and last name");
    }

    [Fact]
    public async Task AuthenticateAsync_Login_WhenCloudReturnsError_ReturnsFailure()
    {
        SetupMockHttpClientFactory(HttpStatusCode.Unauthorized,
            new { error_message = "Invalid credentials" });

        var request = new TransferAuthenticateRequest
        {
            Email = "test@example.com",
            Password = "wrong",
            IsRegistration = false
        };

        var result = await _service.AuthenticateAsync(request, CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task AuthenticateAsync_Login_WhenSuccessful_ReturnsSuccess()
    {
        SetupMockHttpClientFactory(HttpStatusCode.OK, new LoginResponse
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = new UserDto { Id = Guid.NewGuid(), Email = "test@example.com" },
            Tenant = new TenantInfoDto { Id = Guid.NewGuid(), Name = "Test" }
        });

        var request = new TransferAuthenticateRequest
        {
            Email = "test@example.com",
            Password = "correct-password",
            IsRegistration = false
        };

        var result = await _service.AuthenticateAsync(request, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CloudUserEmail.Should().Be("test@example.com");
    }

    #endregion

    #region Helpers

    private void SetupMockHttpClientFactory(HttpStatusCode statusCode, object responseBody)
    {
        using var scope = _serviceProvider.CreateScope();
        var mockFactory = Mock.Get(scope.ServiceProvider.GetRequiredService<IHttpClientFactory>());

        var handler = new TestHttpHandler(statusCode, responseBody);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://app.famick.com/") };

        mockFactory.Setup(f => f.CreateClient("CloudApi")).Returns(httpClient);
    }

    private class TestHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object _responseBody;

        public TestHttpHandler(HttpStatusCode statusCode, object responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(_statusCode);
            var json = JsonSerializer.Serialize(_responseBody, JsonOptions);
            response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        }
    }

    #endregion
}
