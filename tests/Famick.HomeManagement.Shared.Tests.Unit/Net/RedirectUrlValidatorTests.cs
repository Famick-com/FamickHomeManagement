using Famick.HomeManagement.Shared.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Shared.Tests.Unit.Net;

public class RedirectUrlValidatorTests
{
    private static IRedirectUrlValidator Build(params string[] hosts)
    {
        var options = Options.Create(new RedirectUriAllowListOptions { Hosts = hosts.ToList() });
        return new RedirectUrlValidator(options);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/lists")]
    [InlineData("/lists/123")]
    [InlineData("/Settings/billing?tab=payment")]
    [InlineData("/path?with=query&and=fragment#frag")]
    public void Relative_paths_pass_through_unchanged(string input)
    {
        var sut = Build("app.famick.com");

        sut.TryValidate(input, out var safe, out _).Should().BeTrue();
        safe.Should().Be(input);
    }

    [Theory]
    [InlineData("//evil.example/x")]
    [InlineData("//app.famick.com/foo")]
    [InlineData("///still.protocol.relative")]
    public void Protocol_relative_is_rejected(string input)
    {
        var sut = Build("app.famick.com");

        sut.TryValidate(input, out _, out var reason).Should().BeFalse();
        reason.Should().Be(RedirectRejectionReason.ProtocolRelative);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_is_rejected(string? input)
    {
        var sut = Build("app.famick.com");

        sut.TryValidate(input, out _, out var reason).Should().BeFalse();
        reason.Should().Be(RedirectRejectionReason.Empty);
    }

    [Theory]
    [InlineData("https://app.famick.com")]
    [InlineData("https://APP.FAMICK.COM")]
    [InlineData("https://app.famick.com:443")]
    public void Absolute_url_on_allow_list_is_accepted_in_canonical_form(string input)
    {
        var sut = Build("app.famick.com", "auth.famick.com", "proxy.famick.com");

        sut.TryValidate(input, out var safe, out _).Should().BeTrue();
        safe.Should().Be("https://app.famick.com");
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("https://app.famick.com.evil.example")]   // subdomain takeover string
    [InlineData("https://evil.example.app.famick.com")]    // attacker-controlled subdomain pretending to be ours
    [InlineData("https://192.168.1.1")]                    // IPv4 spoof
    [InlineData("https://app-famick.com")]                 // hyphen-vs-dot
    public void Absolute_url_off_allow_list_is_rejected(string input)
    {
        var sut = Build("app.famick.com", "auth.famick.com", "proxy.famick.com");

        sut.TryValidate(input, out _, out var reason).Should().BeFalse();
        reason.Should().Be(RedirectRejectionReason.HostNotAllowed);
    }

    [Theory]
    [InlineData("https://user:pass@app.famick.com")]       // userinfo
    [InlineData("https://app.famick.com/path")]            // path (canonicalizer rejects)
    [InlineData("https://app.famick.com?token=x")]         // query
    [InlineData("https://app.famick.com#frag")]            // fragment
    [InlineData("ftp://app.famick.com")]                   // wrong scheme
    [InlineData("javascript:alert(1)")]                    // wrong scheme
    public void Malformed_or_canonicalizer_rejection_maps_to_Malformed(string input)
    {
        var sut = Build("app.famick.com");

        sut.TryValidate(input, out _, out var reason).Should().BeFalse();
        reason.Should().Be(RedirectRejectionReason.Malformed);
    }

    [Fact]
    public void Encoded_percent_bypass_attempt_is_rejected()
    {
        // %2E is '.' — an attacker might try `app.famick.com%2eevil.example`
        // hoping a naive validator unescapes after host-check. The canonicalizer
        // hands .NET's Uri parser the raw string; .NET interprets %2e as a host
        // character, producing a malformed host rather than splitting on the
        // literal dot. Either way the resulting canonical host is NOT
        // `app.famick.com`, so the allow-list rejects it.
        var sut = Build("app.famick.com");

        sut.TryValidate("https://app.famick.com%2eevil.example", out _, out var reason).Should().BeFalse();
        reason.Should().BeOneOf(RedirectRejectionReason.Malformed, RedirectRejectionReason.HostNotAllowed);
    }

    [Fact]
    public void Empty_allow_list_rejects_all_absolute_urls()
    {
        var sut = Build();  // no hosts

        sut.TryValidate("https://app.famick.com", out _, out var reason).Should().BeFalse();
        reason.Should().Be(RedirectRejectionReason.HostNotAllowed);

        // Relative still works — only the allow-list is empty, not the validator.
        sut.TryValidate("/dashboard", out var safe, out _).Should().BeTrue();
        safe.Should().Be("/dashboard");
    }

    [Fact]
    public void Allow_list_comparison_is_case_insensitive_on_both_sides()
    {
        // Operator typed "APP.FAMICK.COM" in appsettings; user pastes
        // "https://app.famick.com" — both sides should normalize.
        var sut = Build("APP.FAMICK.COM");

        sut.TryValidate("https://app.famick.com", out var safe, out _).Should().BeTrue();
        safe.Should().Be("https://app.famick.com");
    }

    [Fact]
    public void Whitespace_in_allow_list_entries_is_tolerated()
    {
        // Operator pasted " app.famick.com " into config; should still work.
        var sut = Build("  app.famick.com  ");

        sut.TryValidate("https://app.famick.com", out var safe, out _).Should().BeTrue();
        safe.Should().Be("https://app.famick.com");
    }
}
