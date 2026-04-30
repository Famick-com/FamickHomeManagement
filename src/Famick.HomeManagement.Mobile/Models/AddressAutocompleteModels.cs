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

    /// <summary>
    /// Number of secondary (apt / suite) units this address has when the
    /// suggestion came from a provider that supports secondary expansion
    /// (e.g. Smarty Pro). <c>0</c> or <c>1</c> means no expansion is needed;
    /// <c>&gt; 1</c> indicates the client should fetch the secondary list via
    /// <c>GET /addresses/secondaries/{SuggestionId}</c> before resolving.
    /// </summary>
    public int SecondaryCount { get; set; }

    public string DisplayLine => !string.IsNullOrWhiteSpace(FormattedAddress)
        ? FormattedAddress
        : string.Join(", ", new[] { AddressLine1, City, StateProvince, PostalCode }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>True when the suggestion represents a multi-unit building
    /// (the user will be prompted to pick an apt/suite). Drives the
    /// XAML "(N units)" badge visibility without needing a value converter.</summary>
    public bool HasMultipleUnits => SecondaryCount > 1;

    /// <summary>Pre-formatted "(N units)" string for the multi-unit badge.</summary>
    public string SecondaryBadge => HasMultipleUnits ? $"{SecondaryCount} units" : string.Empty;
}

/// <summary>
/// Result of expanding a parent suggestion's secondary units. Mirrors
/// <see cref="ResolveAddressSuggestionResult"/>'s expired/success shape so
/// callers handle the 410 case uniformly.
/// </summary>
public class ExpandSecondariesResult
{
    public bool Success { get; init; }
    public bool IsExpired { get; init; }
    public IReadOnlyList<AddressSuggestionDto> Suggestions { get; init; } = Array.Empty<AddressSuggestionDto>();
    public string? ErrorMessage { get; init; }

    public static ExpandSecondariesResult Ok(IReadOnlyList<AddressSuggestionDto> suggestions) =>
        new() { Success = true, Suggestions = suggestions };

    public static ExpandSecondariesResult Expired() =>
        new() { Success = false, IsExpired = true };

    public static ExpandSecondariesResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
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
