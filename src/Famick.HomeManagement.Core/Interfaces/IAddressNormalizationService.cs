using Famick.HomeManagement.Core.DTOs.Common;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for normalizing and geocoding addresses via external APIs
/// </summary>
public interface IAddressNormalizationService
{
    /// <summary>
    /// Normalizes and geocodes an address, returning the best match
    /// </summary>
    /// <param name="request">The address to normalize</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Normalized address with coordinates, or null if not found</returns>
    Task<NormalizedAddressResult?> NormalizeAsync(
        NormalizeAddressRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Normalizes and geocodes an address, returning multiple suggestions
    /// </summary>
    /// <param name="request">The address to normalize</param>
    /// <param name="limit">Maximum number of suggestions to return (default 5)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of normalized address suggestions, empty if none found</returns>
    Task<List<NormalizedAddressResult>> NormalizeSuggestionsAsync(
        NormalizeAddressRequest request,
        int limit = 5,
        CancellationToken cancellationToken = default);
}
