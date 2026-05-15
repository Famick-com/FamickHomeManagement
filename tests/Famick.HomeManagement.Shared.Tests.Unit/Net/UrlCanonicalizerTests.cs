using Famick.HomeManagement.Shared.Net;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Net;

public class UrlCanonicalizerTests
{
    [Theory]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("https://Example.COM", "https://example.com")]
    [InlineData("HTTPS://example.com", "https://example.com")]
    [InlineData("https://example.com/", "https://example.com")]
    [InlineData("https://example.com:443", "https://example.com")]
    [InlineData("http://example.com:80", "http://example.com")]
    [InlineData("https://example.com:8443", "https://example.com:8443")]
    [InlineData("http://localhost", "http://localhost")]
    [InlineData("http://localhost:5000", "http://localhost:5000")]
    [InlineData("https://app.famick.com", "https://app.famick.com")]
    [InlineData("https://192.168.1.1:8080", "https://192.168.1.1:8080")]
    public void Canonicalize_returns_normalized_form_for_valid_input(string input, string expected)
    {
        UrlCanonicalizer.TryCanonicalize(input, out var canonical, out _).Should().BeTrue();
        canonical.Should().Be(expected);
    }

    [Fact]
    public void Canonicalize_is_idempotent()
    {
        var inputs = new[]
        {
            "https://Example.COM:443",
            "http://example.com:80",
            "https://app.famick.com",
            "https://localhost:8443",
        };

        foreach (var input in inputs)
        {
            UrlCanonicalizer.TryCanonicalize(input, out var first, out _).Should().BeTrue();
            UrlCanonicalizer.TryCanonicalize(first, out var second, out _).Should().BeTrue();
            second.Should().Be(first, because: $"canonicalizing {input} → {first} → should yield {first} again");
        }
    }

    [Fact]
    public void Canonicalize_preserves_IPv6_brackets()
    {
        UrlCanonicalizer.TryCanonicalize("https://[2001:db8::1]:8443", out var canonical, out _).Should().BeTrue();
        canonical.Should().Be("https://[2001:db8::1]:8443");
    }

    [Fact]
    public void Canonicalize_keeps_IDN_in_punycode()
    {
        // bücher.example → xn--bcher-kva.example in punycode. Either passing in the
        // unicode form or the punycode form should produce the punycode output —
        // .NET's Uri.IdnHost handles the conversion.
        UrlCanonicalizer.TryCanonicalize("https://xn--bcher-kva.example", out var fromPunycode, out _).Should().BeTrue();
        fromPunycode.Should().Be("https://xn--bcher-kva.example");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Canonicalize_rejects_empty_input(string? input)
    {
        UrlCanonicalizer.TryCanonicalize(input, out _, out var error).Should().BeFalse();
        error.Should().Be(CanonicalizationError.EmptyInput);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("example.com")]
    [InlineData("//example.com/x")]
    public void Canonicalize_rejects_non_absolute_or_malformed(string input)
    {
        UrlCanonicalizer.TryCanonicalize(input, out _, out var error).Should().BeFalse();
        // .NET's Uri parser is permissive about scheme-shaped tokens — "not-a-url"
        // can parse as scheme=not, host=a-url depending on version. Either rejection
        // bucket (InvalidUri or UnsupportedScheme) is a correct outcome for these
        // inputs; both produce a 400 with a useful reason at the caller.
        error.Should().BeOneOf(CanonicalizationError.InvalidUri, CanonicalizationError.UnsupportedScheme);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    public void Canonicalize_rejects_non_http_schemes(string input)
    {
        UrlCanonicalizer.TryCanonicalize(input, out _, out var error).Should().BeFalse();
        error.Should().Be(CanonicalizationError.UnsupportedScheme);
    }

    [Theory]
    [InlineData("https://user@example.com")]
    [InlineData("https://user:pass@example.com")]
    [InlineData("https://user:pass@example.com:443")]
    public void Canonicalize_rejects_userinfo(string input)
    {
        UrlCanonicalizer.TryCanonicalize(input, out _, out var error).Should().BeFalse();
        error.Should().Be(CanonicalizationError.UserInfoNotAllowed);
    }

    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com/foo/bar")]
    [InlineData("https://example.com:8443/x")]
    public void Canonicalize_rejects_path(string input)
    {
        UrlCanonicalizer.TryCanonicalize(input, out _, out var error).Should().BeFalse();
        error.Should().Be(CanonicalizationError.PathNotAllowed);
    }

    [Theory]
    [InlineData("https://example.com/?x=1")]
    [InlineData("https://example.com?token=abc")]
    public void Canonicalize_rejects_query(string input)
    {
        UrlCanonicalizer.TryCanonicalize(input, out _, out var error).Should().BeFalse();
        // Path "/?..." includes the path "/" so the path check fires first when the
        // Uri parser interprets path-then-query. Both "/?x=1" and "?token=abc" reach
        // the path check first in .NET's Uri model when the path is non-empty.
        // For "https://example.com?token=abc" specifically, the Uri parser treats
        // the path as empty and the query as "?token=abc", so the query check fires.
        // We accept either outcome — both are valid rejections of the same intent.
        error.Should().BeOneOf(CanonicalizationError.QueryNotAllowed, CanonicalizationError.PathNotAllowed);
    }

    [Fact]
    public void Canonicalize_rejects_pure_query()
    {
        UrlCanonicalizer.TryCanonicalize("https://example.com?token=abc", out _, out var error).Should().BeFalse();
        error.Should().Be(CanonicalizationError.QueryNotAllowed);
    }

    [Theory]
    [InlineData("https://example.com/#frag")]
    [InlineData("https://example.com#frag")]
    public void Canonicalize_rejects_fragment(string input)
    {
        UrlCanonicalizer.TryCanonicalize(input, out _, out var error).Should().BeFalse();
        error.Should().BeOneOf(CanonicalizationError.FragmentNotAllowed, CanonicalizationError.PathNotAllowed);
    }

    [Fact]
    public void CanonicalizeOrNull_returns_canonical_on_success()
    {
        UrlCanonicalizer.CanonicalizeOrNull("https://Example.com:443").Should().Be("https://example.com");
    }

    [Fact]
    public void CanonicalizeOrNull_returns_null_on_failure()
    {
        UrlCanonicalizer.CanonicalizeOrNull("https://example.com/path").Should().BeNull();
        UrlCanonicalizer.CanonicalizeOrNull("ftp://example.com").Should().BeNull();
        UrlCanonicalizer.CanonicalizeOrNull(null).Should().BeNull();
    }
}
