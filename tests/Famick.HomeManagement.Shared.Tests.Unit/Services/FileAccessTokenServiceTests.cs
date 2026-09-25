using System.Text;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// The expiry is part of the signed payload, so whatever it does, the whole token
/// and every URL built from it does too. It used to be a raw timestamp, which meant
/// a fresh URL every second and a cache that could never hit: a stock page of 230
/// images reloaded all of them on every list refresh, 38 times in half an hour
/// (Famick-com/HomeManagement-Cloud#80).
///
/// Snapping the expiry to an hourly grid is what holds the URL still. These cover
/// that it does hold still, and that the rounding never costs a caller validity.
/// </summary>
public class FileAccessTokenServiceTests
{
    private const long BucketSeconds = 3600;
    private const string Secret = "a-test-secret-key-of-at-least-32-characters";

    private readonly FileAccessTokenService _service = new(
        Secret, NullLogger<FileAccessTokenService>.Instance);

    private readonly Guid _resourceId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private string Generate(int expirationMinutes = 15) =>
        _service.GenerateToken("product-image", _resourceId, _tenantId, expirationMinutes);

    /// <summary>Reads the expiry back out of a token, mirroring the service's encoding.</summary>
    private static long ExpirationOf(string token)
    {
        var base64 = token.Replace('-', '+').Replace('_', '/');
        base64 += (base64.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));

        return long.Parse(decoded.Split('|')[3]);
    }

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    [Fact]
    public void The_expiry_lands_on_a_bucket_boundary()
    {
        // The property the whole fix rests on. An expiry on a shared grid is what
        // makes two tokens for the same resource come out byte-identical.
        (ExpirationOf(Generate()) % BucketSeconds).Should().Be(0);
    }

    [Fact]
    public void Two_tokens_for_the_same_resource_are_identical()
    {
        // Which is to say: the URL built from them is cacheable.
        Generate().Should().Be(Generate());
    }

    [Fact]
    public void Rounding_never_leaves_a_token_valid_for_less_than_asked()
    {
        // Rounding up rather than down: shortening a caller's requested lifetime
        // would trade a caching win for broken downloads.
        var before = Now;

        var validity = ExpirationOf(Generate(expirationMinutes: 15)) - before;

        validity.Should().BeGreaterThanOrEqualTo(15 * 60);
    }

    [Fact]
    public void Rounding_costs_at_most_one_extra_bucket()
    {
        // The security side of the trade — worst case is the requested lifetime
        // plus one bucket, and no more.
        var before = Now;

        var validity = ExpirationOf(Generate(expirationMinutes: 15)) - before;

        validity.Should().BeLessThanOrEqualTo(15 * 60 + BucketSeconds);
    }

    [Fact]
    public void A_longer_requested_lifetime_still_pushes_the_expiry_out()
    {
        // Bucketing must not flatten every request onto the same boundary
        // regardless of what was asked for.
        ExpirationOf(Generate(expirationMinutes: 15))
            .Should().BeLessThan(ExpirationOf(Generate(expirationMinutes: 24 * 60)));
    }

    [Fact]
    public void Tokens_still_validate()
    {
        _service.ValidateToken(Generate(), "product-image", _resourceId, _tenantId)
            .Should().BeTrue();
    }

    [Fact]
    public void A_token_minted_for_one_resource_does_not_open_another()
    {
        // Identical-looking tokens are the point; identical-scoped ones are not.
        _service.ValidateToken(Generate(), "product-image", Guid.NewGuid(), _tenantId)
            .Should().BeFalse();
    }

    [Fact]
    public void A_token_minted_for_one_tenant_does_not_open_another()
    {
        _service.ValidateToken(Generate(), "product-image", _resourceId, Guid.NewGuid())
            .Should().BeFalse();
    }

    [Fact]
    public void A_token_minted_for_one_resource_type_does_not_open_another()
    {
        _service.ValidateToken(Generate(), "equipment-document", _resourceId, _tenantId)
            .Should().BeFalse();
    }

    [Fact]
    public void Different_resources_still_get_different_tokens()
    {
        var other = _service.GenerateToken("product-image", Guid.NewGuid(), _tenantId);

        Generate().Should().NotBe(other);
    }
}
