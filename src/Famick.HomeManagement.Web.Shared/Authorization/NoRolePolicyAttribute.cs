namespace Famick.HomeManagement.Web.Shared.Authorization;

/// <summary>
/// Declares that a state-changing action deliberately carries no household role policy.
/// </summary>
/// <remarks>
/// Every POST, PUT, PATCH and DELETE action in <c>Web.Shared</c> must either name a role
/// policy (<c>RequireAdmin</c>, <c>RequireEditor</c>, <c>RequireViewer</c>), allow anonymous
/// access, or carry this attribute. <c>ControllerRolePolicyTests</c> enforces that, so a new
/// write cannot fall back to bare <c>[Authorize]</c> — which admits a Viewer — without someone
/// saying why.
/// <para>
/// It has no effect at runtime. Legitimate uses are actions that touch only the caller's own
/// data (profile, passkeys, notifications, sign-out) and POSTs that compute an answer without
/// writing household data.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class NoRolePolicyAttribute(string reason) : Attribute
{
    /// <summary>
    /// Why this action needs no role, for the reader deciding whether that is still true.
    /// </summary>
    public string Reason { get; } = reason;
}
