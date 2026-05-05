using System.Security.Cryptography;
using Famick.HomeManagement.Core.Services;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// Phase 1 — covers JwksController's multi-key publication and Cache-Control header.
/// Downstream verifiers (mobile, future auth.famick.com, future agent) cache this
/// endpoint and depend on both the active key set and the cache TTL being correct.
/// </summary>
public class JwksControllerTests
{
    private static string GeneratePem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportRSAPrivateKeyPem();
    }

    private static JwksController BuildController(IDictionary<string, string?> jwtConfig, out HttpContext httpContext)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(jwtConfig).Build();
        var signingKeyService = new JwtSigningKeyService(config, NullLogger<JwtSigningKeyService>.Instance);

        httpContext = new DefaultHttpContext();
        var controller = new JwksController(signingKeyService)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
        return controller;
    }

    [Fact]
    public void GetJwks_returns_single_key_when_no_previous_key_configured()
    {
        var controller = BuildController(new Dictionary<string, string?>
        {
            ["JwtSettings:CurrentKey:RsaPrivateKeyPem"] = GeneratePem()
        }, out _);

        var result = controller.GetJwks() as OkObjectResult;
        result.Should().NotBeNull();

        // Reflection-friendly extraction of the anonymous "keys" array length.
        var jwks = result!.Value!;
        var keysProperty = jwks.GetType().GetProperty("keys")!;
        var keys = (Array)keysProperty.GetValue(jwks)!;
        keys.Length.Should().Be(1);
    }

    [Fact]
    public void GetJwks_returns_two_keys_during_rotation_overlap()
    {
        var controller = BuildController(new Dictionary<string, string?>
        {
            ["JwtSettings:CurrentKey:RsaPrivateKeyPem"] = GeneratePem(),
            ["JwtSettings:PreviousKey:RsaPrivateKeyPem"] = GeneratePem(),
            ["JwtSettings:PreviousKey:RetiresAt"] = DateTimeOffset.UtcNow.AddHours(12).ToString("O")
        }, out _);

        var result = controller.GetJwks() as OkObjectResult;
        var jwks = result!.Value!;
        var keys = (Array)jwks.GetType().GetProperty("keys")!.GetValue(jwks)!;
        keys.Length.Should().Be(2,
            "during rotation overlap, both current and previous keys must be published");
    }

    [Fact]
    public void GetJwks_returns_one_key_after_previous_RetiresAt_passes()
    {
        var controller = BuildController(new Dictionary<string, string?>
        {
            ["JwtSettings:CurrentKey:RsaPrivateKeyPem"] = GeneratePem(),
            ["JwtSettings:PreviousKey:RsaPrivateKeyPem"] = GeneratePem(),
            ["JwtSettings:PreviousKey:RetiresAt"] = DateTimeOffset.UtcNow.AddHours(-1).ToString("O")
        }, out _);

        var result = controller.GetJwks() as OkObjectResult;
        var jwks = result!.Value!;
        var keys = (Array)jwks.GetType().GetProperty("keys")!.GetValue(jwks)!;
        keys.Length.Should().Be(1, "expired previous key must be dropped from the JWKS");
    }

    [Fact]
    public void GetJwks_sets_5_minute_Cache_Control_header()
    {
        var controller = BuildController(new Dictionary<string, string?>
        {
            ["JwtSettings:CurrentKey:RsaPrivateKeyPem"] = GeneratePem()
        }, out var httpContext);

        controller.GetJwks();

        var cacheControl = httpContext.Response.Headers.CacheControl.ToString();
        cacheControl.Should().Contain("public");
        cacheControl.Should().Contain("max-age=300",
            "Phase 1 publishes a 5-minute cache hint so verifiers don't hammer the endpoint");
    }
}
