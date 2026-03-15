using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// Tests for member avatar rendering logic.
/// Mirrors the avatar display logic from HouseholdOverviewPage.xaml.cs RenderMembersList().
/// </summary>
public class MemberAvatarRenderingTests
{
    [Fact]
    public void Initials_WithFirstAndLastName_ReturnsBothInitials()
    {
        var result = ComputeInitials("John", "Doe");
        result.Should().Be("JD");
    }

    [Fact]
    public void Initials_WithFirstNameOnly_ReturnsFirstInitial()
    {
        var result = ComputeInitials("John", null);
        result.Should().Be("J");
    }

    [Fact]
    public void Initials_WithEmptyNames_ReturnsQuestionMark()
    {
        var result = ComputeInitials(null, null);
        result.Should().Be("?");
    }

    [Fact]
    public void Initials_AreCaseInsensitive_ReturnsUppercase()
    {
        var result = ComputeInitials("jane", "smith");
        result.Should().Be("JS");
    }

    [Fact]
    public void AvatarDisplay_WithProfileImageUrl_ShowsImage()
    {
        var display = ComputeAvatarDisplay("John", "Doe", "https://example.com/photo.jpg");
        display.ShowProfileImage.Should().BeTrue();
        display.Initials.Should().Be("JD");
    }

    [Fact]
    public void AvatarDisplay_WithoutProfileImageUrl_ShowsInitialsOnly()
    {
        var display = ComputeAvatarDisplay("John", "Doe", null);
        display.ShowProfileImage.Should().BeFalse();
        display.Initials.Should().Be("JD");
    }

    [Fact]
    public void AvatarDisplay_WithEmptyProfileImageUrl_ShowsInitialsOnly()
    {
        var display = ComputeAvatarDisplay("John", "Doe", "");
        display.ShowProfileImage.Should().BeFalse();
    }

    #region Test Helpers

    /// <summary>
    /// Mirrors the initials computation from HouseholdOverviewPage.xaml.cs
    /// </summary>
    private static string ComputeInitials(string? firstName, string? lastName)
    {
        if (!string.IsNullOrEmpty(firstName) && !string.IsNullOrEmpty(lastName))
            return $"{firstName[0]}{lastName[0]}".ToUpper();
        if (!string.IsNullOrEmpty(firstName))
            return firstName[0].ToString().ToUpper();
        return "?";
    }

    /// <summary>
    /// Mirrors the avatar display logic from HouseholdOverviewPage.xaml.cs
    /// </summary>
    private static AvatarDisplayResult ComputeAvatarDisplay(string? firstName, string? lastName, string? profileImageUrl)
    {
        return new AvatarDisplayResult
        {
            Initials = ComputeInitials(firstName, lastName),
            ShowProfileImage = !string.IsNullOrEmpty(profileImageUrl),
        };
    }

    private class AvatarDisplayResult
    {
        public string Initials { get; set; } = "";
        public bool ShowProfileImage { get; set; }
    }

    #endregion
}
