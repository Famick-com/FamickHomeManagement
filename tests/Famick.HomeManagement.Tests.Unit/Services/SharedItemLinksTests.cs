using System.Text.Json;
using System.Text.RegularExpressions;
using Famick.HomeManagement.UI.Services;
using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.DTOs.Contacts;
using FluentAssertions;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Services;

/// <summary>
/// Opening a share link in the app and adding its item: what each refusal from the server means,
/// where the copy is shown, and that every message the page can show exists.
/// </summary>
public class SharedItemLinksTests
{
    [Theory]
    [InlineData("SHARE_LINK_NOT_FOUND", 404, SharedLinkProblem.NotFound)]
    [InlineData("SHARE_LINK_EXPIRED", 410, SharedLinkProblem.Expired)]
    [InlineData("SHARE_LINK_REVOKED", 410, SharedLinkProblem.Revoked)]
    [InlineData("SHARE_LINK_USED", 410, SharedLinkProblem.Used)]
    [InlineData("SHARE_LINK_UNAVAILABLE", 410, SharedLinkProblem.Unavailable)]
    [InlineData("SHARE_LINK_OWN_HOUSEHOLD", 409, SharedLinkProblem.OwnHousehold)]
    [InlineData("SHARE_LINK_HOUSEHOLD_INVALID", 400, SharedLinkProblem.HouseholdInvalid)]
    [InlineData("SUBSCRIPTION_TIER_INSUFFICIENT", 403, SharedLinkProblem.PlanRequired)]
    [InlineData("SUBSCRIPTION_EXPIRED", 402, SharedLinkProblem.PlanExpired)]
    [InlineData("SHARE_ITEM_TOO_LARGE", 422, SharedLinkProblem.TooLarge)]
    [InlineData("RATE_LIMITED", 429, SharedLinkProblem.RateLimited)]
    [InlineData("STEP_UP_REQUIRED", 403, SharedLinkProblem.StepUpRequired)]
    [InlineData(null, 404, SharedLinkProblem.NotFound)]
    [InlineData(null, 429, SharedLinkProblem.RateLimited)]
    [InlineData(null, 500, SharedLinkProblem.Failed)]
    public void A_refusal_is_read_from_its_code(string? code, int status, SharedLinkProblem expected)
    {
        var result = new ApiResult { IsSuccess = false, ErrorCode = code, StatusCode = status };

        SharedItemLinks.ProblemOf(result).Should().Be(expected);
    }

    [Fact]
    public void A_plan_refusal_names_the_plan_it_needs()
    {
        var body = JsonDocument.Parse("""{ "code": "SUBSCRIPTION_TIER_INSUFFICIENT", "required_tier": "Home" }""").RootElement;
        var result = new ApiResult { ErrorCode = "SUBSCRIPTION_TIER_INSUFFICIENT", StatusCode = 403, ErrorBody = body };

        SharedItemLinks.RequiredPlan(result).Should().Be("Home");
        SharedItemLinks.RequiredPlan(new ApiResult { StatusCode = 403 }).Should().BeNull();
    }

    [Theory]
    [InlineData(SharedItemKind.Recipe, "/recipes/")]
    [InlineData(SharedItemKind.Product, "/products/")]
    [InlineData(SharedItemKind.Meal, "/meals/")]
    [InlineData(SharedItemKind.Contact, "/contacts/")]
    public void The_copy_opens_on_its_own_page(SharedItemKind kind, string prefix)
    {
        var id = Guid.NewGuid();

        SharedItemLinks.CopyRoute(new SharedItemCopy { Kind = kind, ItemId = id }).Should().Be(prefix + id);
    }

    [Fact]
    public void A_token_is_escaped_into_the_route()
    {
        SharedItemLinks.AddEndpoint("a/b?c").Should().Be("api/cloud/shares/a%2Fb%3Fc/add");
        SharedItemLinks.ImageEndpoint("abc").Should().Be("api/cloud/shares/abc/image");
        SharedItemLinks.AddPagePreviewEndpoint("abc").Should().Be("api/cloud/shares/abc?count=false", "the share page already counted the view");
    }

    [Fact]
    public void The_server_sends_kinds_by_number()
    {
        var preview = JsonSerializer.Deserialize<SharedLinkPreview>(
            """{ "item": { "kind": 2, "name": "Soup night", "sharedByHouseholdName": "Smith", "hasImage": true }, "singleUse": false }""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        preview.Item.Kind.Should().Be(SharedItemKind.Meal);
        preview.Item.HasImage.Should().BeTrue();
    }

    public static TheoryData<Exception> UnansweredRequests => new()
    {
        new HttpRequestException("no route to host"),
        new TaskCanceledException("timed out"),
        // A server without these routes answers with the app's own page.
        new JsonException("'<' is an invalid start of a value.")
    };

    [Theory]
    [MemberData(nameof(UnansweredRequests))]
    public async Task A_link_that_cant_be_reached_is_a_problem_to_show(Exception failure)
    {
        var api = new Mock<IApiClient>();
        api.Setup(a => a.GetAsync<SharedLinkPreview>(It.IsAny<string>())).ThrowsAsync(failure);

        var loaded = await SharedItemLinks.LoadAsync(api.Object, "abc");

        loaded.Preview.Should().BeNull();
        loaded.Problem.Should().Be(SharedLinkProblem.Failed);
    }

    [Theory]
    [MemberData(nameof(UnansweredRequests))]
    public async Task An_add_that_gets_no_answer_can_be_tried_again(Exception failure)
    {
        var api = new Mock<IApiClient>();
        api.Setup(a => a.PostAsync<object, SharedItemCopy>(It.IsAny<string>(), It.IsAny<object>())).ThrowsAsync(failure);

        var added = await SharedItemLinks.AddAsync(api.Object, "abc");

        added.Copy.Should().BeNull();
        added.Problem.Should().Be(SharedLinkProblem.Failed);
    }

    [Fact]
    public async Task Loading_reads_the_item_without_counting_a_view()
    {
        var api = new Mock<IApiClient>();
        var preview = new SharedLinkPreview { Item = new SharedLinkItem { Name = "Gravy" } };
        api.Setup(a => a.GetAsync<SharedLinkPreview>("api/cloud/shares/abc?count=false"))
            .ReturnsAsync(ApiResult<SharedLinkPreview>.Success(preview));

        var loaded = await SharedItemLinks.LoadAsync(api.Object, "abc");

        loaded.Preview.Should().BeSameAs(preview);
        loaded.Problem.Should().Be(SharedLinkProblem.None);
    }

    [Fact]
    public async Task An_add_returns_the_copy_or_the_plan_it_needs()
    {
        var api = new Mock<IApiClient>();
        var copy = new SharedItemCopy { Kind = SharedItemKind.Recipe, ItemId = Guid.NewGuid(), Name = "Gravy" };
        api.Setup(a => a.PostAsync<object, SharedItemCopy>("api/cloud/shares/abc/add", It.IsAny<object>()))
            .ReturnsAsync(ApiResult<SharedItemCopy>.Success(copy));
        api.Setup(a => a.PostAsync<object, SharedItemCopy>("api/cloud/shares/low/add", It.IsAny<object>()))
            .ReturnsAsync(new ApiResult<SharedItemCopy>
            {
                ErrorCode = "SUBSCRIPTION_TIER_INSUFFICIENT",
                StatusCode = 403,
                ErrorBody = JsonDocument.Parse("""{ "required_tier": "Home" }""").RootElement
            });

        (await SharedItemLinks.AddAsync(api.Object, "abc")).Copy.Should().BeSameAs(copy);

        var refused = await SharedItemLinks.AddAsync(api.Object, "low");
        refused.Problem.Should().Be(SharedLinkProblem.PlanRequired);
        refused.RequiredPlan.Should().Be("Home");
    }

    public static TheoryData<SharedContactPlacement?, string> Placements
    {
        get
        {
            var household = Guid.Parse("6f2b8f4e-7d0c-4a43-9a8e-1c2d3e4f5a6b");
            return new()
            {
                { null, "{}" },
                { new SharedContactPlacement(), "{}" },
                { new SharedContactPlacement(NewHouseholdName: "Schnepp"), """{"newHouseholdName":"Schnepp"}""" },
                { new SharedContactPlacement(HouseholdId: household), $$"""{"householdId":"{{household}}"}""" },
            };
        }
    }

    [Theory]
    [MemberData(nameof(Placements))]
    public async Task An_add_says_where_a_contact_goes_and_nothing_for_the_households_own(
        SharedContactPlacement? placement, string body)
    {
        var api = new Mock<IApiClient>();
        object? sent = null;
        api.Setup(a => a.PostAsync<object, SharedItemCopy>("api/cloud/shares/abc/add", It.IsAny<object>()))
            .Callback<string, object>((_, b) => sent = b)
            .ReturnsAsync(ApiResult<SharedItemCopy>.Success(new SharedItemCopy()));

        await SharedItemLinks.AddAsync(api.Object, "abc", placement);

        JsonSerializer.Serialize(sent, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Should().Be(body);
    }

    [Fact]
    public async Task Searching_households_leaves_out_the_households_own()
    {
        var api = new Mock<IApiClient>();
        string? url = null;
        api.Setup(a => a.GetAsync<PagedResult<ContactGroupSummaryDto>>(It.IsAny<string>()))
            .Callback<string>(u => url = u)
            .ReturnsAsync(ApiResult<PagedResult<ContactGroupSummaryDto>>.Success(new PagedResult<ContactGroupSummaryDto>
            {
                Items =
                [
                    new ContactGroupSummaryDto { GroupName = "The Joneses", IsTenantHousehold = true },
                    new ContactGroupSummaryDto { GroupName = "The Schnepps" }
                ]
            }));

        var found = await SharedItemLinks.SearchHouseholdsAsync(api.Object, " Sch&n ");

        found.Select(h => h.GroupName).Should().Equal("The Schnepps");
        url.Should().Be("api/v1/contacts/groups?contactType=Household&isActive=true&pageSize=20&searchTerm=Sch%26n");
    }

    [Theory]
    [MemberData(nameof(UnansweredRequests))]
    public async Task Households_that_cant_be_read_are_none(Exception failure)
    {
        var api = new Mock<IApiClient>();
        api.Setup(a => a.GetAsync<PagedResult<ContactGroupSummaryDto>>(It.IsAny<string>())).ThrowsAsync(failure);

        (await SharedItemLinks.SearchHouseholdsAsync(api.Object, null)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_link_is_made_for_the_item_and_its_url_returned_once()
    {
        var api = new Mock<IApiClient>();
        var itemId = Guid.NewGuid();
        object? sent = null;
        var created = new SharedLinkCreated { Id = Guid.NewGuid(), ShareUrl = "/s/abc", Kind = SharedItemKind.Meal };
        api.Setup(a => a.PostAsync<object, SharedLinkCreated>("api/cloud/shares", It.IsAny<object>()))
            .Callback<string, object>((_, body) => sent = body)
            .ReturnsAsync(ApiResult<SharedLinkCreated>.Success(created));

        var made = await SharedItemLinks.CreateAsync(api.Object, SharedItemKind.Meal, itemId);

        made.Link.Should().BeSameAs(created);
        made.Problem.Should().Be(SharedLinkProblem.None);
        JsonSerializer.Serialize(sent, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Be($$"""{"kind":2,"itemId":"{{itemId}}"}""", "the server reads kinds by number");
    }

    [Fact]
    public async Task A_contact_link_is_made_on_the_contacts_route()
    {
        var api = new Mock<IApiClient>();
        var contactId = Guid.NewGuid();
        object? sent = null;
        api.Setup(a => a.PostAsync<object, SharedLinkCreated>("api/cloud/shares/contacts", It.IsAny<object>()))
            .Callback<string, object>((_, body) => sent = body)
            .ReturnsAsync(ApiResult<SharedLinkCreated>.Success(new SharedLinkCreated { Kind = SharedItemKind.Contact }));

        var made = await SharedItemLinks.CreateAsync(api.Object, SharedItemKind.Contact, contactId);

        made.Problem.Should().Be(SharedLinkProblem.None);
        JsonSerializer.Serialize(sent, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Be($$"""{"itemId":"{{contactId}}"}""");
    }

    [Fact]
    public async Task A_link_the_plan_doesnt_cover_names_the_plan()
    {
        var api = new Mock<IApiClient>();
        api.Setup(a => a.PostAsync<object, SharedLinkCreated>(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(new ApiResult<SharedLinkCreated>
            {
                ErrorCode = "SUBSCRIPTION_TIER_INSUFFICIENT",
                StatusCode = 403,
                ErrorBody = JsonDocument.Parse("""{ "required_tier": "Home" }""").RootElement
            });

        var made = await SharedItemLinks.CreateAsync(api.Object, SharedItemKind.Product, Guid.NewGuid());

        made.Link.Should().BeNull();
        made.Problem.Should().Be(SharedLinkProblem.PlanRequired);
        made.RequiredPlan.Should().Be("Home");
    }

    [Theory]
    [MemberData(nameof(UnansweredRequests))]
    public async Task Making_listing_or_revoking_without_an_answer_is_a_failure_to_show(Exception failure)
    {
        var api = new Mock<IApiClient>();
        api.Setup(a => a.PostAsync<object, SharedLinkCreated>(It.IsAny<string>(), It.IsAny<object>())).ThrowsAsync(failure);
        api.Setup(a => a.GetAsync<List<SharedLinkSummary>>(It.IsAny<string>())).ThrowsAsync(failure);
        api.Setup(a => a.DeleteAsync(It.IsAny<string>())).ThrowsAsync(failure);

        (await SharedItemLinks.CreateAsync(api.Object, SharedItemKind.Recipe, Guid.NewGuid())).Problem.Should().Be(SharedLinkProblem.Failed);
        (await SharedItemLinks.ListMineAsync(api.Object)).Problem.Should().Be(SharedLinkProblem.Failed);
        (await SharedItemLinks.RevokeAsync(api.Object, Guid.NewGuid())).Should().Be(SharedLinkProblem.Failed);
    }

    [Fact]
    public async Task Revoking_a_link_deletes_it_by_id()
    {
        var api = new Mock<IApiClient>();
        var id = Guid.NewGuid();
        api.Setup(a => a.DeleteAsync($"api/cloud/shares/{id}")).ReturnsAsync(ApiResult.Success());
        api.Setup(a => a.DeleteAsync($"api/cloud/shares/{Guid.Empty}"))
            .ReturnsAsync(new ApiResult { StatusCode = 404 });

        (await SharedItemLinks.RevokeAsync(api.Object, id)).Should().Be(SharedLinkProblem.None);
        (await SharedItemLinks.RevokeAsync(api.Object, Guid.Empty)).Should().Be(SharedLinkProblem.NotFound);
    }

    [Fact]
    public void The_server_sends_a_links_status_by_number()
    {
        var links = JsonSerializer.Deserialize<List<SharedLinkSummary>>(
            """[{ "id": "6f9619ff-8b86-d011-b42d-00cf4fc964ff", "kind": 3, "itemName": "Ann", "status": 3, "singleUse": true, "viewCount": 2, "addCount": 1 }]""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        links.Single().Kind.Should().Be(SharedItemKind.Contact);
        links.Single().Status.Should().Be(SharedLinkStatus.Used);
        links.Single().AddCount.Should().Be(1);
    }

    [Theory]
    [InlineData(SharedItemKind.Recipe, "/recipes/")]
    [InlineData(SharedItemKind.Contact, "/contacts/")]
    public void A_sent_link_leads_back_to_its_item(SharedItemKind kind, string prefix)
    {
        var id = Guid.NewGuid();

        SharedItemLinks.ItemRoute(kind, id).Should().Be(prefix + id);
    }

    [Fact]
    public void Sending_problems_are_worded_for_the_sender()
    {
        SharedItemLinks.SendProblemKey(SharedLinkProblem.NotFound).Should().Be("sharing.send.problems.notFound");
        SharedItemLinks.SendProblemKey(SharedLinkProblem.PlanExpired).Should().Be("sharing.send.problems.planExpired");
        SharedItemLinks.SendProblemKey(SharedLinkProblem.Revoked).Should().Be("sharing.problems.failed");
    }

    [Fact]
    public void Every_message_the_page_can_show_exists()
    {
        var ui = Path.Combine(RepoRoot, "src", "Famick.HomeManagement.UI");
        string[] files =
        [
            Path.Combine(ui, "Pages", "Sharing", "SharedItemAdd.razor"),
            Path.Combine(ui, "Pages", "Settings", "SharedLinks.razor"),
            Path.Combine(ui, "Components", "Sharing", "ShareLinkDialog.razor"),
            Path.Combine(ui, "Components", "Sharing", "ShareLinkButton.razor")
        ];
        var keys = files
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"L\[""(?<key>[^""]+)""").Select(m => m.Groups["key"].Value))
            .Concat(Enum.GetValues<SharedLinkProblem>().Select(SharedItemLinks.ProblemKey))
            .Concat(Enum.GetValues<SharedLinkProblem>().Select(SharedItemLinks.SendProblemKey))
            .Concat(Enum.GetValues<SharedLinkStatus>().Select(SharedItemLinks.StatusKey))
            .Concat(Enum.GetValues<SharedItemKind>().Select(SharedItemLinks.KindKey))
            .Distinct()
            .ToList();

        var locale = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepoRoot, "src", "Famick.HomeManagement.UI", "wwwroot", "locales", "en.json"))).RootElement;
        var missing = keys.Where(key => Resolve(locale, key) is not { ValueKind: JsonValueKind.String }).ToList();

        missing.Should().BeEmpty("a missing key renders as its own name. Missing: {0}", string.Join(", ", missing));
    }

    private static JsonElement? Resolve(JsonElement root, string key)
    {
        var current = root;
        foreach (var segment in key.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
                return null;
            current = next;
        }
        return current;
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
}
