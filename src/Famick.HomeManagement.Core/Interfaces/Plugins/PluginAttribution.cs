namespace Famick.HomeManagement.Core.Interfaces.Plugins;

/// <summary>
/// Attribution and licensing metadata for a plugin's data.
/// Used to render compliance-correct attribution in the UI.
/// </summary>
public class PluginAttribution
{
    /// <summary>
    /// URL to the data source website (e.g., "https://openfoodfacts.org")
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Short license description for inline attribution on product pages
    /// (e.g., "Database: ODbL, Images: CC BY-SA")
    /// </summary>
    public required string LicenseText { get; set; }

    /// <summary>
    /// Longer description of the data source for the settings/about page.
    /// Includes source description, full licensing info, and citation if applicable.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// URL template for individual product pages. Use {barcode} placeholder.
    /// Example: "https://world.openfoodfacts.org/product/{barcode}"
    /// Null if the source doesn't have individual product pages.
    /// </summary>
    public string? ProductUrlTemplate { get; set; }
}
