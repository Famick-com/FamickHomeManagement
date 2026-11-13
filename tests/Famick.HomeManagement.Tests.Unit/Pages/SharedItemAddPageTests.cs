using System.Reflection;
using Famick.HomeManagement.UI.Localization;
using Famick.HomeManagement.UI.Pages.Sharing;
using Famick.HomeManagement.UI.Services;
using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.DTOs.Contacts;
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
    // ---------- where a contact goes ----------

    private static ApiResult<SharedLinkPreview> Contact(string name, string? suggested) =>
        ApiResult<SharedLinkPreview>.Success(new SharedLinkPreview
        {
            Item = new SharedLinkItem { Kind = SharedItemKind.Contact, Name = name, SuggestedHouseholdName = suggested }
        });

    private void Households(params ContactGroupSummaryDto[] households) =>
        _api.Setup(a => a.GetAsync<PagedResult<ContactGroupSummaryDto>>(It.IsAny<string>()))
            .ReturnsAsync(ApiResult<PagedResult<ContactGroupSummaryDto>>.Success(
                new PagedResult<ContactGroupSummaryDto> { Items = households.ToList() }));

    private void Set(string field, object? value) => typeof(SharedItemAdd).GetField(field, Private)!.SetValue(_page, value);

    private bool Chosen => (bool)typeof(SharedItemAdd).GetProperty("Chosen", Private)!.GetValue(_page)!;

    private SharedContactPlacement? Placement =>
        (SharedContactPlacement?)typeof(SharedItemAdd).GetProperty("Placement", Private)!.GetValue(_page);

    [Fact]
    public async Task A_contact_starts_as_a_new_household_named_for_them()
    {
        Households();
        Pending("abc").SetResult(Contact("John Schnepp", "Schnepp"));

        await OpenAsync("abc");

        Field<SharedItemAdd.Destination>("_destination").Should().Be(SharedItemAdd.Destination.NewHousehold);
        Field<string>("_newHouseholdName").Should().Be("Schnepp");
        Placement.Should().Be(new SharedContactPlacement(NewHouseholdName: "Schnepp"));
    }

    [Fact]
    public async Task Without_a_last_name_the_new_household_takes_their_whole_name()
    {
        Households();
        Pending("abc").SetResult(Contact("Cher", null));

        await OpenAsync("abc");

        Field<string>("_newHouseholdName").Should().Be("Cher");
    }

    [Fact]
    public async Task Adding_to_a_household_is_offered_only_when_there_is_another_one()
    {
        Households(new ContactGroupSummaryDto { GroupName = "The Joneses", IsTenantHousehold = true });
        Pending("abc").SetResult(Contact("John Schnepp", "Schnepp"));
        await OpenAsync("abc");
        Field<bool>("_hasOtherHouseholds").Should().BeFalse();

        Households(new ContactGroupSummaryDto { GroupName = "The Joneses", IsTenantHousehold = true },
            new ContactGroupSummaryDto { GroupName = "The Schnepps" });
        Pending("def").SetResult(Contact("John Schnepp", "Schnepp"));
        await OpenAsync("def");
        Field<bool>("_hasOtherHouseholds").Should().BeTrue();
    }

    [Fact]
    public async Task A_contact_isnt_added_until_it_has_somewhere_to_go()
    {
        Households(new ContactGroupSummaryDto { GroupName = "The Schnepps" });
        Pending("abc").SetResult(Contact("John Schnepp", "Schnepp"));
        await OpenAsync("abc");

        Set("_newHouseholdName", "   ");
        Chosen.Should().BeFalse();
        Set("_destination", SharedItemAdd.Destination.ExistingHousehold);
        Chosen.Should().BeFalse();
        await ClickAddAsync();

        _api.Verify(a => a.PostAsync<object, SharedItemCopy>(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task Each_choice_is_what_the_add_sends()
    {
        var schnepps = new ContactGroupSummaryDto { Id = Guid.NewGuid(), GroupName = "The Schnepps" };
        Households(schnepps);
        Pending("abc").SetResult(Contact("John Schnepp", "Schnepp"));
        await OpenAsync("abc");

        Set("_newHouseholdName", "  The Schnepps  ");
        Placement.Should().Be(new SharedContactPlacement(NewHouseholdName: "The Schnepps"));

        Set("_destination", SharedItemAdd.Destination.ExistingHousehold);
        Set("_household", schnepps);
        Placement.Should().Be(new SharedContactPlacement(HouseholdId: schnepps.Id));

        Set("_destination", SharedItemAdd.Destination.MyHousehold);
        Placement.Should().BeNull();
        Chosen.Should().BeTrue();
    }

    [Fact]
    public async Task Households_read_for_the_previous_link_dont_change_the_current_one()
    {
        var households = new TaskCompletionSource<ApiResult<PagedResult<ContactGroupSummaryDto>>>();
        _api.Setup(a => a.GetAsync<PagedResult<ContactGroupSummaryDto>>(It.IsAny<string>())).Returns(households.Task);
        Pending("abc").SetResult(Contact("John Schnepp", "Schnepp"));
        Pending("def").SetResult(Item("Gravy"));

        var openingContact = OpenAsync("abc");
        await OpenAsync("def");
        households.SetResult(ApiResult<PagedResult<ContactGroupSummaryDto>>.Success(new PagedResult<ContactGroupSummaryDto>
        {
            Items = [new ContactGroupSummaryDto { GroupName = "The Schnepps" }]
        }));
        await openingContact;

        Field<SharedLinkPreview>("_preview")!.Item.Name.Should().Be("Gravy");
        Field<bool>("_hasOtherHouseholds").Should().BeFalse();
        Field<bool>("_loading").Should().BeFalse();
    }

    [Fact]
    public async Task Another_kind_has_no_choice_to_make()
    {
        Pending("abc").SetResult(Item("Gravy"));
        await OpenAsync("abc");

        Chosen.Should().BeTrue();
        Placement.Should().BeNull();
        _api.Verify(a => a.GetAsync<PagedResult<ContactGroupSummaryDto>>(It.IsAny<string>()), Times.Never);
    }
}
