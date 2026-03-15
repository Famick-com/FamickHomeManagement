using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// Tests for the home button navigation toggle logic.
/// Recreates the detection logic to avoid MAUI project dependency.
/// </summary>
public class HomeButtonNavigationTests
{
    private enum FlyoutDisplayOptions { AsSingleItem, AsMultipleItems }

    private class TestFlyoutItem
    {
        public string Title { get; set; } = string.Empty;
        public FlyoutDisplayOptions DisplayOptions { get; set; }
    }

    /// <summary>
    /// Mirrors the logic in AppShell.OnNavigated: determines if the current
    /// FlyoutItem is the primary one (with bottom tabs).
    /// </summary>
    private static bool IsOnPrimaryTabs(
        List<TestFlyoutItem> allItems,
        TestFlyoutItem currentItem)
    {
        var primary = allItems.FirstOrDefault(
            fi => fi.DisplayOptions == FlyoutDisplayOptions.AsMultipleItems);
        return currentItem == primary;
    }

    private static bool ShouldShowHomeButton(bool isOnPrimaryTabs, bool homeButtonExists)
    {
        return !isOnPrimaryTabs && !homeButtonExists;
    }

    private static bool ShouldRemoveHomeButton(bool isOnPrimaryTabs, bool homeButtonExists)
    {
        return isOnPrimaryTabs && homeButtonExists;
    }

    private readonly TestFlyoutItem _primaryItem = new()
    {
        Title = "Main",
        DisplayOptions = FlyoutDisplayOptions.AsMultipleItems
    };
    private readonly TestFlyoutItem _profileItem = new()
    {
        Title = "Profile",
        DisplayOptions = FlyoutDisplayOptions.AsSingleItem
    };
    private readonly TestFlyoutItem _householdItem = new()
    {
        Title = "Household",
        DisplayOptions = FlyoutDisplayOptions.AsSingleItem
    };

    private List<TestFlyoutItem> AllItems =>
        new() { _primaryItem, _profileItem, _householdItem };

    [Fact]
    public void OnPrimaryTabs_IsOnPrimary()
    {
        IsOnPrimaryTabs(AllItems, _primaryItem).Should().BeTrue();
    }

    [Fact]
    public void OnProfileSection_IsNotOnPrimary()
    {
        IsOnPrimaryTabs(AllItems, _profileItem).Should().BeFalse();
    }

    [Fact]
    public void OnHouseholdSection_IsNotOnPrimary()
    {
        IsOnPrimaryTabs(AllItems, _householdItem).Should().BeFalse();
    }

    [Fact]
    public void OnFlyoutSection_WithoutExistingButton_ShouldShowHomeButton()
    {
        ShouldShowHomeButton(isOnPrimaryTabs: false, homeButtonExists: false)
            .Should().BeTrue();
    }

    [Fact]
    public void OnFlyoutSection_WithExistingButton_ShouldNotDuplicateHomeButton()
    {
        ShouldShowHomeButton(isOnPrimaryTabs: false, homeButtonExists: true)
            .Should().BeFalse();
    }

    [Fact]
    public void OnPrimaryTabs_WithHomeButton_ShouldRemoveHomeButton()
    {
        ShouldRemoveHomeButton(isOnPrimaryTabs: true, homeButtonExists: true)
            .Should().BeTrue();
    }

    [Fact]
    public void OnPrimaryTabs_WithoutHomeButton_ShouldNotRemoveAnything()
    {
        ShouldRemoveHomeButton(isOnPrimaryTabs: true, homeButtonExists: false)
            .Should().BeFalse();
    }
}
