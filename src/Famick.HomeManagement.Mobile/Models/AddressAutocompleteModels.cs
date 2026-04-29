namespace Famick.HomeManagement.Mobile.Models;

/// <summary>
/// Client-side mirror of Core's <c>AddressSuggestionDto</c> returned from
/// <c>GET /api/v1/addresses/autocomplete</c>.
/// </summary>
public class AddressSuggestionDto
{
    public Guid SuggestionId { get; set; }
    public Guid? AddressId { get; set; }
    public string Source { get; set; } = "Local";
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? FormattedAddress { get; set; }

    public string DisplayLine => !string.IsNullOrWhiteSpace(FormattedAddress)
        ? FormattedAddress
        : string.Join(", ", new[] { AddressLine1, City, StateProvince, PostalCode }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}

public class ResolveAddressSuggestionRequest
{
    public Guid SuggestionId { get; set; }
    public string? AddressLine2 { get; set; }
}

/// <summary>
/// Result of resolving an autocomplete suggestion. Distinguishes a genuine
/// failure (<see cref="Success"/> = false, <see cref="IsExpired"/> = false)
/// from the 410-Gone case where the client should simply re-query.
/// </summary>
public class ResolveAddressSuggestionResult
{
    public bool Success { get; init; }
    public bool IsExpired { get; init; }
    public AddressDto? Address { get; init; }
    public string? ErrorMessage { get; init; }

    public static ResolveAddressSuggestionResult Ok(AddressDto address) =>
        new() { Success = true, Address = address };

    public static ResolveAddressSuggestionResult Expired() =>
        new() { Success = false, IsExpired = true };

    public static ResolveAddressSuggestionResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

public class StandardizeAddressRequest
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}
