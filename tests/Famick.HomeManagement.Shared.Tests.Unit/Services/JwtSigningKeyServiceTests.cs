using System.Security.Cryptography;
using Famick.HomeManagement.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Phase 1 — covers JwtSigningKeyService's new dual-key support. The contract:
/// new tokens always sign with the current key, the validator accepts both keys
/// during the rotation overlap, and the previous key is silently dropped once
/// its RetiresAt timestamp passes.
/// </summary>
public class JwtSigningKeyServiceTests
{
    private static string GeneratePem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportRSAPrivateKeyPem();
    }

    private static IConfiguration Build(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Loads_single_key_from_legacy_config_shape()
    {
        var pem = GeneratePem();
        var config = Build(new Dictionary<string, string?>
        {
            ["JwtSettings:RsaPrivateKeyPem"] = pem
        });

        var service = new JwtSigningKeyService(config, NullLogger<JwtSigningKeyService>.Instance);

        service.ActiveValidationKeys.Should().HaveCount(1);
        service.ActiveJwks.Should().HaveCount(1);
        service.SecurityKey.KeyId.Should().Be(service.ActiveValidationKeys[0].KeyId);
    }

    [Fact]
    public void Loads_single_key_from_new_CurrentKey_config_shape()
    {
        var pem = GeneratePem();
        var config = Build(new Dictionary<string, string?>
        {
            ["JwtSettings:CurrentKey:RsaPrivateKeyPem"] = pem
        });

        var service = new JwtSigningKeyService(config, NullLogger<JwtSigningKeyService>.Instance);

        service.ActiveValidationKeys.Should().HaveCount(1);
    }

    [Fact]
    public void Loads_dual_keys_when_both_current_and_previous_configured_with_future_RetiresAt()
    {
        var current = GeneratePem();
        var previous = GeneratePem();
        var config = Build(new Dictionary<string, string?>
        {
            ["JwtSettings:CurrentKey:RsaPrivateKeyPem"] = current,
            ["JwtSettings:PreviousKey:RsaPrivateKeyPem"] = previous,
            ["JwtSettings:PreviousKey:RetiresAt"] = DateTimeOffset.UtcNow.AddHours(12).ToString("O")
        });

        var service = new JwtSigningKeyService(config, NullLogger<JwtSigningKeyService>.Instance);

        service.ActiveValidationKeys.Should().HaveCount(2);
        service.ActiveJwks.Should().HaveCount(2);
        // Current key first; previous key second.
        service.SecurityKey.KeyId.Should().Be(service.ActiveValidationKeys[0].KeyId);
        service.ActiveValidationKeys[0].KeyId.Should().NotBe(service.ActiveValidationKeys[1].KeyId);
    }

    [Fact]
    public void Drops_previous_key_when_RetiresAt_has_passed()
    {
        var current = GeneratePem();
        var previous = GeneratePem();
        var config = Build(new Dictionary<string, string?>
        {
            ["JwtSettings:CurrentKey:RsaPrivateKeyPem"] = current,
            ["JwtSettings:PreviousKey:RsaPrivateKeyPem"] = previous,
            ["JwtSettings:PreviousKey:RetiresAt"] = DateTimeOffset.UtcNow.AddHours(-1).ToString("O")
        });

        var service = new JwtSigningKeyService(config, NullLogger<JwtSigningKeyService>.Instance);

        service.ActiveValidationKeys.Should().HaveCount(1,
            "PreviousKey RetiresAt is in the past — previous key must be dropped");
    }

    [Fact]
    public void Drops_previous_key_when_RetiresAt_is_unparseable()
    {
        var current = GeneratePem();
        var previous = GeneratePem();
        var config = Build(new Dictionary<string, string?>
        {
            ["JwtSettings:CurrentKey:RsaPrivateKeyPem"] = current,
            ["JwtSettings:PreviousKey:RsaPrivateKeyPem"] = previous,
            ["JwtSettings:PreviousKey:RetiresAt"] = "not-a-date"
        });

        var service = new JwtSigningKeyService(config, NullLogger<JwtSigningKeyService>.Instance);

        service.ActiveValidationKeys.Should().HaveCount(1,
            "unparseable RetiresAt is treated as 'do not load previous key' — fail closed");
    }

    [Fact]
    public void Includes_previous_key_when_no_RetiresAt_set()
    {
        // No RetiresAt set means "no expiry configured" — the previous key stays
        // active until config is updated. Operators are expected to set RetiresAt;
        // the service does not assume an implicit expiry.
        var current = GeneratePem();
        var previous = GeneratePem();
        var config = Build(new Dictionary<string, string?>
        {
            ["JwtSettings:CurrentKey:RsaPrivateKeyPem"] = current,
            ["JwtSettings:PreviousKey:RsaPrivateKeyPem"] = previous
        });

        var service = new JwtSigningKeyService(config, NullLogger<JwtSigningKeyService>.Instance);

        service.ActiveValidationKeys.Should().HaveCount(2);
    }

    [Fact]
    public void KeyId_is_stable_across_service_instances_with_same_pem()
    {
        // Stable kid is what makes JWKS rotation actually work — verifiers cache
        // by kid, and a token's kid must match a key in the JWKS to validate.
        var pem = GeneratePem();
        var config = Build(new Dictionary<string, string?>
        {
            ["JwtSettings:RsaPrivateKeyPem"] = pem
        });

        var s1 = new JwtSigningKeyService(config, NullLogger<JwtSigningKeyService>.Instance);
        var s2 = new JwtSigningKeyService(config, NullLogger<JwtSigningKeyService>.Instance);

        s1.SecurityKey.KeyId.Should().Be(s2.SecurityKey.KeyId);
    }

    [Fact]
    public void Auto_generates_key_in_dev_when_no_config_present()
    {
        var config = Build(new Dictionary<string, string?>());
        var service = new JwtSigningKeyService(config, NullLogger<JwtSigningKeyService>.Instance);
        service.ActiveValidationKeys.Should().HaveCount(1);
        service.SecurityKey.KeyId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Refuses_to_start_in_Production_when_no_key_configured()
    {
        // An ephemeral auto-generated key looks fine until the container restarts,
        // at which point AuthProxy tunnel handshakes break with public_key_mismatch
        // and every issued JWT fails validation. Refuse to start instead.
        var config = Build(new Dictionary<string, string?>());
        var prodEnv = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production);

        var act = () => new JwtSigningKeyService(config, NullLogger<JwtSigningKeyService>.Instance, prodEnv);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JwtSettings:CurrentKey:RsaPrivateKeyPem*not configured*");
    }

    [Fact]
    public void Allows_configured_key_in_Production()
    {
        var pem = GeneratePem();
        var config = Build(new Dictionary<string, string?>
        {
            ["JwtSettings:CurrentKey:RsaPrivateKeyPem"] = pem
        });
        var prodEnv = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production);

        var service = new JwtSigningKeyService(config, NullLogger<JwtSigningKeyService>.Instance, prodEnv);

        service.ActiveValidationKeys.Should().HaveCount(1);
    }

    [Fact]
    public void Allows_legacy_key_shape_in_Production()
    {
        // Older self-hosted installs use the pre-rotation single-key shape;
        // the production guard must accept it the same as CurrentKey.
        var pem = GeneratePem();
        var config = Build(new Dictionary<string, string?>
        {
            ["JwtSettings:RsaPrivateKeyPem"] = pem
        });
        var prodEnv = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production);

        var service = new JwtSigningKeyService(config, NullLogger<JwtSigningKeyService>.Instance, prodEnv);

        service.ActiveValidationKeys.Should().HaveCount(1);
    }

    [Fact]
    public void Loads_key_from_pem_file_path()
    {
        var pem = GeneratePem();
        var pemFile = Path.Combine(Path.GetTempPath(), $"jwt-test-{Guid.NewGuid():N}.pem");
        File.WriteAllText(pemFile, pem);
        try
        {
            var config = Build(new Dictionary<string, string?>
            {
                ["JwtSettings:CurrentKey:RsaPrivateKeyPemFile"] = pemFile
            });

            var service = new JwtSigningKeyService(config, NullLogger<JwtSigningKeyService>.Instance);
            service.ActiveValidationKeys.Should().HaveCount(1);
        }
        finally
        {
            File.Delete(pemFile);
        }
    }
}
