using AutoMapper;
using Famick.HomeManagement.Core.DTOs.ProductGroups;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Mapping;

public class ProductGroupMappingProfile : Profile
{
    public ProductGroupMappingProfile()
    {
        CreateMap<ProductGroup, ProductGroupDto>()
            .ForMember(dest => dest.ProductCount,
                opt => opt.MapFrom(src => src.Products != null ? src.Products.Count : 0));

        CreateMap<CreateProductGroupRequest, ProductGroup>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Products, opt => opt.Ignore());

        CreateMap<UpdateProductGroupRequest, ProductGroup>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Products, opt => opt.Ignore());

        CreateMap<Product, ProductSummaryDto>()
            .ForMember(dest => dest.ProductGroupName,
                opt => opt.MapFrom(src => src.ProductGroup != null ? src.ProductGroup.Name : null))
            .ForMember(dest => dest.ShoppingLocationName,
                opt => opt.MapFrom(src => src.ShoppingLocation != null ? src.ShoppingLocation.Name : null));
    }
}
