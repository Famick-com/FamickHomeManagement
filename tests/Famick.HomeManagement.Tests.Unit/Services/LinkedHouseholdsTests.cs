using System.Text.Json;
using System.Text.RegularExpressions;
using Famick.HomeManagement.UI.Services;
using FluentAssertions;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Services;

/// <summary>
/// Reading which contacts a linked household keeps up to date: one request for a page of contacts,
/// within the server's limit, and nothing shown as linked when the server can't say.
/// </summary>
public class LinkedHouseholdsTests
{
    private readonly Mock<IApiClient> _api = new();

    [Fact]
    public async Task The_page_s_contacts_are_asked_about_once_each_and_read_by_contact()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        string? url = null;
        _api.Setup(x => x.GetAsync<List<ContactLinkInfo>>(It.IsAny<string>()))
            .Callback<string>(u => url = u)
            .ReturnsAsync(ApiResult<List<ContactLinkInfo>>.Success([new ContactLinkInfo { ContactId = b, ManagedByHousehold = "The Smiths" }]));

        var links = await LinkedHouseholds.LoadAsync(_api.Object, [a, b, a]);

        url.Should().Be($"api/cloud/household-links/contacts?ids={a}&ids={b}");
        links.Keys.Should().Equal(b);
        links[b].ManagedByHousehold.Should().Be("The Smiths");
    }

    [Fact]
    public async Task No_more_contacts_are_asked_about_than_the_server_allows()
    {
        string? url = null;
        _api.Setup(x => x.GetAsync<List<ContactLinkInfo>>(It.IsAny<string>()))
            .Callback<string>(u => url = u)
            .ReturnsAsync(ApiResult<List<ContactLinkInfo>>.Success([]));

        await LinkedHouseholds.LoadAsync(_api.Object, Enumerable.Range(0, 600).Select(_ => Guid.NewGuid()));

        Regex.Matches(url!, "ids=").Should().HaveCount(LinkedHouseholds.MaxContactsPerRequest);
    }

    [Fact]
    public async Task No_contacts_need_no_request()
    {
        (await LinkedHouseholds.LoadAsync(_api.Object, [])).Should().BeEmpty();

        _api.VerifyNoOtherCalls();
    }

    public static TheoryData<Func<Mock<IApiClient>, object>> Failures => new()
    {
        api => api.Setup(x => x.GetAsync<List<ContactLinkInfo>>(It.IsAny<string>()))
            .ReturnsAsync(new ApiResult<List<ContactLinkInfo>> { StatusCode = 500 }),
        api => api.Setup(x => x.GetAsync<List<ContactLinkInfo>>(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException()),
        // A server without linked households answers with the app's own page.
        api => api.Setup(x => x.GetAsync<List<ContactLinkInfo>>(It.IsAny<string>()))
            .ThrowsAsync(new JsonException()),
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task A_server_that_can_t_say_leaves_every_contact_unlinked(Func<Mock<IApiClient>, object> fail)
    {
        fail(_api);

        (await LinkedHouseholds.LoadAsync(_api.Object, [Guid.NewGuid()])).Should().BeEmpty();
    }

    [Fact]
    public void A_contact_s_invitation_page_is_under_the_settings_page()
    {
        var id = Guid.NewGuid();

        LinkedHouseholds.InvitationRoute(id).Should().Be($"/settings/linked-households/invite/{id}");
    }

    [Fact]
    public void Every_message_about_linked_households_exists()
    {
        var ui = Path.Combine(RepoRoot, "src", "Famick.HomeManagement.UI");
        string[] files =
        [
            Path.Combine(ui, "Pages", "Contacts", "ContactDetail.razor"),
            Path.Combine(ui, "Pages", "Settings", "Settings.razor"),
            Path.Combine(ui, "Components", "Contacts", "LinkedChip.razor")
        ];
        var keys = files
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"L\[""(?<key>[^""]*linkedHouseholds[^""]*)""").Select(m => m.Groups["key"].Value))
            .Distinct()
            .ToList();
        keys.Should().HaveCountGreaterThan(5);

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
            return directory?.FullName ?? throw new InvalidOperationException("Famick.sln not found above the test output");
        }
    }
}
