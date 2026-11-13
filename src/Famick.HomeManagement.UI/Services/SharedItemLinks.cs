using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.DTOs.Contacts;

namespace Famick.HomeManagement.UI.Services;

/// <summary>The kinds of item a share link can carry, numbered as the server sends them.</summary>
public enum SharedItemKind
{
    Recipe = 0,
    Product = 1,
    Meal = 2,
    Contact = 3
}

/// <summary>A share link's item, as anyone holding the link sees it.</summary>
public sealed class SharedLinkPreview
{
    public SharedLinkItem Item { get; set; } = new();

    public DateTime ExpiresAt { get; set; }

    /// <summary>The link can be added by one household only.</summary>
    public bool SingleUse { get; set; }
}

public sealed class SharedLinkItem
{
    public SharedItemKind Kind { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SharedByHouseholdName { get; set; } = string.Empty;

    public bool HasImage { get; set; }

    /// <summary>For a contact, a name for a new household to add them to: their last name, or their company.</summary>
    public string? SuggestedHouseholdName { get; set; }
}

/// <summary>
/// Where a shared contact goes: into one of the household's households, into a new household with
/// this name, or, with neither, into the household's own.
/// </summary>
public sealed record SharedContactPlacement(Guid? HouseholdId = null, string? NewHouseholdName = null);

/// <summary>The household's copy of a shared item.</summary>
public sealed class SharedItemCopy
{
    public SharedItemKind Kind { get; set; }

    public Guid ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>The household already had this item, and that copy is the one returned.</summary>
    public bool AlreadyHad { get; set; }
}

/// <summary>A link just made. Its URL is given out this once: the server keeps only a hash of it.</summary>
public sealed class SharedLinkCreated
{
    public Guid Id { get; set; }

    /// <summary>The page to send, relative to the site: <c>/s/{token}</c>.</summary>
    public string ShareUrl { get; set; } = string.Empty;

    public SharedItemKind Kind { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    /// <summary>The link can be added by one household only: a contact's.</summary>
    public bool SingleUse { get; set; }
}

/// <summary>Where a sent link stands, numbered as the server sends it.</summary>
public enum SharedLinkStatus
{
    Active = 0,
    Expired = 1,
    Revoked = 2,
    Used = 3
}

/// <summary>One of the member's sent links, as their list shows it. The link itself can't be shown again.</summary>
public sealed class SharedLinkSummary
{
    public Guid Id { get; set; }

    public SharedItemKind Kind { get; set; }

    public Guid ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool SingleUse { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public SharedLinkStatus Status { get; set; }

    /// <summary>Times someone outside the household opened it.</summary>
    public int ViewCount { get; set; }

    /// <summary>Households that added a copy from it.</summary>
    public int AddCount { get; set; }
}

/// <summary>A share link's item, or why it couldn't be opened.</summary>
public sealed record SharedLinkLoad(SharedLinkPreview? Preview, SharedLinkProblem Problem);

/// <summary>The household's copy, or why it couldn't be added, naming the plan it needs if the server did.</summary>
public sealed record SharedLinkAdd(SharedItemCopy? Copy, SharedLinkProblem Problem, string? RequiredPlan);

/// <summary>A link just made, or why it couldn't be, naming the plan it needs if the server did.</summary>
public sealed record SharedLinkSend(SharedLinkCreated? Link, SharedLinkProblem Problem, string? RequiredPlan);

/// <summary>The member's sent links, or why they couldn't be read.</summary>
public sealed record SharedLinkList(IReadOnlyList<SharedLinkSummary>? Links, SharedLinkProblem Problem);

/// <summary>Why a share link couldn't be opened, added, made or revoked.</summary>
public enum SharedLinkProblem
{
    None,
    NotFound,
    Expired,
    Revoked,
    Used,
    Unavailable,
    OwnHousehold,
    /// <summary>The household chosen for a contact can't be used, or the new one's name is empty.</summary>
    HouseholdInvalid,
    PlanRequired,
    PlanExpired,
    TooLarge,
    RateLimited,
    /// <summary>The server asked the member to sign in again, and they didn't.</summary>
    StepUpRequired,
    Failed
}

/// <summary>
/// Opening a share link in the app and adding its item to the household: the server's routes, what
/// its refusals mean, and where the copy lives.
/// </summary>
public static class SharedItemLinks
{
    public static string PreviewEndpoint(string token) => $"api/cloud/shares/{Uri.EscapeDataString(token)}";

    /// <summary>The link's item for the add page, read without counting a view: the share page counted it.</summary>
    public static string AddPagePreviewEndpoint(string token) => $"{PreviewEndpoint(token)}?count=false";

    public static string ImageEndpoint(string token) => $"{PreviewEndpoint(token)}/image";

    public static string AddEndpoint(string token) => $"{PreviewEndpoint(token)}/add";

    public const string LinksEndpoint = "api/cloud/shares";

    public const string MyLinksEndpoint = "api/cloud/shares/mine";

    /// <summary>Contacts have their own route, which asks the member to confirm it's them.</summary>
    public const string ContactLinksEndpoint = "api/cloud/shares/contacts";

    public static string LinkEndpoint(Guid id) => $"{LinksEndpoint}/{id}";

    /// <summary>The page listing the member's sent links.</summary>
    public const string MyLinksRoute = "/settings/shared-links";

    /// <summary>Reads the link's item for the add page. A request that never got an answer is a problem to show.</summary>
    public static async Task<SharedLinkLoad> LoadAsync(IApiClient api, string token)
    {
        try
        {
            var result = await api.GetAsync<SharedLinkPreview>(AddPagePreviewEndpoint(token));
            return result.IsSuccess && result.Data != null
                ? new SharedLinkLoad(result.Data, SharedLinkProblem.None)
                : new SharedLinkLoad(null, ProblemOf(result));
        }
        catch (Exception ex) when (NoUsableAnswer(ex))
        {
            return new SharedLinkLoad(null, SharedLinkProblem.Failed);
        }
    }

    /// <summary>
    /// Adds the link's item to the household, a contact where <paramref name="placement"/> says. A
    /// request that never got an answer can be tried again.
    /// </summary>
    public static async Task<SharedLinkAdd> AddAsync(IApiClient api, string token, SharedContactPlacement? placement = null)
    {
        try
        {
            object body = placement switch
            {
                { HouseholdId: { } householdId } => new { householdId },
                { NewHouseholdName: { } newHouseholdName } => new { newHouseholdName },
                _ => new { }
            };
            var result = await api.PostAsync<object, SharedItemCopy>(AddEndpoint(token), body);
            return result.IsSuccess && result.Data != null
                ? new SharedLinkAdd(result.Data, SharedLinkProblem.None, null)
                : new SharedLinkAdd(null, ProblemOf(result), RequiredPlan(result));
        }
        catch (Exception ex) when (NoUsableAnswer(ex))
        {
            return new SharedLinkAdd(null, SharedLinkProblem.Failed, null);
        }
    }

    /// <summary>
    /// Makes a link to one of the household's items. A request that never got an answer can be tried
    /// again: if the server did make a link, nobody has its URL, and it lapses unused.
    /// </summary>
    public static async Task<SharedLinkSend> CreateAsync(IApiClient api, SharedItemKind kind, Guid itemId)
    {
        try
        {
            var result = kind == SharedItemKind.Contact
                ? await api.PostAsync<object, SharedLinkCreated>(ContactLinksEndpoint, new { itemId })
                : await api.PostAsync<object, SharedLinkCreated>(LinksEndpoint, new { kind, itemId });
            return result.IsSuccess && result.Data != null
                ? new SharedLinkSend(result.Data, SharedLinkProblem.None, null)
                : new SharedLinkSend(null, ProblemOf(result), RequiredPlan(result));
        }
        catch (Exception ex) when (NoUsableAnswer(ex))
        {
            return new SharedLinkSend(null, SharedLinkProblem.Failed, null);
        }
    }

    /// <summary>The member's sent links, newest first.</summary>
    public static async Task<SharedLinkList> ListMineAsync(IApiClient api)
    {
        try
        {
            var result = await api.GetAsync<List<SharedLinkSummary>>(MyLinksEndpoint);
            return result.IsSuccess && result.Data != null
                ? new SharedLinkList(result.Data, SharedLinkProblem.None)
                : new SharedLinkList(null, ProblemOf(result));
        }
        catch (Exception ex) when (NoUsableAnswer(ex))
        {
            return new SharedLinkList(null, SharedLinkProblem.Failed);
        }
    }

    /// <summary>
    /// The household's households whose names match <paramref name="search"/>, for adding a shared
    /// contact to one, leaving out the household's own. None when they couldn't be read.
    /// </summary>
    public static async Task<IReadOnlyList<ContactGroupSummaryDto>> SearchHouseholdsAsync(
        IApiClient api, string? search, int max = 20)
    {
        var query = $"contactType=Household&isActive=true&pageSize={max}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&searchTerm={Uri.EscapeDataString(search.Trim())}";
        try
        {
            var result = await api.GetAsync<PagedResult<ContactGroupSummaryDto>>($"{HouseholdsEndpoint}?{query}");
            return result.IsSuccess && result.Data != null
                ? result.Data.Items.Where(g => !g.IsTenantHousehold).ToList()
                : [];
        }
        catch (Exception ex) when (NoUsableAnswer(ex))
        {
            return [];
        }
    }

    public const string HouseholdsEndpoint = "api/v1/contacts/groups";

    /// <summary>Stops a sent link working. <see cref="SharedLinkProblem.None"/> when it has.</summary>
    public static async Task<SharedLinkProblem> RevokeAsync(IApiClient api, Guid id)
    {
        try
        {
            var result = await api.DeleteAsync(LinkEndpoint(id));
            return result.IsSuccess ? SharedLinkProblem.None : ProblemOf(result);
        }
        catch (Exception ex) when (NoUsableAnswer(ex))
        {
            return SharedLinkProblem.Failed;
        }
    }

    /// <summary>
    /// A request that never got an answer, or got one that isn't the API's: a server without these
    /// routes answers with the app's own page, which can't be read as JSON.
    /// </summary>
    private static bool NoUsableAnswer(Exception ex) =>
        ex is HttpRequestException or OperationCanceledException or JsonException;

    /// <summary>What a failed request to a share link's routes means.</summary>
    public static SharedLinkProblem ProblemOf(ApiResult result) => result.ErrorCode switch
    {
        "SHARE_LINK_NOT_FOUND" => SharedLinkProblem.NotFound,
        "SHARE_LINK_EXPIRED" => SharedLinkProblem.Expired,
        "SHARE_LINK_REVOKED" => SharedLinkProblem.Revoked,
        "SHARE_LINK_USED" => SharedLinkProblem.Used,
        "SHARE_LINK_UNAVAILABLE" => SharedLinkProblem.Unavailable,
        "SHARE_LINK_OWN_HOUSEHOLD" => SharedLinkProblem.OwnHousehold,
        "SHARE_LINK_HOUSEHOLD_INVALID" => SharedLinkProblem.HouseholdInvalid,
        "SUBSCRIPTION_TIER_INSUFFICIENT" => SharedLinkProblem.PlanRequired,
        "SUBSCRIPTION_EXPIRED" => SharedLinkProblem.PlanExpired,
        "SHARE_ITEM_TOO_LARGE" => SharedLinkProblem.TooLarge,
        "RATE_LIMITED" => SharedLinkProblem.RateLimited,
        // What's left when the member closes the sign-in prompt the client shows for this code.
        "STEP_UP_REQUIRED" => SharedLinkProblem.StepUpRequired,
        // A server without share links has no such route.
        _ when result.StatusCode == 404 => SharedLinkProblem.NotFound,
        _ when result.StatusCode == 429 => SharedLinkProblem.RateLimited,
        _ => SharedLinkProblem.Failed
    };

    /// <summary>The plan a <see cref="SharedLinkProblem.PlanRequired"/> refusal names, if it names one.</summary>
    public static string? RequiredPlan(ApiResult result) =>
        result.ErrorBody is { ValueKind: JsonValueKind.Object } body
        && body.TryGetProperty("required_tier", out var tier)
        && tier.ValueKind == JsonValueKind.String
            ? tier.GetString()
            : null;

    /// <summary>The page that shows the household's copy.</summary>
    public static string CopyRoute(SharedItemCopy copy) => ItemRoute(copy.Kind, copy.ItemId);

    /// <summary>The page that shows one of the household's items.</summary>
    public static string ItemRoute(SharedItemKind kind, Guid itemId) => kind switch
    {
        SharedItemKind.Recipe => $"/recipes/{itemId}",
        SharedItemKind.Product => $"/products/{itemId}",
        SharedItemKind.Meal => $"/meals/{itemId}",
        SharedItemKind.Contact => $"/contacts/{itemId}",
        _ => "/"
    };

    /// <summary>The localization key naming a kind of item.</summary>
    public static string KindKey(SharedItemKind kind) => kind switch
    {
        SharedItemKind.Recipe => "sharing.kinds.recipe",
        SharedItemKind.Product => "sharing.kinds.product",
        SharedItemKind.Meal => "sharing.kinds.meal",
        SharedItemKind.Contact => "sharing.kinds.contact",
        _ => "sharing.kinds.item"
    };

    /// <summary>The localization key explaining a problem.</summary>
    public static string ProblemKey(SharedLinkProblem problem) => "sharing.problems." + Camel(problem);

    /// <summary>
    /// The localization key explaining why a link couldn't be made or revoked. These are worded for
    /// the sender: a missing item is theirs, and a plan problem stops them sending, not adding.
    /// </summary>
    public static string SendProblemKey(SharedLinkProblem problem) => problem switch
    {
        SharedLinkProblem.NotFound => "sharing.send.problems.notFound",
        SharedLinkProblem.PlanRequired => "sharing.send.problems.planRequired",
        SharedLinkProblem.PlanExpired => "sharing.send.problems.planExpired",
        SharedLinkProblem.TooLarge => "sharing.send.problems.tooLarge",
        SharedLinkProblem.RateLimited => "sharing.problems.rateLimited",
        SharedLinkProblem.StepUpRequired => "sharing.problems.stepUpRequired",
        _ => "sharing.problems.failed"
    };

    /// <summary>The localization key naming a sent link's status.</summary>
    public static string StatusKey(SharedLinkStatus status) => "sharing.status." + Camel(status);

    private static string Camel(Enum value)
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
