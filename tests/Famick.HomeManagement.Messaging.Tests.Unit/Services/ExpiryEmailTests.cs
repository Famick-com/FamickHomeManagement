using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Messaging.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Famick.HomeManagement.Messaging.Tests.Unit.Services;

/// <summary>
/// Faults visible only in a delivered message: nothing here fails a build, and the send
/// path reports success either way.
/// </summary>
public class ExpiryEmailTests
{
    private readonly StubbleTemplateRenderer _renderer = new(NullLogger<StubbleTemplateRenderer>.Instance);

    /// <summary>
    /// The plain-text alternative is never parsed as HTML, so an entity in it is read
    /// literally. A real message showed "Kroger&#174; Ground Cumin Shaker" because the
    /// default Mustache tag escapes.
    /// </summary>
    [Fact]
    public async Task PlainTextCarriesRealCharactersNotHtmlEntities()
    {
        var text = await _renderer.RenderAsync(
            MessageType.Expiry, TransportChannel.EmailText, DataWith("Kroger® Cumin & Sage"));

        text.Should().Contain("Kroger® Cumin & Sage");
        text.Should().NotContain("&#174;").And.NotContain("&amp;");
    }

    /// <summary>
    /// The HTML alternative is the opposite case: there an ampersand has to be escaped, or
    /// the markup is invalid and clients render it inconsistently.
    /// </summary>
    [Fact]
    public async Task HtmlStillEscapes()
    {
        var html = await _renderer.RenderAsync(
            MessageType.Expiry, TransportChannel.EmailHtml, DataWith("Cheese & Onion"));

        html.Should().Contain("&amp;");
    }

    /// <summary>
    /// Buying two of something makes two stock entries, which listed individually are two
    /// identical lines. One line with a count is shorter and says the same thing.
    /// </summary>
    [Fact]
    public async Task RepeatedEntriesShareOneLineWithACount()
    {
        var data = DataWith("Marshmallows");
        data.ExpiringItems[0].Quantity = 2;

        var text = await _renderer.RenderAsync(MessageType.Expiry, TransportChannel.EmailText, data);

        text.Should().Contain("Marshmallows × 2");
    }

    [Fact]
    public async Task ASingleEntryReadsAsAnOrdinaryLine()
    {
        var text = await _renderer.RenderAsync(
            MessageType.Expiry, TransportChannel.EmailText, DataWith("Marshmallows"));

        text.Should().Contain("Marshmallows");
        text.Should().NotContain("×", "a count on a single item is noise");
    }

    private static ExpiryData DataWith(string productName) => new()
    {
        Title = "1 item(s) expired",
        Summary = "1 expired",
        ExpiredCount = 1,
        ExpiringSoonCount = 0,
        ExpiringItems =
        [
            new ExpiryItemData
            {
                ProductName = productName,
                ExpiryDate = "2026-08-01",
                LocationName = "Pantry",
                IsExpired = true,
                DaysUntilExpiry = -30
            }
        ]
    };
}
