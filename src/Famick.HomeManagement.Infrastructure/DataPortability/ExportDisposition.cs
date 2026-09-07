namespace Famick.HomeManagement.Infrastructure.DataPortability;

/// <summary>
/// Whether a household's rows of a given entity type belong in an archive.
/// </summary>
public enum ExportDisposition
{
    /// <summary>Goes into the archive.</summary>
    Export,

    /// <summary>
    /// Withheld because it authenticates somebody. An archive is a file people email around and
    /// leave in a downloads folder; anything in it that grants access is a credential in the wild.
    /// </summary>
    ExcludeSecret,

    /// <summary>
    /// Withheld because it belongs to the deployment rather than the household — seeded reference
    /// data, licensed catalogs, the bookkeeping of the export itself.
    /// </summary>
    ExcludeSystem,
}

/// <summary>
/// Whether rows of a given entity type may be written back on a restore.
/// </summary>
/// <remarks>
/// Separate from <see cref="ExportDisposition"/> on purpose. Audit logs are the case that needs
/// two axes: they are personal data, so withholding them from an export is the worse position to
/// be in, but replaying them into a household fabricates a record of things that did not happen.
/// </remarks>
public enum ImportPolicy
{
    Import,
    Never,
}

/// <summary>
/// What the archive does with one entity type, and why.
/// </summary>
/// <param name="Reason">
/// Written for a person reading the manifest or the failing guard test, not for the code.
/// </param>
public sealed record EntityDisposition(
    ExportDisposition Export,
    ImportPolicy Import,
    string Reason);
