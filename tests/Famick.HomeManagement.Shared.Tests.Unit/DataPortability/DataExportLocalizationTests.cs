using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.DataPortability;

/// <summary>
/// Checks that every string the export UI asks for actually exists.
/// </summary>
/// <remarks>
/// A missing key does not throw — it renders as the raw dotted key in the middle of the page, so
/// the first person to notice is a user. There is no bUnit here, so the component is read as text
/// and its L["..."] references are matched against en.json.
/// </remarks>
public class DataExportLocalizationTests
{
    [Theory]
    [InlineData("DataExportSection.razor")]
    [InlineData("RestoreDataDialog.razor")]
    public void EveryKeyTheDataPortabilityUiUsesExistsInEnJson(string componentFile)
    {
        var component = File.ReadAllText(Path.Combine(ComponentDirectory, componentFile));
        var keys = Regex.Matches(component, @"L\[""(?<key>[^""]+)""")
            .Select(m => m.Groups["key"].Value)
            .Distinct()
            .ToList();

        keys.Should().NotBeEmpty("the component is localized, so it must reference some keys");

        var locale = JsonDocument.Parse(File.ReadAllText(LocalePath)).RootElement;
        var missing = keys.Where(key => !Resolves(locale, key)).ToList();

        missing.Should().BeEmpty(
            "a key with no entry renders as its own name in the page. Missing: {0}",
            string.Join(", ", missing));
    }

    [Fact]
    public void ExportKeysResolveToStringsRatherThanObjects()
    {
        var locale = JsonDocument.Parse(File.ReadAllText(LocalePath)).RootElement;

        // Referencing a branch rather than a leaf is the other way to get a key rendered as text.
        Resolve(locale, "dataPortability.export.title")!.Value.ValueKind
            .Should().Be(JsonValueKind.String);

        Resolve(locale, "dataPortability.export")!.Value.ValueKind
            .Should().Be(JsonValueKind.Object, "sanity check that the lookup distinguishes the two");
    }

    private static bool Resolves(JsonElement root, string key) =>
        Resolve(root, key) is { ValueKind: JsonValueKind.String };

    private static JsonElement? Resolve(JsonElement root, string key)
    {
        var current = root;

        foreach (var segment in key.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object) return null;
            if (!current.TryGetProperty(segment, out var next)) return null;
            current = next;
        }

        return current;
    }

    private static string ComponentDirectory => Path.Combine(
        RepoRoot, "src", "Famick.HomeManagement.UI", "Components", "Profile");

    private static string LocalePath => Path.Combine(
        RepoRoot, "src", "Famick.HomeManagement.UI", "wwwroot", "locales", "en.json");

    private static string RepoRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Famick.sln")))
                directory = directory.Parent;

            return directory?.FullName
                ?? throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
        }
    }
}
