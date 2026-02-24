namespace Famick.HomeManagement.Core.DTOs.StorageBins;

/// <summary>
/// Request to generate a label sheet PDF
/// </summary>
public class GenerateLabelSheetRequest
{
    /// <summary>
    /// Number of sheets to generate
    /// </summary>
    public int SheetCount { get; set; } = 1;

    /// <summary>
    /// IDs of existing bins to include in the labels.
    /// If empty/null, new bins will be created.
    /// </summary>
    public List<Guid>? BinIds { get; set; }

    /// <summary>
    /// Label format to use (determines labels per sheet and dimensions)
    /// </summary>
    public LabelFormat LabelFormat { get; set; } = LabelFormat.Avery94104;

    /// <summary>
    /// If true, repeat the provided bins to fill all sheets.
    /// If false (default), each bin is printed once.
    /// </summary>
    public bool RepeatToFill { get; set; } = false;
}
