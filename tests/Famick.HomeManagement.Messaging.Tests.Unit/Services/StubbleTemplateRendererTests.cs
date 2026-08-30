using System.Linq;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Messaging.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Famick.HomeManagement.Messaging.Tests.Unit.Services;

public class StubbleTemplateRendererTests
{
    private readonly StubbleTemplateRenderer _renderer;

    public StubbleTemplateRendererTests()
    {
        _renderer = new StubbleTemplateRenderer(NullLogger<StubbleTemplateRenderer>.Instance);
    }

    [Fact]
    public void ValidateAllTemplatesExist_AllTemplatesPresent_DoesNotThrow()
    {
        var act = () => _renderer.ValidateAllTemplatesExist();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task RenderAsync_EmailVerification_RendersHouseholdName()
    {
        var data = new EmailVerificationData
        {
            HouseholdName = "The Smiths",
            VerificationLink = "famick://verify?token=abc123",
            Token = "abc123"
        };

        var result = await _renderer.RenderAsync(MessageType.EmailVerification, TransportChannel.EmailHtml, data);

        result.Should().Contain("The Smiths");
        result.Should().Contain("famick://verify?token=abc123");
        result.Should().Contain("abc123");
        // Should be wrapped in the layout
        result.Should().Contain("<!DOCTYPE html>");
    }

    [Fact]
    public async Task RenderAsync_EmailText_DoesNotWrapInLayout()
    {
        var data = new PasswordResetData
        {
            UserName = "John",
            ResetLink = "https://example.com/reset?token=xyz"
        };

        var result = await _renderer.RenderAsync(MessageType.PasswordReset, TransportChannel.EmailText, data);

        result.Should().Contain("John");
        result.Should().Contain("https://example.com/reset?token=xyz");
        result.Should().NotContain("<!DOCTYPE html>");
    }

    [Fact]
    public async Task RenderAsync_ExpiryPush_RendersTitle()
    {
        var data = new ExpiryData
        {
            Title = "3 items expiring soon",
            Summary = "2 expiring, 1 expired",
            ExpiredCount = 1,
            ExpiringSoonCount = 2
        };

        var result = await _renderer.RenderAsync(MessageType.Expiry, TransportChannel.Push, data);

        result.Should().Contain("3 items expiring soon");
    }

    [Fact]
    public async Task RenderAsync_WithLayoutContext_IncludesComplianceFooter()
    {
        var data = new LowStockData
        {
            Title = "2 items low",
            Summary = "2 below minimum",
            ItemCount = 2,
            LowStockItems = [new() { Name = "Milk", CurrentStock = 1, MinStockAmount = 3 }]
        };

        var layoutContext = new Dictionary<string, object>
        {
            {
                "complianceFooter", new Dictionary<string, object>
                {
                    { "CompanyName", "Test Co" },
                    { "UnsubscribeUrl", "https://example.com/unsub" },
                    { "PhysicalAddress", "123 Main St" }
                }
            }
        };

        var result = await _renderer.RenderAsync(
            MessageType.LowStock, TransportChannel.EmailHtml, data, layoutContext);

        result.Should().Contain("Test Co");
        result.Should().Contain("https://example.com/unsub");
        result.Should().Contain("123 Main St");
    }

    [Fact]
    public async Task RenderAsync_TransactionalWithoutLayoutContext_NoComplianceFooter()
    {
        var data = new WelcomeData
        {
            UserName = "Jane",
            Email = "jane@example.com",
            TemporaryPassword = "temp123",
            LoginUrl = "https://app.example.com"
        };

        var result = await _renderer.RenderAsync(
            MessageType.Welcome, TransportChannel.EmailHtml, data, layoutContext: null);

        result.Should().Contain("Jane");
        // No compliance footer section rendered (the CSS class exists in the style block, but no footer div)
        result.Should().NotContain("Unsubscribe");
    }

    [Fact]
    public async Task RenderSubjectAsync_RendersTemplateVariables()
    {
        var data = new ExpiryData { Title = "5 items expiring" };

        var result = await _renderer.RenderSubjectAsync(MessageType.Expiry, data);

        result.Should().Contain("5 items expiring");
    }

    [Fact]
    public async Task RenderAsync_CalendarReminder_AllChannelsRender()
    {
        var data = new CalendarReminderData
        {
            EventTitle = "Team Standup",
            StartTime = "09:00 UTC",
            StartDate = "2026-04-07"
        };

        var emailHtml = await _renderer.RenderAsync(MessageType.CalendarReminder, TransportChannel.EmailHtml, data);
        var emailText = await _renderer.RenderAsync(MessageType.CalendarReminder, TransportChannel.EmailText, data);
        var push = await _renderer.RenderAsync(MessageType.CalendarReminder, TransportChannel.Push, data);
        var inApp = await _renderer.RenderAsync(MessageType.CalendarReminder, TransportChannel.InApp, data);
        var sms = await _renderer.RenderAsync(MessageType.CalendarReminder, TransportChannel.Sms, data);

        emailHtml.Should().Contain("Team Standup");
        emailText.Should().Contain("Team Standup");
        push.Should().Contain("09:00 UTC");
        inApp.Should().Contain("09:00 UTC");
        sms.Should().Contain("Team Standup");
    }

    [Fact]
    public void HasTemplate_ExistingTemplate_ReturnsTrue()
    {
        _renderer.HasTemplate(MessageType.Expiry, TransportChannel.EmailHtml).Should().BeTrue();
    }

    [Fact]
    public void HasTemplate_NonExistentTemplate_ReturnsFalse()
    {
        // Transactional types don't have push templates
        _renderer.HasTemplate(MessageType.Welcome, TransportChannel.Push).Should().BeFalse();
    }

    [Fact]
    public async Task RenderAsync_MissingTemplate_ThrowsInvalidOperationException()
    {
        var data = new WelcomeData { UserName = "Test" };

        var act = () => _renderer.RenderAsync(MessageType.Welcome, TransportChannel.Push, data);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Template not found*");
    }

    [Fact]
    public async Task RenderAsync_TaskSummary_RendersConditionalSections()
    {
        var data = new TaskSummaryData
        {
            Title = "3 pending",
            TotalTasks = 3,
            IncompleteTodos = 2,
            OverdueChores = 1,
            OverdueMaintenance = 0
        };

        var result = await _renderer.RenderAsync(MessageType.TaskSummary, TransportChannel.EmailHtml, data);

        result.Should().Contain("2"); // todos count
        result.Should().Contain("1"); // chores count
        // OverdueMaintenance is 0, so HasMaintenance is false — section should not render
        result.Should().NotContain("vehicle maintenance");
    }

    // ---------------------------------------------------------------------
    // Expiry alert
    // ---------------------------------------------------------------------

    private static ExpiryData SampleExpiry() => new()
    {
        Title = "3 item(s) expiring soon",
        Summary = "1 expired; 2 expiring soon",
        DeepLinkUrl = "/stock",
        ExpiredCount = 1,
        ExpiringSoonCount = 2,
        ExpiringItems =
        [
            new ExpiryItemData
            {
                ProductName = "Milk", ExpiryDate = "2026-08-27",
                LocationName = "Fridge", IsExpired = true, DaysUntilExpiry = -2
            },
            new ExpiryItemData
            {
                ProductName = "Yoghurt", ExpiryDate = "2026-08-31",
                LocationName = "Fridge", IsExpired = false, DaysUntilExpiry = 1
            },
            new ExpiryItemData
            {
                ProductName = "Bread", ExpiryDate = "2026-09-02",
                LocationName = "Pantry", IsExpired = false, DaysUntilExpiry = 5
            }
        ]
    };

    [Fact]
    public async Task RenderAsync_Expiry_SeparatesExpiredFromExpiringSoon()
    {
        // Grouping is what makes the distinction survive without colour — several clients strip
        // inline colour, and colour alone excludes anyone who cannot distinguish it.
        var result = await _renderer.RenderAsync(
            MessageType.Expiry, TransportChannel.EmailHtml, SampleExpiry());

        result.Should().Contain("Already expired");
        result.Should().Contain("Expiring soon");

        // Expired first: it is the part that needs acting on today.
        result.IndexOf("Already expired", StringComparison.Ordinal)
            .Should().BeLessThan(result.IndexOf("Expiring soon", StringComparison.Ordinal));

        // Each item under the heading it belongs to.
        result.IndexOf("Milk", StringComparison.Ordinal)
            .Should().BeLessThan(result.IndexOf("Yoghurt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RenderAsync_Expiry_UsesStackedRowsRatherThanATable()
    {
        // A four-column table cannot be read on a phone; clients either scroll it sideways or
        // squeeze the columns until every cell wraps to one word per line.
        var result = await _renderer.RenderAsync(
            MessageType.Expiry, TransportChannel.EmailHtml, SampleExpiry());

        result.Should().NotContain("<th>");
        result.Should().NotContain("<td>");
        result.Should().Contain("class=\"item\"");
    }

    [Fact]
    public async Task RenderAsync_Expiry_ShowsEveryItemWithItsDateAndLocation()
    {
        var result = await _renderer.RenderAsync(
            MessageType.Expiry, TransportChannel.EmailHtml, SampleExpiry());

        foreach (var expected in new[] { "Milk", "Yoghurt", "Bread", "Fridge", "Pantry" })
        {
            result.Should().Contain(expected);
        }
    }

    [Fact]
    public async Task RenderAsync_Expiry_WithNothingExpired_OmitsThatSection()
    {
        // The heading must not appear over an empty list.
        var data = SampleExpiry();
        data.ExpiredCount = 0;
        data.ExpiringItems = data.ExpiringItems.Where(i => !i.IsExpired).ToList();

        var result = await _renderer.RenderAsync(
            MessageType.Expiry, TransportChannel.EmailHtml, data);

        result.Should().NotContain("Already expired");
        result.Should().Contain("Expiring soon");
    }

    [Fact]
    public async Task RenderAsync_Expiry_TextAlternativeMatchesTheGrouping()
    {
        // The plain-text part must not drift from the HTML; some clients only show this one.
        var result = await _renderer.RenderAsync(
            MessageType.Expiry, TransportChannel.EmailText, SampleExpiry());

        result.Should().Contain("ALREADY EXPIRED");
        result.Should().Contain("EXPIRING SOON");
        result.Should().Contain("Milk");
        result.Should().Contain("Bread");
        result.Should().NotContain("<div");
    }

    [Fact]
    public async Task RenderAsync_Expiry_LeadsWithHowLongIsLeft()
    {
        // Standing in a kitchen, "in 5 days" answers the question a calendar date makes the
        // reader work out for themselves.
        var result = await _renderer.RenderAsync(
            MessageType.Expiry, TransportChannel.EmailHtml, SampleExpiry());

        result.Should().Contain("Expired 2 days ago");
        result.Should().Contain("Expires tomorrow");
        result.Should().Contain("Expires in 5 days");
    }

    [Theory]
    [InlineData(-2, "Expired 2 days ago")]
    [InlineData(-1, "Expired yesterday")]
    [InlineData(0, "Expires today")]
    [InlineData(1, "Expires tomorrow")]
    [InlineData(5, "Expires in 5 days")]
    public void RelativeExpiry_ReadsNaturallyAtTheBoundaries(int days, string expected)
    {
        // The singular cases are where a naive "in N days" reads badly — "in 1 days",
        // "Expired 1 days ago" — and today has no plural form at all.
        new ExpiryItemData { DaysUntilExpiry = days }.RelativeExpiry.Should().Be(expected);
    }

    [Fact]
    public async Task RenderAsync_Expiry_SurvivesAClientStrippingTheStylesheet()
    {
        // Several clients drop <style> — Gmail's mobile app does for non-Gmail accounts — so
        // the rows must carry their own structural styling rather than depending on it.
        var result = await _renderer.RenderAsync(
            MessageType.Expiry, TransportChannel.EmailHtml, SampleExpiry());

        var withoutStylesheet = System.Text.RegularExpressions.Regex.Replace(
            result, "<style.*?</style>", string.Empty,
            System.Text.RegularExpressions.RegexOptions.Singleline);

        // Still separated, still legible: a border between rows, muted meta text, and a button
        // that still looks like one.
        withoutStylesheet.Should().Contain("border-bottom:1px solid");
        withoutStylesheet.Should().Contain("color:#666666");
        withoutStylesheet.Should().Contain("background-color:#518751");
        withoutStylesheet.Should().Contain("Already expired");
    }
}
