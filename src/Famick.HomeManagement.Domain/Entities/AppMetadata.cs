namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Global (non-tenant) key/value store for application-level metadata that
/// isn't derivable from other tables. One row per <see cref="Key"/>.
///
/// First use: tracking the SHA-256 hash of the embedded master-product seed
/// file (key <c>MasterCatalogSeedHash</c>) so the seeder can skip re-running
/// the catalog upsert when the file is unchanged.
/// </summary>
public class AppMetadata : BaseEntity
{
    /// <summary>Unique metadata key (e.g. <c>MasterCatalogSeedHash</c>).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Opaque value for the key.</summary>
    public string Value { get; set; } = string.Empty;
}
