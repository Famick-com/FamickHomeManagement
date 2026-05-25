namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Request body for the Phase 4 chunk 4.C <c>/check</c> account-type probe.
/// Email is the only field; the endpoint is intentionally minimal so the
/// constant-shape contract has nothing else to vary on.
/// </summary>
public class CheckRequest
{
    public string? Email { get; set; }
}
