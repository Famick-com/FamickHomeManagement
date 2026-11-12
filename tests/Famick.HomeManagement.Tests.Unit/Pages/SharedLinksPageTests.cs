using System.Reflection;
using Famick.HomeManagement.UI.Localization;
using Famick.HomeManagement.UI.Pages.Settings;
using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Moq;
using MudBlazor;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// My shared links: a link is revoked once, however quickly Revoke is clicked again.
/// </summary>
public class SharedLinksPageTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly Mock<IApiClient> _api = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly SharedLinks _page = new();

    public SharedLinksPageTests()
    {
        Inject("ApiClient", _api.Object);
        Inject("Snackbar", Mock.Of<ISnackbar>());
        Inject("L", Mock.Of<ILocalizer>());
        Inject("DialogService", _dialogs.Object);
        _api.Setup(a => a.GetAsync<List<SharedLinkSummary>>(SharedItemLinks.MyLinksEndpoint))
            .ReturnsAsync(ApiResult<List<SharedLinkSummary>>.Success([]));
    }

    private void Inject(string name, object value) =>
        typeof(SharedLinks).GetProperty(name, Private)!.SetValue(_page, value);

    private Task RevokeAsync(SharedLinkSummary link) =>
        (Task)typeof(SharedLinks).GetMethod("RevokeAsync", Private)!.Invoke(_page, [link])!;

    private Guid? Revoking => (Guid?)typeof(SharedLinks).GetField("_revoking", Private)!.GetValue(_page);

    private TaskCompletionSource<bool?> Asked()
    {
        var answer = new TaskCompletionSource<bool?>();
        _dialogs.Setup(d => d.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DialogOptions>()))
            .Returns(answer.Task);
        return answer;
    }

    [Fact]
    public async Task A_second_revoke_while_the_question_is_open_is_ignored()
    {
        var link = new SharedLinkSummary { Id = Guid.NewGuid(), ItemName = "Gravy" };
        _api.Setup(a => a.DeleteAsync(SharedItemLinks.LinkEndpoint(link.Id))).ReturnsAsync(ApiResult.Success());
        var answer = Asked();

        var first = RevokeAsync(link);
        var second = RevokeAsync(link);
        answer.SetResult(true);
        await Task.WhenAll(first, second);

        _api.Verify(a => a.DeleteAsync(It.IsAny<string>()), Times.Once);
        Revoking.Should().BeNull();
    }

    [Fact]
    public async Task Cancelling_the_question_lets_revoke_be_clicked_again()
    {
        var answer = Asked();
        answer.SetResult(false);

        await RevokeAsync(new SharedLinkSummary { Id = Guid.NewGuid() });

        _api.Verify(a => a.DeleteAsync(It.IsAny<string>()), Times.Never);
        Revoking.Should().BeNull();
    }
}
