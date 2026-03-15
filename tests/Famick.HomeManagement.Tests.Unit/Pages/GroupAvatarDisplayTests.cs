using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// Tests for contact group avatar display and action logic.
/// Recreates display model to avoid MAUI project dependency.
/// </summary>
public class GroupAvatarDisplayTests
{
    private static (bool showInitials, bool showImage, string initials) ComputeAvatarState(
        string? groupName, string? profileImageFileName)
    {
        var hasImage = !string.IsNullOrEmpty(profileImageFileName);
        var initials = string.Empty;

        if (!string.IsNullOrEmpty(groupName))
        {
            var words = groupName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            initials = words.Length >= 2
                ? $"{words[0][0]}{words[1][0]}".ToUpper()
                : groupName.Length >= 2
                    ? groupName[..2].ToUpper()
                    : groupName.ToUpper();
        }

        return (!hasImage, hasImage, initials);
    }

    private static string[] GetActionSheetOptions(bool hasImage)
    {
        return hasImage
            ? new[] { "Take Photo", "Choose from Gallery", "Remove Image" }
            : new[] { "Take Photo", "Choose from Gallery" };
    }

    [Fact]
    public void GroupWithoutImage_ShowsInitials()
    {
        var state = ComputeAvatarState("Smith Family", null);

        state.showInitials.Should().BeTrue();
        state.showImage.Should().BeFalse();
        state.initials.Should().Be("SF");
    }

    [Fact]
    public void GroupWithImage_ShowsImage()
    {
        var state = ComputeAvatarState("Smith Family", "profile.jpg");

        state.showInitials.Should().BeFalse();
        state.showImage.Should().BeTrue();
    }

    [Fact]
    public void SingleWordGroupName_UsesTwoChars()
    {
        var state = ComputeAvatarState("Contractors", null);

        state.initials.Should().Be("CO");
    }

    [Fact]
    public void SingleCharGroupName_UsesSingleChar()
    {
        var state = ComputeAvatarState("A", null);

        state.initials.Should().Be("A");
    }

    [Fact]
    public void NullGroupName_ReturnsEmptyInitials()
    {
        var state = ComputeAvatarState(null, null);

        state.initials.Should().BeEmpty();
    }

    [Fact]
    public void ActionSheet_WithImage_IncludesRemoveOption()
    {
        var options = GetActionSheetOptions(hasImage: true);

        options.Should().HaveCount(3);
        options.Should().Contain("Remove Image");
    }

    [Fact]
    public void ActionSheet_WithoutImage_ExcludesRemoveOption()
    {
        var options = GetActionSheetOptions(hasImage: false);

        options.Should().HaveCount(2);
        options.Should().NotContain("Remove Image");
    }

    [Fact]
    public void ActionSheet_AlwaysIncludesPhotoOptions()
    {
        var withImage = GetActionSheetOptions(hasImage: true);
        var withoutImage = GetActionSheetOptions(hasImage: false);

        withImage.Should().Contain("Take Photo");
        withImage.Should().Contain("Choose from Gallery");
        withoutImage.Should().Contain("Take Photo");
        withoutImage.Should().Contain("Choose from Gallery");
    }
}
