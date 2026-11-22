using System.Text.Json;
using System.Text.RegularExpressions;
using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Services;

/// <summary>
/// Opening a request to link households and accepting it: what each refusal from the server means,
/// and that every message the page can show exists.
/// </summary>
public class HouseholdLinkRequestsTests
{
    [Theory]
    [InlineData("STEP_UP_REQUIRED", 403, HouseholdLinkRequestProblem.StepUpRequired)]
    [InlineData(null, 404, HouseholdLinkRequestProblem.NotFound)]
    [InlineData(null, 403, HouseholdLinkRequestProblem.NotAdmin)]
    [InlineData(null, 422, HouseholdLinkRequestProblem.Refused)]
    [InlineData(null, 409, HouseholdLinkRequestProblem.Refused)]
    [InlineData(null, 500, HouseholdLinkRequestProblem.Failed)]
    public void A_refusal_is_read_from_its_answer(string? code, int status, HouseholdLinkRequestProblem expected)
    {
        var result = new ApiResult { IsSuccess = false, ErrorCode = code, StatusCode = status };

        HouseholdLinkRequests.ProblemOf(result).Should().Be(expected);
    }

    [Fact]
    public async Task A_refusal_carries_the_servers_reason()
    {
        var api = new Mock<IApiClient>();
        api.Setup(a => a.PostAsync<object, HouseholdLinkAccepted>(HouseholdLinkRequests.AcceptEndpoint("abc"), It.IsAny<object>()))
            .ReturnsAsync(ApiResult<HouseholdLinkAccepted>.Failure("Your households are already linked.", 422));

        var accepted = await HouseholdLinkRequests.AcceptAsync(api.Object, "abc");

        accepted.Should().Be(new HouseholdLinkRequestAccept(null, HouseholdLinkRequestProblem.Refused, "Your households are already linked."));
    }

    [Fact]
    public async Task An_answer_that_isnt_the_apis_is_a_failure_to_show()
    {
        // A server that answers the route with the app's own page, which isn't JSON.
        var api = new Mock<IApiClient>();
        api.Setup(a => a.GetAsync<HouseholdLinkRequestPreview>(It.IsAny<string>())).ThrowsAsync(new JsonException());

        (await HouseholdLinkRequests.LoadAsync(api.Object, "abc")).Problem.Should().Be(HouseholdLinkRequestProblem.Failed);
    }

    [Fact]
    public void The_request_reads_as_the_server_sends_it()
    {
        var request = JsonSerializer.Deserialize<HouseholdLinkRequestPreview>(
            """{ "fromHousehold": "The Smiths", "invitedBy": "Sam Smith", "sentTo": "dan@example.com", "status": 2, "canAccept": false }""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        request.Status.Should().Be(HouseholdLinkRequestStatus.AlreadyLinked);
        request.FromHousehold.Should().Be("The Smiths");
    }

    [Fact]
    public void The_token_is_escaped_into_the_routes()
    {
        HouseholdLinkRequests.AcceptRoute("a+b").Should().Be("/linked-households/accept?token=a%2Bb");
        HouseholdLinkRequests.AcceptEndpoint("a+b").Should().Be("api/cloud/household-link-requests/a%2Bb/accept");
    }

    [Fact]
    public void Every_message_the_page_can_show_exists()
    {
        var page = Path.Combine(RepoRoot, "src", "Famick.HomeManagement.UI", "Pages", "HouseholdLinks", "LinkedHouseholdAccept.razor");
        var keys = Regex.Matches(File.ReadAllText(page), @"L\[""(?<key>[^""]+)""").Select(m => m.Groups["key"].Value)
            .Concat(Enum.GetValues<HouseholdLinkRequestProblem>().Select(HouseholdLinkRequests.ProblemKey))
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
