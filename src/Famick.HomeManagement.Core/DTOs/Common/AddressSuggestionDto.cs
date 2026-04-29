namespace Famick.HomeManagement.Core.DTOs.Common;

/// <summary>
/// A single address autocomplete suggestion returned from the unified search
/// (local DB + external provider such as Smarty).
/// </summary>
public class AddressSuggestionDto
{
    /// <summary>
    /// Opaque identifier the client returns on selection. For local results this
    /// is a throwaway GUID; for external-provider results it is the cache key
    /// the server issued when caching the provider's suggestion payload.
    /// </summary>
    public Guid SuggestionId { get; set; }

    /// <summary>
    /// Populated only when the suggestion came from the local Addresses table.
    /// Null for external suggestions that have not yet been saved.
    /// </summary>
    public Guid? AddressId { get; set; }

    /// <summary>
    /// Origin of the suggestion, e.g. "Local" or "Smarty". Useful for UI hints.
    /// </summary>
    public string Source { get; set; } = "Local";

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? FormattedAddress { get; set; }
}
