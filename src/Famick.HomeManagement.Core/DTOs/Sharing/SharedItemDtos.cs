using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Sharing;

/// <summary>A household's item that one of its members shared.</summary>
/// <param name="OwnerUserId">
/// The member who shared it. The item is read as they see it, so a contact they can't see can't be
/// shared, and nothing is shared once they leave the household.
/// </param>
public sealed record SharedItemRef(ShareableKind Kind, Guid ItemId, Guid OwnerTenantId, Guid OwnerUserId);

/// <summary>A shared item to copy into the recipient's household, as the recipient.</summary>
public sealed record ShareCopyRequest(SharedItemRef Item, Guid RecipientTenantId, Guid RecipientUserId);

/// <summary>
/// What anyone holding a share link may see of the item: enough to recognise it, never a contact's
/// details. Nothing in it identifies the item or either household's records.
/// </summary>
public sealed class SharedItemPreview
{
    public ShareableKind Kind { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>The household that shared it.</summary>
    public string SharedByHouseholdName { get; init; } = string.Empty;

    /// <summary>Whether <c>ReadPreviewImageAsync</c> has a picture to show.</summary>
    public bool HasImage { get; init; }

    /// <summary>A recipe's ingredient lines, its nested recipes' included.</summary>
    public int? IngredientCount { get; init; }

    /// <summary>A recipe's steps.</summary>
    public int? StepCount { get; init; }

    /// <summary>A meal's items.</summary>
    public int? ItemCount { get; init; }
}

/// <summary>A preview's picture.</summary>
public sealed record SharedItemImage(string ContentType, byte[] Bytes);

/// <summary>The recipient's copy of a shared item.</summary>
public sealed class SharedItemCopyResult
{
    public ShareableKind Kind { get; init; }

    /// <summary>The copy, in the recipient's household.</summary>
    public Guid ItemId { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>The household already had this item from an earlier share, and that copy is returned.</summary>
    public bool AlreadyHad { get; init; }

    /// <summary>Pictures left out of the copy, as too large or not a JPEG, PNG or WebP image.</summary>
    public int SkippedImages { get; init; }
}
