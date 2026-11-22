using System.Text.Json;

namespace Famick.HomeManagement.UI.Services;

/// <summary>Whether the member's household can accept a request to link, numbered as the server sends it.</summary>
public enum HouseholdLinkRequestStatus
{
    Open = 0,

    /// <summary>The request came from the member's own household.</summary>
    OwnHousehold = 1,

    /// <summary>The two households are linked already.</summary>
    AlreadyLinked = 2
}

/// <summary>A request from another household to link with the member's.</summary>
public sealed class HouseholdLinkRequestPreview
{
    public string FromHousehold { get; set; } = string.Empty;

    /// <summary>The member of that household who sent it.</summary>
    public string InvitedBy { get; set; } = string.Empty;

    /// <summary>The address it was sent to, which may not be the member's own.</summary>
    public string SentTo { get; set; } = string.Empty;

    public HouseholdLinkRequestStatus Status { get; set; }

    /// <summary>The member can accept it: it is open, and they are an admin.</summary>
    public bool CanAccept { get; set; }
}

/// <summary>A request accepted: the link, and the household on its other side.</summary>
public sealed class HouseholdLinkAccepted
{
    public Guid LinkId { get; set; }

    public string Household { get; set; } = string.Empty;
}

/// <summary>Why a request to link couldn't be opened or accepted.</summary>
public enum HouseholdLinkRequestProblem
{
    None,

    /// <summary>Expired, withdrawn or accepted already; or a server that doesn't link households.</summary>
    NotFound,

    /// <summary>The member isn't an admin.</summary>
    NotAdmin,

    /// <summary>The server refused it, and said why.</summary>
    Refused,

    /// <summary>The server asked the member to sign in again, and they didn't.</summary>
    StepUpRequired,

    Failed
}

/// <summary>The request, or why it couldn't be read.</summary>
public sealed record HouseholdLinkRequestLoad(HouseholdLinkRequestPreview? Request, HouseholdLinkRequestProblem Problem);

/// <summary>The link made, or why it wasn't, with the server's reason for a refusal.</summary>
public sealed record HouseholdLinkRequestAccept(HouseholdLinkAccepted? Link, HouseholdLinkRequestProblem Problem, string? Reason);

/// <summary>
/// Opening a request to link households in the app and accepting it: the server's routes and what
/// its refusals mean. The request reaches the member as a link the server's claim page sends here.
/// </summary>
public static class HouseholdLinkRequests
{
    /// <summary>The page that shows a request.</summary>
    public static string AcceptRoute(string token) => $"/linked-households/accept?token={Uri.EscapeDataString(token)}";

    public static string RequestEndpoint(string token) => $"api/cloud/household-link-requests/{Uri.EscapeDataString(token)}";

    public static string AcceptEndpoint(string token) => $"{RequestEndpoint(token)}/accept";

    /// <summary>Reads the request. A request that never got an answer is a problem to show.</summary>
    public static async Task<HouseholdLinkRequestLoad> LoadAsync(IApiClient api, string token)
    {
        try
        {
            var result = await api.GetAsync<HouseholdLinkRequestPreview>(RequestEndpoint(token));
            return result.IsSuccess && result.Data != null
                ? new HouseholdLinkRequestLoad(result.Data, HouseholdLinkRequestProblem.None)
                : new HouseholdLinkRequestLoad(null, ProblemOf(result));
        }
        catch (Exception ex) when (NoUsableAnswer(ex))
        {
            return new HouseholdLinkRequestLoad(null, HouseholdLinkRequestProblem.Failed);
        }
    }

    /// <summary>Links the member's household with the one that sent the request.</summary>
    public static async Task<HouseholdLinkRequestAccept> AcceptAsync(IApiClient api, string token)
    {
        try
        {
            var result = await api.PostAsync<object, HouseholdLinkAccepted>(AcceptEndpoint(token), new { });
            if (result.IsSuccess && result.Data != null)
                return new HouseholdLinkRequestAccept(result.Data, HouseholdLinkRequestProblem.None, null);

            var problem = ProblemOf(result);
            return new HouseholdLinkRequestAccept(null, problem, problem == HouseholdLinkRequestProblem.Refused ? result.ErrorMessage : null);
        }
        catch (Exception ex) when (NoUsableAnswer(ex))
        {
            return new HouseholdLinkRequestAccept(null, HouseholdLinkRequestProblem.Failed, null);
        }
    }

    /// <summary>What a failed request to these routes means.</summary>
    public static HouseholdLinkRequestProblem ProblemOf(ApiResult result) => result switch
    {
        // What's left when the member closes the sign-in prompt the client shows for this code.
        { ErrorCode: "STEP_UP_REQUIRED" } => HouseholdLinkRequestProblem.StepUpRequired,
        // A server without linked households has no such route.
        { StatusCode: 404 } => HouseholdLinkRequestProblem.NotFound,
        { StatusCode: 403 } => HouseholdLinkRequestProblem.NotAdmin,
        { StatusCode: 409 or 422 } => HouseholdLinkRequestProblem.Refused,
        _ => HouseholdLinkRequestProblem.Failed
    };

    /// <summary>The localization key explaining a problem.</summary>
    public static string ProblemKey(HouseholdLinkRequestProblem problem) => problem switch
    {
        HouseholdLinkRequestProblem.NotFound => "householdLinks.accept.problems.notFound",
        HouseholdLinkRequestProblem.NotAdmin => "householdLinks.accept.problems.notAdmin",
        HouseholdLinkRequestProblem.StepUpRequired => "householdLinks.accept.problems.stepUpRequired",
        _ => "householdLinks.accept.problems.failed"
    };

    /// <summary>
    /// A request that never got an answer, or got one that isn't the API's: a server without these
    /// routes answers with the app's own page, which can't be read as JSON.
    /// </summary>
    private static bool NoUsableAnswer(Exception ex) =>
        ex is HttpRequestException or OperationCanceledException or JsonException;
}
