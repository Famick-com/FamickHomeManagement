using System.Text.Json;

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
}

/// <summary>The household's copy of a shared item.</summary>
public sealed class SharedItemCopy
{
    public SharedItemKind Kind { get; set; }

    public Guid ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>The household already had this item, and that copy is the one returned.</summary>
    public bool AlreadyHad { get; set; }
}

/// <summary>Why a share link couldn't be opened or added.</summary>
public enum SharedLinkProblem
{
    None,
    NotFound,
    Expired,
    Revoked,
    Used,
    Unavailable,
    OwnHousehold,
    PlanRequired,
    PlanExpired,
    TooLarge,
    RateLimited,
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

    /// <summary>What a failed request to a share link's routes means.</summary>
    public static SharedLinkProblem ProblemOf(ApiResult result) => result.ErrorCode switch
    {
        "SHARE_LINK_NOT_FOUND" => SharedLinkProblem.NotFound,
        "SHARE_LINK_EXPIRED" => SharedLinkProblem.Expired,
        "SHARE_LINK_REVOKED" => SharedLinkProblem.Revoked,
        "SHARE_LINK_USED" => SharedLinkProblem.Used,
        "SHARE_LINK_UNAVAILABLE" => SharedLinkProblem.Unavailable,
        "SHARE_LINK_OWN_HOUSEHOLD" => SharedLinkProblem.OwnHousehold,
        "SUBSCRIPTION_TIER_INSUFFICIENT" => SharedLinkProblem.PlanRequired,
        "SUBSCRIPTION_EXPIRED" => SharedLinkProblem.PlanExpired,
        "SHARE_ITEM_TOO_LARGE" => SharedLinkProblem.TooLarge,
        "RATE_LIMITED" => SharedLinkProblem.RateLimited,
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
    public static string CopyRoute(SharedItemCopy copy) => copy.Kind switch
    {
        SharedItemKind.Recipe => $"/recipes/{copy.ItemId}",
        SharedItemKind.Product => $"/products/{copy.ItemId}",
        SharedItemKind.Meal => $"/meals/{copy.ItemId}",
        SharedItemKind.Contact => $"/contacts/{copy.ItemId}",
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
    public static string ProblemKey(SharedLinkProblem problem) =>
        "sharing.problems." + char.ToLowerInvariant(problem.ToString()[0]) + problem.ToString()[1..];
}
