using System.Reflection;
using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.UI.Components.Sharing;
using Famick.HomeManagement.UI.Localization;
using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Moq;
using MudBlazor;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// The send dialog: a link is made only on a click and once per click, its address is the app's own,
/// a link nobody has copied or shared isn't lost without a question, and a recipe's older link can
/// still be revoked from it.
/// </summary>
public class ShareLinkDialogTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly Mock<IApiClient> _api = new();
    private readonly RecordingJsRuntime _js = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly Mock<IMudDialogInstance> _instance = new();
    private readonly ShareLinkDialog _dialog = new() { Kind = SharedItemKind.Recipe, ItemId = Guid.NewGuid(), ItemName = "Gravy" };

    public ShareLinkDialogTests()
    {
        Inject("ApiClient", _api.Object);
        Inject("Snackbar", Mock.Of<ISnackbar>());
        Inject("JS", _js);
        Inject("L", Mock.Of<ILocalizer>());
        Inject("Navigation", new TestNavigationManager("https://example.test/api/hassio_ingress/abc/"));
        Inject("ShareIcons", new ShareIcons(_js));
        Inject("Dialogs", _dialogs.Object);
        Inject("MudDialog", _instance.Object);
    }

    private void Inject(string name, object value) =>
        typeof(ShareLinkDialog).GetProperty(name, Private)!.SetValue(_dialog, value);

    private T? Field<T>(string name) => (T?)typeof(ShareLinkDialog).GetField(name, Private)!.GetValue(_dialog);

    private Task Call(string method) => (Task)typeof(ShareLinkDialog).GetMethod(method, Private)!.Invoke(_dialog, null)!;

    private void Created(Task<ApiResult<SharedLinkCreated>> answer) =>
        _api.Setup(a => a.PostAsync<object, SharedLinkCreated>(SharedItemLinks.LinksEndpoint, It.IsAny<object>())).Returns(answer);

    private static SharedLinkCreated Link() =>
        new() { Id = Guid.NewGuid(), ShareUrl = "/s/token", ExpiresAt = DateTime.UtcNow.AddDays(90) };

    [Fact]
    public async Task The_link_is_shown_and_copied_at_the_apps_own_address()
    {
        Created(Task.FromResult(ApiResult<SharedLinkCreated>.Success(Link())));

        await Call("CreateAsync");
        var shown = typeof(ShareLinkDialog).GetProperty("AbsoluteShareUrl", Private)!.GetValue(_dialog);
        await Call("CopyLinkAsync");

        shown.Should().Be("https://example.test/api/hassio_ingress/abc/s/token");
        _js.Identifier.Should().Be("navigator.clipboard.writeText");
        _js.Arguments.Should().Equal(shown);
    }

    [Fact]
    public async Task The_share_sheet_gets_the_items_name_a_line_about_it_and_the_link()
    {
        var l = new Mock<ILocalizer>();
        l.Setup(x => x["sharing.send.shareText", It.IsAny<object[]>()])
            .Returns((string _, object[] args) => $"{args[0]}, shared on Famick");
        Inject("L", l.Object);
        Created(Task.FromResult(ApiResult<SharedLinkCreated>.Success(Link())));
        await Call("CreateAsync");

        await Call("ShareAsync");

        _js.Identifier.Should().Be("navigator.share");
        _js.Arguments.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            title = "Gravy",
            text = "Gravy, shared on Famick",
            url = "https://example.test/api/hassio_ingress/abc/s/token"
        });
    }

    [Fact]
    public async Task A_second_click_while_making_the_link_doesnt_make_another()
    {
        var answer = new TaskCompletionSource<ApiResult<SharedLinkCreated>>();
        Created(answer.Task);

        var first = Call("CreateAsync");
        var second = Call("CreateAsync");
        answer.SetResult(ApiResult<SharedLinkCreated>.Success(Link()));
        await Task.WhenAll(first, second);

        _api.Verify(a => a.PostAsync<object, SharedLinkCreated>(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
        Field<bool>("_creating").Should().BeFalse();
    }

    [Fact]
    public async Task A_refused_link_shows_why_and_can_be_tried_again()
    {
        Created(Task.FromResult(new ApiResult<SharedLinkCreated> { ErrorCode = "SUBSCRIPTION_EXPIRED", StatusCode = 402 }));

        await Call("CreateAsync");

        Field<SharedLinkCreated>("_link").Should().BeNull();
        Field<SharedLinkProblem>("_problem").Should().Be(SharedLinkProblem.PlanExpired);
        Field<bool>("_creating").Should().BeFalse();
    }

    [Fact]
    public async Task Revoking_the_new_link_takes_it_off_the_dialog()
    {
        var link = Link();
        Created(Task.FromResult(ApiResult<SharedLinkCreated>.Success(link)));
        _api.Setup(a => a.DeleteAsync(SharedItemLinks.LinkEndpoint(link.Id))).ReturnsAsync(ApiResult.Success());
        await Call("CreateAsync");

        await Call("RevokeAsync");

        Field<SharedLinkCreated>("_link").Should().BeNull();
        Field<SharedLinkProblem>("_problem").Should().Be(SharedLinkProblem.None);
    }

    private void LeavingAnswered(bool? answer) =>
        _dialogs.Setup(d => d.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DialogOptions>()))
            .ReturnsAsync(answer);

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public async Task Leaving_before_the_link_is_copied_or_shared_asks_first(bool? answer, bool closed)
    {
        Created(Task.FromResult(ApiResult<SharedLinkCreated>.Success(Link())));
        await Call("CreateAsync");
        LeavingAnswered(answer);

        await Call("Close");

        _dialogs.Verify(d => d.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DialogOptions>()), Times.Once);
        _instance.Verify(i => i.Close(), closed ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task Going_to_my_links_with_an_unsaved_link_stays_put_unless_the_member_agrees()
    {
        Created(Task.FromResult(ApiResult<SharedLinkCreated>.Success(Link())));
        await Call("CreateAsync");
        LeavingAnswered(null);

        await Call("OpenMyLinks");

        _instance.Verify(i => i.Close(), Times.Never);
    }

    [Theory]
    [InlineData("CopyLinkAsync")]
    [InlineData("ShareAsync")]
    public async Task Once_the_link_is_copied_or_shared_closing_doesnt_ask(string handOff)
    {
        Created(Task.FromResult(ApiResult<SharedLinkCreated>.Success(Link())));
        await Call("CreateAsync");
        await Call(handOff);

        await Call("Close");

        _dialogs.VerifyNoOtherCalls();
        _instance.Verify(i => i.Close(), Times.Once);
    }

    [Fact]
    public async Task Closing_without_a_link_doesnt_ask()
    {
        await Call("Close");

        _dialogs.VerifyNoOtherCalls();
        _instance.Verify(i => i.Close(), Times.Once);
    }

    [Theory]
    [InlineData(false, 30, true)]
    [InlineData(true, 30, false)]
    [InlineData(false, -1, false)]
    public void Only_a_recipes_working_older_link_is_offered(bool revoked, int daysLeft, bool offered)
    {
        _dialog.OlderRecipeLink = new RecipeShareDto { IsRevoked = revoked, ExpiresAt = DateTime.UtcNow.AddDays(daysLeft) };

        typeof(ShareLinkDialog).GetMethod("OnParametersSet", Private)!.Invoke(_dialog, null);

        (Field<RecipeShareDto>("_olderLink") != null).Should().Be(offered);
    }

    [Fact]
    public async Task The_older_link_is_revoked_through_the_recipe()
    {
        _dialog.OlderRecipeLink = new RecipeShareDto { ExpiresAt = DateTime.UtcNow.AddDays(30) };
        typeof(ShareLinkDialog).GetMethod("OnParametersSet", Private)!.Invoke(_dialog, null);
        _api.Setup(a => a.DeleteAsync($"api/v1/recipes/{_dialog.ItemId}/share")).ReturnsAsync(ApiResult.Success());

        await Call("RevokeOlderLinkAsync");

        Field<RecipeShareDto>("_olderLink").Should().BeNull();
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager(string baseUri) => Initialize(baseUri, baseUri);

        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    /// <summary>Records the last call. The browser can't share here, as a desktop browser often can't.</summary>
    private sealed class RecordingJsRuntime : IJSRuntime
    {
        public string? Identifier { get; private set; }
        public object?[]? Arguments { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "navigator.canShare")
                throw new JSException("navigator.canShare is not a function");

            Identifier = identifier;
            Arguments = args;
            return ValueTask.FromResult(default(TValue)!);
        }
    }
}
