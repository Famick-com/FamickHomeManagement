#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.Stock;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class StockMapper
{
    public static StockEntryDto ToDto(StockEntry source)
    {
        var dto = ToDtoPartial(source);
        dto.ProductName = source.Product != null ? source.Product.Name : string.Empty;
        dto.ProductBarcode = source.Product != null && source.Product.Barcodes != null
            ? source.Product.Barcodes.Select(b => b.Barcode).FirstOrDefault()
            : null;
        dto.LocationName = source.Location != null ? source.Location.Name : null;
        dto.QuantityUnitName = source.Product != null && source.Product.QuantityUnitStock != null
            ? source.Product.QuantityUnitStock.Name
            : string.Empty;
        return dto;
    }

    [MapperIgnoreTarget(nameof(StockEntryDto.ProductName))]
    [MapperIgnoreTarget(nameof(StockEntryDto.ProductBarcode))]
    [MapperIgnoreTarget(nameof(StockEntryDto.LocationName))]
    [MapperIgnoreTarget(nameof(StockEntryDto.QuantityUnitName))]
    private static partial StockEntryDto ToDtoPartial(StockEntry source);

    public static StockEntrySummaryDto ToSummaryDto(StockEntry source)
    {
        var dto = ToSummaryDtoPartial(source);
        dto.ProductName = source.Product != null ? source.Product.Name : string.Empty;
        dto.LocationName = source.Location != null ? source.Location.Name : null;
        dto.QuantityUnitName = source.Product != null && source.Product.QuantityUnitStock != null
            ? source.Product.QuantityUnitStock.Name
            : string.Empty;
        return dto;
    }

    [MapperIgnoreTarget(nameof(StockEntrySummaryDto.ProductName))]
    [MapperIgnoreTarget(nameof(StockEntrySummaryDto.LocationName))]
    [MapperIgnoreTarget(nameof(StockEntrySummaryDto.QuantityUnitName))]
    private static partial StockEntrySummaryDto ToSummaryDtoPartial(StockEntry source);

    [MapperIgnoreTarget(nameof(StockEntry.Id))]
    [MapperIgnoreTarget(nameof(StockEntry.TenantId))]
    [MapperIgnoreTarget(nameof(StockEntry.StockId))]
    [MapperIgnoreTarget(nameof(StockEntry.Open))]
    [MapperIgnoreTarget(nameof(StockEntry.OpenedDate))]
    [MapperIgnoreTarget(nameof(StockEntry.OpenTrackingMode))]
    [MapperIgnoreTarget(nameof(StockEntry.OriginalAmount))]
    [MapperIgnoreTarget(nameof(StockEntry.CreatedAt))]
    [MapperIgnoreTarget(nameof(StockEntry.UpdatedAt))]
    [MapperIgnoreTarget(nameof(StockEntry.Product))]
    [MapperIgnoreTarget(nameof(StockEntry.Location))]
    public static partial StockEntry FromAddRequest(AddStockRequest source);
}
