using System.Text.Json;
using System.Text.RegularExpressions;
using Famick.HomeManagement.UI.Services;
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
    [InlineData("SUBSCRIPTION_TIER_INSUFFICIENT", 403, SharedLinkProblem.PlanRequired)]
    [InlineData("SUBSCRIPTION_EXPIRED", 402, SharedLinkProblem.PlanExpired)]
    [InlineData("SHARE_ITEM_TOO_LARGE", 422, SharedLinkProblem.TooLarge)]
    [InlineData("RATE_LIMITED", 429, SharedLinkProblem.RateLimited)]
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
        new TaskCanceledException("timed out")
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

    [Fact]
    public void Every_message_the_page_can_show_exists()
    {
        var page = File.ReadAllText(Path.Combine(RepoRoot, "src", "Famick.HomeManagement.UI", "Pages", "Sharing", "SharedItemAdd.razor"));
        var keys = Regex.Matches(page, @"L\[""(?<key>[^""]+)""")
            .Select(m => m.Groups["key"].Value)
            .Concat(Enum.GetValues<SharedLinkProblem>().Select(SharedItemLinks.ProblemKey))
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
