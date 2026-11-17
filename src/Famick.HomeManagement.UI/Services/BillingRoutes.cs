namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Where a household manages its plan, on a server that sells plans.
/// </summary>
/// <remarks>
/// The server serves this page, not the app, so navigate to it with <c>forceLoad</c>. A
/// self-hosted server has no plans and no such page; nothing links to it there.
/// </remarks>
public static class BillingRoutes
{
    public const string Billing = "/settings/billing";
}
