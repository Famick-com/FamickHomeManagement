namespace Famick.HomeManagement.Core.DTOs.Common;

/// <summary>
/// Request payload for resolving a cached autocomplete suggestion into a
/// persisted <see cref="AddressDto"/>. The optional secondary-line override lets
/// the UI capture apartment/suite information that the autocomplete source
/// (e.g., Smarty US Autocomplete Pro) does not return.
/// </summary>
public class ResolveAddressSuggestionRequest
{
    public Guid SuggestionId { get; set; }

    /// <summary>
    /// Optional override to attach as AddressLine2 on the saved address.
    /// </summary>
    public string? AddressLine2 { get; set; }
}
