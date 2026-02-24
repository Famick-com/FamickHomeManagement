using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Stock;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Mapping;

public class StockMappingProfile : Profile
{
    public StockMappingProfile()
    {
        CreateMap<StockEntry, StockEntryDto>()
            .ForMember(dest => dest.ProductName,
                opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : string.Empty))
            .ForMember(dest => dest.ProductBarcode,
                opt => opt.MapFrom(src => src.Product != null && src.Product.Barcodes != null
                    ? src.Product.Barcodes.Select(b => b.Barcode).FirstOrDefault()
                    : null))
            .ForMember(dest => dest.LocationName,
                opt => opt.MapFrom(src => src.Location != null ? src.Location.Name : null))
            .ForMember(dest => dest.QuantityUnitName,
                opt => opt.MapFrom(src => src.Product != null && src.Product.QuantityUnitStock != null
                    ? src.Product.QuantityUnitStock.Name
                    : string.Empty));

        CreateMap<StockEntry, StockEntrySummaryDto>()
            .ForMember(dest => dest.ProductName,
                opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : string.Empty))
            .ForMember(dest => dest.LocationName,
                opt => opt.MapFrom(src => src.Location != null ? src.Location.Name : null))
            .ForMember(dest => dest.QuantityUnitName,
                opt => opt.MapFrom(src => src.Product != null && src.Product.QuantityUnitStock != null
                    ? src.Product.QuantityUnitStock.Name
                    : string.Empty));

        CreateMap<AddStockRequest, StockEntry>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.StockId, opt => opt.Ignore())
            .ForMember(dest => dest.Open, opt => opt.Ignore())
            .ForMember(dest => dest.OpenedDate, opt => opt.Ignore())
            .ForMember(dest => dest.OpenTrackingMode, opt => opt.Ignore())
            .ForMember(dest => dest.OriginalAmount, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Product, opt => opt.Ignore())
            .ForMember(dest => dest.Location, opt => opt.Ignore());
    }
}
