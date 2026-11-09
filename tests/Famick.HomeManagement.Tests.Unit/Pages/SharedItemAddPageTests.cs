using System.Reflection;
using Famick.HomeManagement.UI.Localization;
using Famick.HomeManagement.UI.Pages.Sharing;
using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Moq;
using MudBlazor;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// The add page's answers arriving in an awkward order: what it shows has to be the link the button
/// adds, and a second click can't add twice.
/// </summary>
public class SharedItemAddPageTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly Mock<IApiClient> _api = new();
    private readonly SharedItemAdd _page = new();

    public SharedItemAddPageTests()
    {
        Inject("ApiClient", _api.Object);
        Inject("Snackbar", Mock.Of<ISnackbar>());
        Inject("L", Mock.Of<ILocalizer>());
        Inject("Navigation", new TestNavigationManager());
    }

    private void Inject(string name, object value) =>
        typeof(SharedItemAdd).GetProperty(name, Private)!.SetValue(_page, value);

    private T? Field<T>(string name) => (T?)typeof(SharedItemAdd).GetField(name, Private)!.GetValue(_page);

    private Task OpenAsync(string token)
    {
        _page.Token = token;
        return (Task)typeof(SharedItemAdd).GetMethod("OnParametersSetAsync", Private)!.Invoke(_page, null)!;
    }

    private Task ClickAddAsync() => (Task)typeof(SharedItemAdd).GetMethod("AddAsync", Private)!.Invoke(_page, null)!;

    private TaskCompletionSource<ApiResult<SharedLinkPreview>> Pending(string token)
    {
        var answer = new TaskCompletionSource<ApiResult<SharedLinkPreview>>();
        _api.Setup(a => a.GetAsync<SharedLinkPreview>(SharedItemLinks.AddPagePreviewEndpoint(token))).Returns(answer.Task);
        return answer;
    }

    private static ApiResult<SharedLinkPreview> Item(string name) =>
        ApiResult<SharedLinkPreview>.Success(new SharedLinkPreview { Item = new SharedLinkItem { Name = name } });

    [Fact]
    public async Task An_answer_for_the_previous_link_isnt_shown_when_it_arrives_last()
    {
        var first = Pending("first");
        var second = Pending("second");

        var openingFirst = OpenAsync("first");
        var openingSecond = OpenAsync("second");
        second.SetResult(Item("Second"));
        await openingSecond;
        first.SetResult(Item("First"));
        await openingFirst;

        Field<SharedLinkPreview>("_preview")!.Item.Name.Should().Be("Second");
        Field<bool>("_loading").Should().BeFalse();
    }

    [Fact]
    public async Task An_answer_for_the_previous_link_doesnt_end_loading_the_current_one()
    {
        var first = Pending("first");
        var second = Pending("second");

        var openingFirst = OpenAsync("first");
        var openingSecond = OpenAsync("second");
        first.SetResult(ApiResult<SharedLinkPreview>.Failure("gone", 410));
        await openingFirst;

        Field<bool>("_loading").Should().BeTrue();
        Field<SharedLinkPreview>("_preview").Should().BeNull();
        Field<SharedLinkProblem>("_problem").Should().Be(SharedLinkProblem.None);

        second.SetResult(Item("Second"));
        await openingSecond;
        Field<SharedLinkPreview>("_preview")!.Item.Name.Should().Be("Second");
    }

    [Fact]
    public async Task Coming_back_to_a_link_waits_for_the_answer_to_the_latest_read()
    {
        var firstRead = new TaskCompletionSource<ApiResult<SharedLinkPreview>>();
        var secondRead = new TaskCompletionSource<ApiResult<SharedLinkPreview>>();
        _api.SetupSequence(a => a.GetAsync<SharedLinkPreview>(SharedItemLinks.AddPagePreviewEndpoint("first")))
            .Returns(firstRead.Task)
            .Returns(secondRead.Task);
        var other = Pending("second");

        var opening = OpenAsync("first");
        var openingOther = OpenAsync("second");
        var returning = OpenAsync("first");
        firstRead.SetResult(Item("Before it changed"));
        await opening;

        Field<bool>("_loading").Should().BeTrue();
        Field<SharedLinkPreview>("_preview").Should().BeNull();

        secondRead.SetResult(Item("Latest"));
        await returning;
        other.SetResult(Item("Second"));
        await openingOther;

        Field<SharedLinkPreview>("_preview")!.Item.Name.Should().Be("Latest");
        Field<bool>("_loading").Should().BeFalse();
    }

    [Fact]
    public async Task A_re_render_on_the_same_link_keeps_what_an_add_reported()
    {
        Pending("abc").SetResult(Item("Gravy"));
        await OpenAsync("abc");
        _api.Setup(a => a.PostAsync<object, SharedItemCopy>(SharedItemLinks.AddEndpoint("abc"), It.IsAny<object>()))
            .ReturnsAsync(new ApiResult<SharedItemCopy> { ErrorCode = "SHARE_LINK_USED", StatusCode = 410 });
        await ClickAddAsync();

        await OpenAsync("abc");

        Field<SharedLinkProblem>("_problem").Should().Be(SharedLinkProblem.Used);
        _api.Verify(a => a.GetAsync<SharedLinkPreview>(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task A_second_click_while_adding_doesnt_add_again()
    {
        Pending("abc").SetResult(Item("Gravy"));
        await OpenAsync("abc");
        var answer = new TaskCompletionSource<ApiResult<SharedItemCopy>>();
        _api.Setup(a => a.PostAsync<object, SharedItemCopy>(It.IsAny<string>(), It.IsAny<object>())).Returns(answer.Task);

        var firstClick = ClickAddAsync();
        var secondClick = ClickAddAsync();
        answer.SetResult(new ApiResult<SharedItemCopy> { ErrorCode = "SHARE_LINK_USED", StatusCode = 410 });
        await Task.WhenAll(firstClick, secondClick);

        _api.Verify(a => a.PostAsync<object, SharedItemCopy>(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
        Field<bool>("_adding").Should().BeFalse("the add can be tried again");
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://example.test/", "https://example.test/");

        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
