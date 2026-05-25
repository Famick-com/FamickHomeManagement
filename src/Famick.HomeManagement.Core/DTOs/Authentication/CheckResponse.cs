using System.Text.Json.Serialization;

namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Response body for the Phase 4 chunk 4.C <c>/check</c> account-type probe.
/// <c>AccountType</c> serializes as <c>account-type</c> (kebab-case) — that
/// hyphenated wire form is part of the constant-shape contract the mobile
/// client decodes against.
/// </summary>
public class CheckResponse
{
    [JsonPropertyName("account-type")]
    public string AccountType { get; set; } = string.Empty;
}
