namespace Famick.HomeManagement.Core.DTOs.StorageBins;

/// <summary>
/// Supported label formats for storage bin QR code printing
/// </summary>
public enum LabelFormat
{
    /// <summary>
    /// Avery 94104 - 2"x2" square labels, 9 per sheet (3x3 grid)
    /// </summary>
    Avery94104,

    /// <summary>
    /// Avery 94256 - 4"x5" rectangle labels, 4 per sheet (2x2 grid)
    /// </summary>
    Avery94256
}

/// <summary>
/// Specification for a label format including dimensions and layout
/// </summary>
public record LabelFormatSpec(
    string Name,
    float PageWidth,
    float PageHeight,
    float LabelWidth,
    float LabelHeight,
    int LabelsPerRow,
    int RowsPerPage,
    float LeftMargin,
    float TopMargin,
    float HorizontalGap,
    float VerticalGap
)
{
    /// <summary>
    /// Total labels per page (LabelsPerRow * RowsPerPage)
    /// </summary>
    public int LabelsPerPage => LabelsPerRow * RowsPerPage;

    /// <summary>
    /// Available label format specifications
    /// </summary>
    public static readonly Dictionary<LabelFormat, LabelFormatSpec> Specs = new()
    {
        [LabelFormat.Avery94104] = new(
            Name: "Avery 94104 (2\"x2\", 9/sheet)",
            PageWidth: 8.5f,
            PageHeight: 11f,
            LabelWidth: 2f,
            LabelHeight: 2f,
            LabelsPerRow: 3,
            RowsPerPage: 3,
            LeftMargin: 0.5f,
            TopMargin: 0.5f,
            HorizontalGap: 0.25f,
            VerticalGap: 1.25f
        ),
        [LabelFormat.Avery94256] = new(
            Name: "Avery 94256 (4\"x5\", 4/sheet)",
            PageWidth: 8.5f,
            PageHeight: 11f,
            LabelWidth: 4f,
            LabelHeight: 5f,
            LabelsPerRow: 2,
            RowsPerPage: 2,
            LeftMargin: 0.25f,
            TopMargin: 0.5f,
            HorizontalGap: 0f,
            VerticalGap: 0f
        )
    };

    /// <summary>
    /// Get the spec for a given format
    /// </summary>
    public static LabelFormatSpec GetSpec(LabelFormat format) => Specs[format];
}
