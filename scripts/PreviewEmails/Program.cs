using System.Diagnostics;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Messaging.Services;
using Microsoft.Extensions.Logging.Abstractions;

var renderer = new StubbleTemplateRenderer(NullLogger<StubbleTemplateRenderer>.Instance);

var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "email-previews");
Directory.CreateDirectory(outputDir);

// Compliance footer context for notification emails
var complianceContext = new Dictionary<string, object>
{
    { "complianceFooter", new Dictionary<string, object>
        {
            { "CompanyName", "Famick Home Management" },
            { "BaseUrl", "https://app.famick.com" },
            { "UnsubscribeUrl", "https://app.famick.com/api/v1/notifications/unsubscribe?token=sample-token" },
            { "PreferencesUrl", "https://app.famick.com/settings/notifications" },
            { "PhysicalAddress", "123 Main Street, Anytown, ST 12345" },
            { "PrivacyPolicyUrl", "https://famick.com/privacy" }
        }
    }
};

var templates = new Dictionary<MessageType, Func<object>>
{
    [MessageType.EmailVerification] = () => new EmailVerificationData
    {
        HouseholdName = "The Smith Family",
        VerificationLink = "famick://verify?token=abc123def456",
        Token = "abc123def456"
    },
    [MessageType.PasswordReset] = () => new PasswordResetData
    {
        UserName = "John",
        ResetLink = "https://app.famick.com/reset-password?token=xyz789"
    },
    [MessageType.PasswordChanged] = () => new PasswordChangedData
    {
        UserName = "John"
    },
    [MessageType.Welcome] = () => new WelcomeData
    {
        UserName = "John Smith",
        Email = "john@example.com",
        TemporaryPassword = "TempPass#2026!",
        LoginUrl = "https://app.famick.com"
    },
    [MessageType.Expiry] = () => new ExpiryData
    {
        Title = "3 item(s) expiring soon",
        Summary = "1 expired; 2 expiring soon",
        DeepLinkUrl = "/stock",
        ExpiredCount = 1,
        ExpiringSoonCount = 2,
        ExpiringItems =
        [
            new() { ProductName = "Whole Milk", ExpiryDate = "2026-04-05", LocationName = "Refrigerator", IsExpired = true },
            new() { ProductName = "Greek Yogurt", ExpiryDate = "2026-04-10", LocationName = "Refrigerator", IsExpired = false },
            new() { ProductName = "Sliced Bread", ExpiryDate = "2026-04-11", LocationName = "Pantry", IsExpired = false }
        ]
    },
    [MessageType.LowStock] = () => new LowStockData
    {
        Title = "2 item(s) low on stock",
        Summary = "2 below minimum stock",
        DeepLinkUrl = "/stock",
        ItemCount = 2,
        LowStockItems =
        [
            new() { Name = "Paper Towels", CurrentStock = 1, MinStockAmount = 4 },
            new() { Name = "Dish Soap", CurrentStock = 0, MinStockAmount = 2 }
        ]
    },
    [MessageType.TaskSummary] = () => new TaskSummaryData
    {
        Title = "You have 5 pending task(s)",
        Summary = "3 todo(s), 1 overdue chore(s), 1 vehicle maintenance due",
        DeepLinkUrl = "/todos",
        TotalTasks = 5,
        IncompleteTodos = 3,
        OverdueChores = 1,
        OverdueMaintenance = 1
    },
    [MessageType.CalendarReminder] = () => new CalendarReminderData
    {
        EventTitle = "Family Dinner at Grandma's",
        StartTime = "18:00 UTC",
        StartDate = "2026-04-12",
        DeepLinkUrl = "/calendar/events/abc123"
    },
    [MessageType.NewFeatures] = () => new FeatureAnnouncementData
    {
        Title = "2 New Features",
        Summary = "Meal Planning; Barcode Scanning",
        IsSingle = false,
        Announcements =
        [
            new() { Title = "Meal Planning", Body = "Plan your weekly meals, generate shopping lists automatically, and track nutritional goals.", LinkUrl = "https://famick.com/blog/meal-planning" },
            new() { Title = "Barcode Scanning", Body = "Scan product barcodes with your phone camera to quickly add items to inventory or shopping lists." }
        ]
    }
};

// Filter by command-line argument if provided
var filter = args.Length > 0 ? args[0] : null;

var generated = new List<string>();

foreach (var (type, dataFactory) in templates)
{
    if (filter != null && !type.ToString().Equals(filter, StringComparison.OrdinalIgnoreCase))
        continue;

    var data = dataFactory();
    var isTransactional = type.IsTransactional();
    var layoutContext = isTransactional ? null : complianceContext;

    var html = await renderer.RenderAsync(type, TransportChannel.EmailHtml, (Famick.HomeManagement.Core.Interfaces.IMessageData)data, layoutContext);
    var text = await renderer.RenderAsync(type, TransportChannel.EmailText, (Famick.HomeManagement.Core.Interfaces.IMessageData)data);
    var subject = await renderer.RenderSubjectAsync(type, (Famick.HomeManagement.Core.Interfaces.IMessageData)data);

    var htmlPath = Path.Combine(outputDir, $"{type}.html");
    var textPath = Path.Combine(outputDir, $"{type}.txt");

    await File.WriteAllTextAsync(htmlPath, html);
    await File.WriteAllTextAsync(textPath, text);

    generated.Add(type.ToString());
    Console.WriteLine($"  {type,-20} -> {htmlPath}");
    Console.WriteLine($"  {"",20}    Subject: {subject}");
}

if (generated.Count == 0)
{
    Console.WriteLine($"No matching message type: {filter}");
    Console.WriteLine($"Available: {string.Join(", ", templates.Keys)}");
    return;
}

// Create an index page
var links = string.Join("\n", generated.Select(g =>
{
    var type = Enum.Parse<MessageType>(g);
    var tag = type.IsTransactional() ? "transactional" : "notification";
    return $"<a href=\"{g}.html\">{g} <span class=\"tag {tag}\">{tag}</span></a>";
}));

var indexHtml = "<!DOCTYPE html>\n<html>\n<head><title>Email Previews</title>\n<style>\n"
    + "body { font-family: Arial, sans-serif; max-width: 800px; margin: 40px auto; }\n"
    + "a { display: block; padding: 8px 0; font-size: 16px; }\n"
    + ".tag { font-size: 11px; padding: 2px 8px; border-radius: 4px; margin-left: 8px; }\n"
    + ".transactional { background: #e3f2fd; color: #1565c0; }\n"
    + ".notification { background: #e8f5e9; color: #2e7d32; }\n"
    + "</style>\n</head>\n<body>\n"
    + "<h1>Email Template Previews</h1>\n"
    + $"<p>Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>\n"
    + links
    + "\n</body>\n</html>";

var indexPath = Path.Combine(outputDir, "index.html");
await File.WriteAllTextAsync(indexPath, indexHtml);

Console.WriteLine($"\nIndex: {indexPath}");

// Open in browser
if (OperatingSystem.IsMacOS())
    Process.Start("open", indexPath);
else if (OperatingSystem.IsWindows())
    Process.Start(new ProcessStartInfo(indexPath) { UseShellExecute = true });
else if (OperatingSystem.IsLinux())
    Process.Start("xdg-open", indexPath);
