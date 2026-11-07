using Famick.HomeManagement.Core.DTOs.Sharing;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Copies one household's recipe, product, meal or contact into another household, as an independent
/// copy that the sender's later changes don't reach. Reads run as the sharing member in the sender's
/// household and writes as the recipient in theirs; neither runs in the calling request's context.
/// Only for multi-tenant deployments: a single-household server has no one to share with.
/// </summary>
public interface ISharedItemCopier
{
    /// <summary>
    /// The item as anyone holding its link may see it, or null when it can't be shared: gone, inactive,
    /// not visible to the member who shared it, or that member no longer in the household.
    /// </summary>
    Task<SharedItemPreview?> PreviewAsync(SharedItemRef item, CancellationToken ct = default);

    /// <summary>The preview's picture, or null when it has none or can't be shared.</summary>
    Task<SharedItemImage?> ReadPreviewImageAsync(SharedItemRef item, CancellationToken ct = default);

    /// <summary>
    /// Adds a copy of the item to the recipient's household, or returns the copy it already has from an
    /// earlier share. Null when the item can't be shared. All or nothing: a copy that fails leaves
    /// nothing behind.
    /// </summary>
    /// <exception cref="Exceptions.ShareTooLargeException">The item is too large to copy.</exception>
    Task<SharedItemCopyResult?> CopyAsync(ShareCopyRequest request, CancellationToken ct = default);
}
