using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Web.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Services;

public class CloudApiClientTests
{
    private readonly Mock<ILogger<CloudApiClient>> _mockLogger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public CloudApiClientTests()
    {
        _mockLogger = new Mock<ILogger<CloudApiClient>>();
    }

    private static CloudApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://app.famick.com/") };
        return new CloudApiClient(httpClient, Mock.Of<ILogger<CloudApiClient>>());
    }

    #region Login Tests

    [Fact]
    public async Task LoginAsync_WhenSuccessful_StoresTokensAndReturnsOk()
    {
        var loginResponse = new LoginResponse
        {
            AccessToken = "access-token-123",
            RefreshToken = "refresh-token-456",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = new UserDto { Id = Guid.NewGuid(), Email = "test@example.com", FirstName = "Test", LastName = "User" },
            Tenant = new TenantInfoDto { Id = Guid.NewGuid(), Name = "Test Tenant" }
        };
        var handler = new MockHttpHandler(HttpStatusCode.OK, loginResponse);
        var client = CreateClient(handler);

        var result = await client.LoginAsync("test@example.com", "password123");

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("access-token-123");
        client.RefreshToken.Should().Be("refresh-token-456");
    }

    [Fact]
    public async Task LoginAsync_WhenUnauthorized_ReturnsFailWithError()
    {
        var handler = new MockHttpHandler(HttpStatusCode.Unauthorized,
            new { error_message = "Invalid credentials" });
        var client = CreateClient(handler);

        var result = await client.LoginAsync("test@example.com", "wrong-password");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid credentials");
        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task LoginAsync_WhenConnectionFails_ReturnsFailWithMessage()
    {
        var handler = new MockHttpHandler(new HttpRequestException("Connection refused"));
        var client = CreateClient(handler);

        var result = await client.LoginAsync("test@example.com", "password");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Connection failed");
    }

    #endregion

    #region Register Tests

    [Fact]
    public async Task RegisterAsync_WhenSuccessful_StoresTokens()
    {
        var registerResponse = new RegisterResponse
        {
            UserId = Guid.NewGuid(),
            Email = "new@example.com",
            AccessToken = "new-access-token",
            RefreshToken = "new-refresh-token"
        };
        var handler = new MockHttpHandler(HttpStatusCode.OK, registerResponse);
        var client = CreateClient(handler);

        var result = await client.RegisterAsync("new@example.com", "password", "New", "User");

        result.IsSuccess.Should().BeTrue();
        client.RefreshToken.Should().Be("new-refresh-token");
    }

    [Fact]
    public async Task RegisterAsync_WhenConflict_ReturnsError()
    {
        var handler = new MockHttpHandler(HttpStatusCode.Conflict,
            new { error_message = "Email already exists" });
        var client = CreateClient(handler);

        var result = await client.RegisterAsync("existing@example.com", "password", "Test", "User");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Email already exists");
    }

    #endregion

    #region GET with Token Refresh Tests

    [Fact]
    public async Task GetAsync_WhenAuthenticated_SendsBearerToken()
    {
        // Login first to get tokens
        var loginResponse = new LoginResponse
        {
            AccessToken = "my-access-token",
            RefreshToken = "my-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = new UserDto { Id = Guid.NewGuid(), Email = "test@example.com" },
            Tenant = new TenantInfoDto { Id = Guid.NewGuid(), Name = "Test" }
        };
        var handler = new SequentialMockHandler(new[]
        {
            (HttpStatusCode.OK, (object)loginResponse),
            (HttpStatusCode.OK, (object)new[] { new { Id = Guid.NewGuid(), Name = "Location 1" } })
        });
        var client = CreateClient(handler);
        await client.LoginAsync("test@example.com", "password");

        var result = await client.GetAsync<List<object>>("api/v1/locations");

        result.IsSuccess.Should().BeTrue();
        handler.Requests[1].Headers.Authorization.Should().NotBeNull();
        handler.Requests[1].Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Requests[1].Headers.Authorization!.Parameter.Should().Be("my-access-token");
    }

    [Fact]
    public async Task GetAsync_When401_RefreshesTokenAndRetries()
    {
        var loginResponse = new LoginResponse
        {
            AccessToken = "old-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = new UserDto { Id = Guid.NewGuid(), Email = "test@example.com" },
            Tenant = new TenantInfoDto { Id = Guid.NewGuid(), Name = "Test" }
        };
        var refreshResponse = new LoginResponse
        {
            AccessToken = "new-token",
            RefreshToken = "new-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = new UserDto { Id = Guid.NewGuid(), Email = "test@example.com" },
            Tenant = new TenantInfoDto { Id = Guid.NewGuid(), Name = "Test" }
        };
        var handler = new SequentialMockHandler(new[]
        {
            (HttpStatusCode.OK, (object)loginResponse),       // Login
            (HttpStatusCode.Unauthorized, (object)""),         // First GET fails
            (HttpStatusCode.OK, (object)refreshResponse),     // Token refresh
            (HttpStatusCode.OK, (object)new[] { new { Id = Guid.NewGuid(), Name = "Item" } })  // Retry GET
        });
        var client = CreateClient(handler);
        await client.LoginAsync("test@example.com", "password");

        var result = await client.GetAsync<List<object>>("api/v1/locations");

        result.IsSuccess.Should().BeTrue();
        handler.Requests.Should().HaveCount(4); // login + failed GET + refresh + retry GET
    }

    [Fact]
    public async Task GetAsync_When401AndRefreshFails_ReturnsFailure()
    {
        var loginResponse = new LoginResponse
        {
            AccessToken = "old-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = new UserDto { Id = Guid.NewGuid(), Email = "test@example.com" },
            Tenant = new TenantInfoDto { Id = Guid.NewGuid(), Name = "Test" }
        };
        var handler = new SequentialMockHandler(new[]
        {
            (HttpStatusCode.OK, (object)loginResponse),        // Login
            (HttpStatusCode.Unauthorized, (object)""),          // First GET fails
            (HttpStatusCode.Unauthorized, (object)""),          // Token refresh also fails
            (HttpStatusCode.Unauthorized, (object)"")           // Retry also fails (but won't happen)
        });
        var client = CreateClient(handler);
        await client.LoginAsync("test@example.com", "password");

        var result = await client.GetAsync<List<object>>("api/v1/locations");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    #endregion

    #region POST Tests

    [Fact]
    public async Task PostAsync_WithBody_ReturnsTypedResponse()
    {
        var createdResponse = new { Id = Guid.NewGuid() };
        var loginResponse = new LoginResponse
        {
            AccessToken = "token",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = new UserDto { Id = Guid.NewGuid(), Email = "test@example.com" },
            Tenant = new TenantInfoDto { Id = Guid.NewGuid(), Name = "Test" }
        };
        var handler = new SequentialMockHandler(new[]
        {
            (HttpStatusCode.OK, (object)loginResponse),
            (HttpStatusCode.OK, (object)createdResponse)
        });
        var client = CreateClient(handler);
        await client.LoginAsync("test@example.com", "password");

        var result = await client.PostAsync<object, object>("api/v1/locations", new { Name = "Kitchen" });

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PostAsync_WhenServerError_ReturnsFailure()
    {
        var loginResponse = new LoginResponse
        {
            AccessToken = "token",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = new UserDto { Id = Guid.NewGuid(), Email = "test@example.com" },
            Tenant = new TenantInfoDto { Id = Guid.NewGuid(), Name = "Test" }
        };
        var handler = new SequentialMockHandler(new[]
        {
            (HttpStatusCode.OK, (object)loginResponse),
            (HttpStatusCode.InternalServerError, (object)new { error_message = "Database error" })
        });
        var client = CreateClient(handler);
        await client.LoginAsync("test@example.com", "password");

        var result = await client.PostAsync<object, object>("api/v1/locations", new { Name = "Test" });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
    }

    #endregion

    #region RestoreAuth Tests

    [Fact]
    public async Task RestoreAuthAsync_WhenRefreshSucceeds_ReturnsTrue()
    {
        var refreshResponse = new LoginResponse
        {
            AccessToken = "restored-token",
            RefreshToken = "new-refresh",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = new UserDto { Id = Guid.NewGuid(), Email = "test@example.com" },
            Tenant = new TenantInfoDto { Id = Guid.NewGuid(), Name = "Test" }
        };
        var handler = new MockHttpHandler(HttpStatusCode.OK, refreshResponse);
        var client = CreateClient(handler);

        var result = await client.RestoreAuthAsync("stored-refresh-token");

        result.Should().BeTrue();
        client.RefreshToken.Should().Be("new-refresh");
    }

    [Fact]
    public async Task RestoreAuthAsync_WhenRefreshFails_ReturnsFalse()
    {
        var handler = new MockHttpHandler(HttpStatusCode.Unauthorized, "");
        var client = CreateClient(handler);

        var result = await client.RestoreAuthAsync("expired-refresh-token");

        result.Should().BeFalse();
    }

    #endregion

    #region Error Message Parsing

    [Fact]
    public async Task ErrorMessage_ParsesJsonErrorMessage()
    {
        var handler = new MockHttpHandler(HttpStatusCode.BadRequest,
            new { error_message = "Name is required" });
        var client = CreateClient(handler);

        var result = await client.LoginAsync("", "");

        result.ErrorMessage.Should().Be("Name is required");
    }

    [Fact]
    public async Task ErrorMessage_FallsBackToReasonPhrase()
    {
        var handler = new MockHttpHandler(HttpStatusCode.BadRequest, reasonPhrase: "Bad Request");
        var client = CreateClient(handler);

        var result = await client.LoginAsync("", "");

        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Mock HTTP Handlers

    /// <summary>
    /// Simple mock that returns the same response for every request.
    /// </summary>
    private class MockHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object? _responseBody;
        private readonly Exception? _exception;
        private readonly string? _reasonPhrase;

        public MockHttpHandler(HttpStatusCode statusCode, object? responseBody = null, string? reasonPhrase = null)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
            _reasonPhrase = reasonPhrase;
        }

        public MockHttpHandler(Exception exception)
        {
            _exception = exception;
            _statusCode = HttpStatusCode.InternalServerError;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (_exception != null)
                throw _exception;

            var response = new HttpResponseMessage(_statusCode);
            if (_reasonPhrase != null)
                response.ReasonPhrase = _reasonPhrase;

            if (_responseBody != null)
            {
                var json = _responseBody is string s ? s : JsonSerializer.Serialize(_responseBody, JsonOptions);
                response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Returns different responses for each sequential request.
    /// </summary>
    private class SequentialMockHandler : HttpMessageHandler
    {
        private readonly (HttpStatusCode statusCode, object body)[] _responses;
        private int _callIndex;
        public List<HttpRequestMessage> Requests { get; } = new();

        public SequentialMockHandler((HttpStatusCode, object)[] responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);

            var index = _callIndex < _responses.Length ? _callIndex : _responses.Length - 1;
            _callIndex++;

            var (statusCode, body) = _responses[index];
            var response = new HttpResponseMessage(statusCode);

            var json = body is string s ? s : JsonSerializer.Serialize(body, JsonOptions);
            response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            return Task.FromResult(response);
        }
    }

    #endregion
}
