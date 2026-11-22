using System.Reflection;
using Famick.HomeManagement.UI.Localization;
using Famick.HomeManagement.UI.Pages.HouseholdLinks;
using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Moq;
using MudBlazor;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// The page accepting a request to link households: it accepts the request it shows, once, and only
/// for an admin who clicks.
/// </summary>
public class LinkedHouseholdAcceptPageTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly Mock<IApiClient> _api = new();
    private readonly TestNavigationManager _navigation = new();
    private readonly LinkedHouseholdAccept _page = new();

    public LinkedHouseholdAcceptPageTests()
    {
        Inject("ApiClient", _api.Object);
        Inject("Snackbar", Mock.Of<ISnackbar>());
        Inject("L", Mock.Of<ILocalizer>());
        Inject("Navigation", _navigation);
    }

    private void Inject(string name, object value) =>
        typeof(LinkedHouseholdAccept).GetProperty(name, Private)!.SetValue(_page, value);

    private T? Field<T>(string name) => (T?)typeof(LinkedHouseholdAccept).GetField(name, Private)!.GetValue(_page);

    private Task OpenAsync(string? token)
    {
        _page.Token = token;
        return (Task)typeof(LinkedHouseholdAccept).GetMethod("OnParametersSetAsync", Private)!.Invoke(_page, null)!;
    }

    private Task ClickAcceptAsync() => (Task)typeof(LinkedHouseholdAccept).GetMethod("AcceptAsync", Private)!.Invoke(_page, null)!;

    private void Answer(string token, string from, bool canAccept) =>
        _api.Setup(a => a.GetAsync<HouseholdLinkRequestPreview>(HouseholdLinkRequests.RequestEndpoint(token)))
            .ReturnsAsync(ApiResult<HouseholdLinkRequestPreview>.Success(
                new HouseholdLinkRequestPreview { FromHousehold = from, CanAccept = canAccept }));

    [Fact]
    public async Task An_answer_for_the_previous_link_isnt_shown_when_it_arrives_last()
    {
        var first = new TaskCompletionSource<ApiResult<HouseholdLinkRequestPreview>>();
        _api.Setup(a => a.GetAsync<HouseholdLinkRequestPreview>(HouseholdLinkRequests.RequestEndpoint("first"))).Returns(first.Task);
        Answer("second", "The Joneses", canAccept: true);

        var openingFirst = OpenAsync("first");
        await OpenAsync("second");
        first.SetResult(ApiResult<HouseholdLinkRequestPreview>.Success(new HouseholdLinkRequestPreview { FromHousehold = "The Smiths" }));
        await openingFirst;

        Field<HouseholdLinkRequestPreview>("_request")!.FromHousehold.Should().Be("The Joneses");
    }

    [Fact]
    public async Task No_token_is_a_request_not_found()
    {
        await OpenAsync(null);

        Field<HouseholdLinkRequestProblem>("_problem").Should().Be(HouseholdLinkRequestProblem.NotFound);
        _api.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_second_click_while_accepting_doesnt_accept_again()
    {
        Answer("abc", "The Smiths", canAccept: true);
        await OpenAsync("abc");
        var answer = new TaskCompletionSource<ApiResult<HouseholdLinkAccepted>>();
        _api.Setup(a => a.PostAsync<object, HouseholdLinkAccepted>(HouseholdLinkRequests.AcceptEndpoint("abc"), It.IsAny<object>()))
            .Returns(answer.Task);

        var firstClick = ClickAcceptAsync();
        var secondClick = ClickAcceptAsync();
        answer.SetResult(ApiResult<HouseholdLinkAccepted>.Success(new HouseholdLinkAccepted { Household = "The Smiths" }));
        await Task.WhenAll(firstClick, secondClick);

        _api.Verify(a => a.PostAsync<object, HouseholdLinkAccepted>(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
        _navigation.Uri.Should().EndWith("/contacts");
    }

    [Fact]
    public async Task A_member_who_cant_accept_isnt_able_to()
    {
        Answer("abc", "The Smiths", canAccept: false);
        await OpenAsync("abc");

        await ClickAcceptAsync();

        _api.Verify(a => a.PostAsync<object, HouseholdLinkAccepted>(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task A_refused_accept_can_be_tried_again()
    {
        Answer("abc", "The Smiths", canAccept: true);
        await OpenAsync("abc");
        _api.Setup(a => a.PostAsync<object, HouseholdLinkAccepted>(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(new ApiResult<HouseholdLinkAccepted> { ErrorCode = "STEP_UP_REQUIRED", StatusCode = 403 });

        await ClickAcceptAsync();

        Field<HouseholdLinkRequestProblem>("_problem").Should().Be(HouseholdLinkRequestProblem.StepUpRequired);
        Field<bool>("_accepting").Should().BeFalse();
    }

    [Fact]
    public async Task Not_now_goes_home_without_accepting()
    {
        Answer("abc", "The Smiths", canAccept: true);
        await OpenAsync("abc");

        typeof(LinkedHouseholdAccept).GetMethod("GoHome", Private)!.Invoke(_page, null);

        _navigation.Uri.Should().Be("https://example.test/");
        _api.Verify(a => a.PostAsync<object, HouseholdLinkAccepted>(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Theory]
    [InlineData("HouseholdLinks", "LinkedHouseholdAccept.razor")]
    [InlineData("Sharing", "SharedItemAdd.razor")]
    public void No_button_relies_on_an_empty_link(string folder, string file)
    {
        // MudBlazor renders a button with Href="" as a plain button, which goes nowhere when clicked.
        var page = File.ReadAllText(Path.Combine(RepoRoot, "src", "Famick.HomeManagement.UI", "Pages", folder, file));

        page.Should().NotMatchRegex(@"<MudButton[^>]*Href=""""");
    }

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

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://example.test/", "https://example.test/linked-households/accept");

        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }
}
