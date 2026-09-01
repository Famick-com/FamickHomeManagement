using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Messaging.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Famick.HomeManagement.Messaging.Tests.Unit.Services;

/// <summary>
/// A colour and the surface behind it have to move together.
/// </summary>
/// <remarks>
/// The shared layout lightens its text under <c>prefers-color-scheme: dark</c>. It once did
/// that without darkening the background, on the assumption that a client asking for dark
/// mode would paint a dark background itself. Plenty do not — they honour the media query
/// and still render on white — which left pale grey text on white and an unreadable email
/// that nothing in the send path could detect.
/// </remarks>
public class EmailLayoutThemeTests
{
    private readonly StubbleTemplateRenderer _renderer = new(NullLogger<StubbleTemplateRenderer>.Instance);

    [Fact]
    public async Task TheLightGroundIsStatedRatherThanInherited()
    {
        var html = await RenderAnyEmailAsync();

        // Without this the message sits on whatever the client happens to paint, so the
        // light theme is a guess rather than a choice.
        html.Should().Contain("background-color: #ffffff",
            "the body needs an explicit background so light mode does not depend on the client");
    }

    [Fact]
    public async Task TheDarkGroundIsDarkenedWhereverTheTextIsLightened()
    {
        var html = await RenderAnyEmailAsync();

        var darkBlock = ExtractDarkModeBlock(html);

        darkBlock.Should().NotBeNullOrWhiteSpace("the layout should still carry a dark-mode block");

        var bodyRule = darkBlock!
            .Split('\n')
            .FirstOrDefault(line => line.Contains(".body-text"));

        bodyRule.Should().NotBeNull();
        bodyRule!.Should().Contain("color:");
        bodyRule.Should().Contain("background-color:",
            "lightening the text without darkening its ground produces pale grey on white");
    }

    private async Task<string> RenderAnyEmailAsync()
    {
        // Any type renders through the same shared layout; the deletion notice is simply
        // one that exercises it.
        return await _renderer.RenderAsync(
            MessageType.AccountDeletionScheduled,
            TransportChannel.EmailHtml,
            new AccountDeletionData
            {
                UserName = "Mike",
                IsHousehold = true,
                HouseholdName = "The Therien Family",
                RequestedOn = "30 August 2026",
                DeletedOn = "29 September 2026"
            });
    }

    /// <summary>
    /// Returns everything from the dark-mode media query to the end of the stylesheet.
    /// </summary>
    /// <remarks>
    /// Deliberately not a CSS parser. The block is the last thing in the layout's style
    /// element, so taking the remainder of it is enough to find the rules and cannot
    /// silently match a rule from the light theme.
    /// </remarks>
    private static string? ExtractDarkModeBlock(string html)
    {
        const string marker = "prefers-color-scheme: dark";

        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;

        var end = html.IndexOf("</style>", start, StringComparison.OrdinalIgnoreCase);

        return end < 0 ? html[start..] : html[start..end];
    }
}
