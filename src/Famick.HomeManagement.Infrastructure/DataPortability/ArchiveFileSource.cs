using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Infrastructure.DataPortability;

/// <summary>
/// A file referenced by an exported row, spotted as the row streams past.
/// </summary>
/// <param name="Kind">Folder in the archive, and the label shown to a person.</param>
/// <param name="OwnerEntity">The entity that referenced it.</param>
/// <param name="OwnerId">The row that owns it — the product, the recipe, the contact.</param>
/// <param name="SecondaryId">A second key, for the one attachment nested two levels deep.</param>
/// <param name="FileName">The stored file name.</param>
public sealed record ArchiveFileSource(
    string Kind,
    string OwnerEntity,
    Guid OwnerId,
    Guid? SecondaryId,
    string FileName);

/// <summary>
/// Knows which exported rows carry attachments, and how to read them back.
/// </summary>
/// <remarks>
/// <para>
/// Files are the half of an archive that makes it a backup rather than a spreadsheet, and they do
/// not live in the database — only a reference to them does. So the export watches rows go past
/// and collects the references, rather than asking storage what it holds: storage cannot say
/// which household a loose file belongs to, and a listing would sweep up orphans.
/// </para>
/// <para>
/// <c>VehicleDocument</c> is missing on purpose. It has the same file metadata as the others, but
/// <see cref="IFileStorageService"/> has no vehicle methods at all — there is nowhere to read the
/// bytes from. Rather than claim to export something that cannot be produced, vehicle documents
/// are reported as missing files with that reason.
/// </para>
/// </remarks>
public static class ArchiveFileSources
{
    /// <summary>The property holding the file name, per entity that has one.</summary>
    private static readonly Dictionary<string, (string Kind, string OwnerProperty, string FileNameProperty)> Sources = new()
    {
        ["ProductImage"] = ("product-images", "ProductId", "FileName"),
        ["EquipmentDocument"] = ("equipment-documents", "EquipmentId", "FileName"),
        ["StorageBinPhoto"] = ("storage-bin-photos", "StorageBinId", "FileName"),
        ["RecipeImage"] = ("recipe-images", "RecipeId", "FileName"),
        ["RecipeStep"] = ("recipe-step-images", "RecipeId", "ImageFileName"),
        ["Contact"] = ("contact-profile-images", "Id", "ProfileImageFileName"),
    };

    /// <summary>
    /// Entities whose rows reference a file the storage layer has no way to read.
    /// </summary>
    private static readonly Dictionary<string, (string Kind, string OwnerProperty, string FileNameProperty, string Reason)> Unreadable = new()
    {
        ["VehicleDocument"] = ("vehicle-documents", "VehicleId", "FileName",
            "Vehicle documents have no storage implementation — IFileStorageService exposes no " +
            "vehicle methods, so the bytes cannot be read. Reported rather than silently omitted."),
    };

    public static bool CarriesFiles(string entityName) =>
        Sources.ContainsKey(entityName) || Unreadable.ContainsKey(entityName);

    /// <summary>
    /// Reads a file reference out of one row, or null when the row has no file attached.
    /// </summary>
    public static ArchiveFileSource? Extract(string entityName, IReadOnlyDictionary<string, object?> row)
    {
        var kind = Sources.TryGetValue(entityName, out var s)
            ? (s.Kind, s.OwnerProperty, s.FileNameProperty)
            : Unreadable.TryGetValue(entityName, out var u)
                ? (u.Kind, u.OwnerProperty, u.FileNameProperty)
                : default;

        if (kind.Kind is null) return null;

        if (!row.TryGetValue(kind.FileNameProperty, out var fileName) || fileName is not string name || name.Length == 0)
            return null;

        if (!row.TryGetValue(kind.OwnerProperty, out var owner) || owner is not Guid ownerId)
            return null;

        // Recipe step images are the one attachment keyed by two ids: the recipe and the step.
        Guid? secondary = entityName == "RecipeStep" && row.TryGetValue("Id", out var stepId) && stepId is Guid s2
            ? s2
            : null;

        return new ArchiveFileSource(kind.Kind, entityName, ownerId, secondary, name);
    }

    public static string? UnreadableReason(string entityName) =>
        Unreadable.TryGetValue(entityName, out var u) ? u.Reason : null;

    /// <summary>
    /// Opens the bytes for a collected reference, or null when storage does not have them.
    /// </summary>
    public static Task<Stream?> OpenAsync(IFileStorageService storage, ArchiveFileSource file, CancellationToken ct) =>
        file.OwnerEntity switch
        {
            "ProductImage" => storage.GetProductImageStreamAsync(file.OwnerId, file.FileName, ct),
            "EquipmentDocument" => storage.GetEquipmentDocumentStreamAsync(file.OwnerId, file.FileName, ct),
            "StorageBinPhoto" => storage.GetStorageBinPhotoStreamAsync(file.OwnerId, file.FileName, ct),
            "RecipeImage" => storage.GetRecipeImageStreamAsync(file.OwnerId, file.FileName, ct),
            "RecipeStep" => storage.GetRecipeStepImageStreamAsync(file.OwnerId, file.SecondaryId!.Value, file.FileName, ct),
            "Contact" => storage.GetContactProfileImageStreamAsync(file.OwnerId, file.FileName, ct),
            _ => Task.FromResult<Stream?>(null),
        };

    /// <summary>The path a file takes inside the archive.</summary>
    public static string PathInArchive(ArchiveFileSource file) =>
        file.SecondaryId is { } secondary
            ? $"files/{file.Kind}/{file.OwnerId}/{secondary}/{file.FileName}"
            : $"files/{file.Kind}/{file.OwnerId}/{file.FileName}";
}
