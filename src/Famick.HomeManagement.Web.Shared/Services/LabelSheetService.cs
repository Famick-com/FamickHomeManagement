using Famick.HomeManagement.Core.DTOs.StorageBins;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Famick.HomeManagement.Web.Shared.Services;

/// <summary>
/// Service for generating label sheet PDFs in various Avery formats
/// </summary>
public class LabelSheetService
{
    private readonly QrCodeService _qrCodeService;
    private readonly ILogger<LabelSheetService> _logger;

    public LabelSheetService(
        QrCodeService qrCodeService,
        ILogger<LabelSheetService> logger)
    {
        _qrCodeService = qrCodeService;
        _logger = logger;

        // Configure QuestPDF license (free community license)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Generates a PDF with label sheets for the given storage bins
    /// </summary>
    /// <param name="labels">List of labels with short codes</param>
    /// <param name="format">Label format to use (default: Avery94104)</param>
    /// <returns>PDF bytes</returns>
    public byte[] GenerateLabelSheet(List<LabelInfo> labels, LabelFormat format = LabelFormat.Avery94104)
    {
        var spec = LabelFormatSpec.GetSpec(format);
        _logger.LogInformation("Generating label sheet for {Count} labels using format {Format} ({LabelsPerPage}/page)",
            labels.Count, format, spec.LabelsPerPage);

        // Calculate scaling for QR code and text based on label size
        var qrCodeHeight = spec.LabelHeight * 0.65f; // 65% of label height (reduced to accommodate category)
        var fontSize = spec.LabelWidth * 6f; // Scale font with label width (2" -> 12pt, 4" -> 24pt)
        var categoryFontSize = fontSize * 0.6f; // Category text is 60% of short code font size
        var qrPixelsPerModule = spec.LabelWidth >= 3f ? 8 : 6; // Higher resolution for larger labels

        var document = Document.Create(container =>
        {
            var labelIndex = 0;
            var totalPages = (int)Math.Ceiling(labels.Count / (double)spec.LabelsPerPage);

            for (var page = 0; page < totalPages; page++)
            {
                container.Page(pageDescriptor =>
                {
                    pageDescriptor.Size(spec.PageWidth, spec.PageHeight, Unit.Inch);
                    pageDescriptor.MarginLeft(spec.LeftMargin, Unit.Inch);
                    pageDescriptor.MarginTop(spec.TopMargin, Unit.Inch);
                    pageDescriptor.MarginRight(0, Unit.Inch);
                    pageDescriptor.MarginBottom(0, Unit.Inch);

                    pageDescriptor.Content().Column(column =>
                    {
                        for (var row = 0; row < spec.RowsPerPage; row++)
                        {
                            column.Item().Row(rowDescriptor =>
                            {
                                for (var col = 0; col < spec.LabelsPerRow; col++)
                                {
                                    if (labelIndex < labels.Count)
                                    {
                                        var label = labels[labelIndex];
                                        rowDescriptor.ConstantItem(spec.LabelWidth, Unit.Inch)
                                            .Height(spec.LabelHeight, Unit.Inch)
                                            .Element(c => CreateLabel(c, label, qrCodeHeight, fontSize, categoryFontSize, qrPixelsPerModule));
                                        labelIndex++;
                                    }
                                    else
                                    {
                                        // Empty cell
                                        rowDescriptor.ConstantItem(spec.LabelWidth, Unit.Inch)
                                            .Height(spec.LabelHeight, Unit.Inch);
                                    }

                                    // Add horizontal gap between labels (except after last column)
                                    if (col < spec.LabelsPerRow - 1 && spec.HorizontalGap > 0)
                                    {
                                        rowDescriptor.ConstantItem(spec.HorizontalGap, Unit.Inch);
                                    }
                                }
                            });

                            // Add vertical gap between rows (except after last row)
                            if (row < spec.RowsPerPage - 1 && spec.VerticalGap > 0)
                            {
                                column.Item().Height(spec.VerticalGap, Unit.Inch);
                            }
                        }
                    });
                });
            }
        });

        return document.GeneratePdf();
    }

    private void CreateLabel(IContainer container, LabelInfo label, float qrCodeHeight, float fontSize, float categoryFontSize, int qrPixelsPerModule)
    {
        var qrCodeBytes = _qrCodeService.GenerateQrCodeBytes(label.ShortCode, pixelsPerModule: qrPixelsPerModule);

        container
            .Border(0.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(5)
            .Column(column =>
            {
                // QR Code (centered, takes most of the space)
                column.Item()
                    .AlignCenter()
                    .Height(qrCodeHeight, Unit.Inch)
                    .Image(qrCodeBytes)
                    .FitArea();

                // Short code text below QR code
                column.Item()
                    .AlignCenter()
                    .PaddingTop(2)
                    .Text(label.ShortCode)
                    .FontSize(fontSize)
                    .Bold()
                    .FontFamily(Fonts.Courier);

                // Category text below short code (only if set)
                if (!string.IsNullOrWhiteSpace(label.Category))
                {
                    column.Item()
                        .AlignCenter()
                        .PaddingTop(1)
                        .Text(label.Category)
                        .FontSize(categoryFontSize)
                        .FontFamily(Fonts.Arial);
                }
            });
    }
}

/// <summary>
/// Information for a single label
/// </summary>
public class LabelInfo
{
    public Guid Id { get; set; }
    public string ShortCode { get; set; } = string.Empty;
    public string? Category { get; set; }
}
