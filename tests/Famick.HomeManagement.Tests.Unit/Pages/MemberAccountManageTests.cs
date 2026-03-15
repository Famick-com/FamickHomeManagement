using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// Tests for member account management display logic.
/// Recreates display model to avoid MAUI project dependency.
/// </summary>
public class MemberAccountManageTests
{
    private class TestMember
    {
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool HasUserAccount { get; set; }
        public Guid? LinkedUserId { get; set; }
    }

    private static (string statusText, bool roleVisible, bool inviteVisible, bool resendVisible, bool resetVisible)
        ComputeDisplayState(TestMember member)
    {
        if (member.HasUserAccount)
        {
            return ("Account Active", true, false, true, true);
        }
        return ("No Account", false, true, false, false);
    }

    private static string GetRoleDescription(int roleId) => roleId switch
    {
        2 => "Viewer: Can view all data but cannot make changes.",
        1 => "Editor: Can create, edit, and delete data.",
        _ => "Unknown role"
    };

    [Fact]
    public void MemberWithAccount_ShowsActiveStatus()
    {
        var member = new TestMember { HasUserAccount = true, LinkedUserId = Guid.NewGuid() };
        var state = ComputeDisplayState(member);

        state.statusText.Should().Be("Account Active");
        state.roleVisible.Should().BeTrue();
        state.inviteVisible.Should().BeFalse();
        state.resendVisible.Should().BeTrue();
        state.resetVisible.Should().BeTrue();
    }

    [Fact]
    public void MemberWithoutAccount_ShowsNoAccountStatus()
    {
        var member = new TestMember { HasUserAccount = false };
        var state = ComputeDisplayState(member);

        state.statusText.Should().Be("No Account");
        state.roleVisible.Should().BeFalse();
        state.inviteVisible.Should().BeTrue();
        state.resendVisible.Should().BeFalse();
        state.resetVisible.Should().BeFalse();
    }

    [Fact]
    public void ViewerRole_ShowsCorrectDescription()
    {
        GetRoleDescription(2).Should().Contain("view all data");
    }

    [Fact]
    public void EditorRole_ShowsCorrectDescription()
    {
        GetRoleDescription(1).Should().Contain("create, edit, and delete");
    }

    [Fact]
    public void InviteButton_VisibleOnlyWhenNoAccount()
    {
        var withAccount = ComputeDisplayState(new TestMember { HasUserAccount = true, LinkedUserId = Guid.NewGuid() });
        var withoutAccount = ComputeDisplayState(new TestMember { HasUserAccount = false });

        withAccount.inviteVisible.Should().BeFalse();
        withoutAccount.inviteVisible.Should().BeTrue();
    }

    [Fact]
    public void ResendInvite_VisibleOnlyWhenHasAccount()
    {
        var withAccount = ComputeDisplayState(new TestMember { HasUserAccount = true, LinkedUserId = Guid.NewGuid() });
        var withoutAccount = ComputeDisplayState(new TestMember { HasUserAccount = false });

        withAccount.resendVisible.Should().BeTrue();
        withoutAccount.resendVisible.Should().BeFalse();
    }
}
