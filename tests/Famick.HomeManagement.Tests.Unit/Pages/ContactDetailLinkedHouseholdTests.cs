using System.Reflection;
using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Messaging;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.UI.Localization;
using Famick.HomeManagement.UI.Pages.Contacts;
using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// The contact page where the server links households: a copy another household keeps up to date
/// keeps only the household's own notes, tags, relationships and visibility editable, and a person
/// or household of the household's contacts can be offered a household of their own.
/// </summary>
public class ContactDetailLinkedHouseholdTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly Mock<IApiClient> _api = new();
    private readonly SubscriptionStateProvider _subscription;
    private readonly ContactDetail _page = new();

    public ContactDetailLinkedHouseholdTests()
    {
        _subscription = new SubscriptionStateProvider(
            _api.Object, Mock.Of<ITokenStorage>(), new MessageBus(NullLogger<MessageBus>.Instance, []));
        _subscription.UpdateFromTenant("Home", false, false, false, householdLinksEnabled: true);

        Inject("ApiClient", _api.Object);
        Inject("SubscriptionState", _subscription);
        Inject("L", Mock.Of<ILocalizer>());
        Inject("Snackbar", Mock.Of<ISnackbar>());
        Inject("Navigation", new TestNavigationManager());
        typeof(ContactDetail).GetProperty("CanEdit", Private)!.SetValue(_page, true);
    }

    private void Inject(string name, object value) =>
        typeof(ContactDetail).GetProperty(name, Private)!.SetValue(_page, value);

    private void Set(string field, object? value) => typeof(ContactDetail).GetField(field, Private)!.SetValue(_page, value);

    private T Get<T>(string member) =>
        (T)(typeof(ContactDetail).GetProperty(member, Private)?.GetValue(_page)
            ?? typeof(ContactDetail).GetField(member, Private)!.GetValue(_page)!);

    private void Showing(ContactDto contact, ContactLinkInfo? info = null)
    {
        _page.Id = contact.Id;
        Set("_contact", contact);
        Set("_linkInfo", info);
    }

    private static ContactDto Person() => new() { Id = Guid.NewGuid(), FirstName = "Dan", CanEdit = true };

    private static ContactDto Household() =>
        new() { Id = Guid.NewGuid(), CompanyName = "The Garcias", IsGroup = true, ContactType = ContactType.Household, CanEdit = true };

    [Fact]
    public void A_copy_keeps_what_its_household_owns_read_only_and_the_rest_editable()
    {
        Showing(Person(), new ContactLinkInfo { ManagedByHousehold = "The Smiths" });

        Get<bool>("CanEditOwned").Should().BeFalse();
        Get<bool>("CanEditGroupOwned").Should().BeFalse();
        Get<bool>("CanEditThis").Should().BeTrue("notes, tags, relationships and visibility are the household's own");
    }

    [Fact]
    public void One_of_the_household_s_own_people_seen_by_a_linked_household_stays_editable()
    {
        Showing(Person(), new ContactLinkInfo { LinkedHouseholds = ["The Smiths"] });

        Get<bool>("CanEditOwned").Should().BeTrue();
        Get<bool>("CanEditGroupOwned").Should().BeTrue();
    }

    public static TheoryData<string, ContactDto, ContactLinkInfo?, bool> Offers => new()
    {
        { "a person", Person(), null, true },
        { "a household", Household(), null, true },
        { "a business", new ContactDto { Id = Guid.NewGuid(), IsGroup = true, ContactType = ContactType.Business }, null, false },
        { "the household's own", new ContactDto { Id = Guid.NewGuid(), IsGroup = true, ContactType = ContactType.Household, IsTenantHousehold = true }, null, false },
        { "someone already in a Famick household", new ContactDto { Id = Guid.NewGuid(), IsHouseholdMember = true }, null, false },
        { "a copy", Person(), new ContactLinkInfo { ManagedByHousehold = "The Smiths" }, false },
    };

    [Theory]
    [MemberData(nameof(Offers))]
    public void A_household_of_their_own_is_offered_for_a_person_or_household_of_the_household_s_contacts(
        string _, ContactDto contact, ContactLinkInfo? info, bool offered)
    {
        Showing(contact, info);

        Get<bool>("CanOfferHousehold").Should().Be(offered);
    }

    [Fact]
    public void Nothing_is_offered_where_the_server_doesn_t_link_households()
    {
        _subscription.UpdateFromTenant("Home", false, false, false, householdLinksEnabled: false);
        Showing(Person());

        Get<bool>("CanOfferHousehold").Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_contact_s_links_are_read_only_where_the_server_links_households(bool enabled)
    {
        _subscription.UpdateFromTenant("Home", false, false, false, householdLinksEnabled: enabled);
        var contact = Person();
        _page.Id = contact.Id;
        _api.Setup(a => a.GetAsync<ContactDto>($"api/v1/contacts/{contact.Id}")).ReturnsAsync(ApiResult<ContactDto>.Success(contact));
        _api.Setup(a => a.GetAsync<List<ContactLinkInfo>>(It.IsAny<string>()))
            .ReturnsAsync(ApiResult<List<ContactLinkInfo>>.Success([new ContactLinkInfo { ContactId = contact.Id, ManagedByHousehold = "The Smiths" }]));

        await (Task)typeof(ContactDetail).GetMethod("LoadDataAsync", Private)!.Invoke(_page, null)!;

        Get<bool>("IsCopy").Should().Be(enabled);
        _api.Verify(a => a.GetAsync<List<ContactLinkInfo>>(It.IsAny<string>()), enabled ? Times.Once() : Times.Never());
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://example.test/", "https://example.test/");

        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
