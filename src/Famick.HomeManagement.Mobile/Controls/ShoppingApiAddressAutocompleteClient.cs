using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Controls;

/// <summary>
/// Adapter that maps the <see cref="IAddressAutocompleteClient"/> abstraction
/// the controller expects onto the concrete <c>ShoppingApiClient</c>.
/// </summary>
public sealed class ShoppingApiAddressAutocompleteClient : IAddressAutocompleteClient
{
    private readonly ShoppingApiClient _apiClient;

    public ShoppingApiAddressAutocompleteClient(ShoppingApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<List<AddressSuggestionDto>> GetAutocompleteAsync(string query, int limit, CancellationToken ct)
    {
        var result = await _apiClient.GetAddressAutocompleteAsync(query, limit, ct);
        return result.Success && result.Data != null ? result.Data : new();
    }

    public Task<ResolveAddressSuggestionResult> ResolveAsync(ResolveAddressSuggestionRequest request, CancellationToken ct) =>
        _apiClient.ResolveAddressSuggestionAsync(request, ct);

    public async Task<AddressDto?> StandardizeAsync(StandardizeAddressRequest request, CancellationToken ct)
    {
        var result = await _apiClient.StandardizeAndCreateAddressAsync(request, ct);
        return result.Success ? result.Data : null;
    }
}
