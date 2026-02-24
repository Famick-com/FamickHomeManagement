using AutoMapper;
using Famick.HomeManagement.Core.DTOs.ShoppingLocations;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Mapping;

public class ShoppingLocationMappingProfile : Profile
{
    public ShoppingLocationMappingProfile()
    {
        CreateMap<ShoppingLocation, ShoppingLocationDto>()
            .ForMember(dest => dest.ProductCount,
                opt => opt.MapFrom(src => src.Products != null ? src.Products.Count : 0))
            // IsConnected is computed from TenantIntegrationTokens table (shared tokens per tenant/plugin)
            // The service layer will populate this after mapping
            .ForMember(dest => dest.IsConnected, opt => opt.Ignore());

        CreateMap<CreateShoppingLocationRequest, ShoppingLocation>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Products, opt => opt.Ignore())
            .ForMember(dest => dest.ProductStoreMetadata, opt => opt.Ignore())
            // Map address fields
            .ForMember(dest => dest.StoreAddress, opt => opt.MapFrom(src => src.StoreAddress))
            .ForMember(dest => dest.StorePhone, opt => opt.MapFrom(src => src.StorePhone))
            .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.Latitude))
            .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.Longitude))
            // Map integration fields (PluginId -> IntegrationType)
            .ForMember(dest => dest.IntegrationType, opt => opt.MapFrom(src => src.PluginId))
            .ForMember(dest => dest.ExternalLocationId, opt => opt.MapFrom(src => src.ExternalLocationId))
            .ForMember(dest => dest.ExternalChainId, opt => opt.MapFrom(src => src.ExternalChainId))
            // Ignore OAuth fields (handled by OAuth flow)
            .ForMember(dest => dest.OAuthAccessToken, opt => opt.Ignore())
            .ForMember(dest => dest.OAuthRefreshToken, opt => opt.Ignore())
            .ForMember(dest => dest.OAuthTokenExpiresAt, opt => opt.Ignore())
            // Ignore AisleOrder (handled by separate API endpoint)
            .ForMember(dest => dest.AisleOrder, opt => opt.Ignore());

        CreateMap<UpdateShoppingLocationRequest, ShoppingLocation>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Products, opt => opt.Ignore())
            .ForMember(dest => dest.ProductStoreMetadata, opt => opt.Ignore())
            // Map address fields (editable for all stores)
            .ForMember(dest => dest.StoreAddress, opt => opt.MapFrom(src => src.StoreAddress))
            .ForMember(dest => dest.StorePhone, opt => opt.MapFrom(src => src.StorePhone))
            .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.Latitude))
            .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.Longitude))
            // Ignore integration fields for update (handled by separate API)
            .ForMember(dest => dest.IntegrationType, opt => opt.Ignore())
            .ForMember(dest => dest.ExternalLocationId, opt => opt.Ignore())
            .ForMember(dest => dest.ExternalChainId, opt => opt.Ignore())
            .ForMember(dest => dest.OAuthAccessToken, opt => opt.Ignore())
            .ForMember(dest => dest.OAuthRefreshToken, opt => opt.Ignore())
            .ForMember(dest => dest.OAuthTokenExpiresAt, opt => opt.Ignore())
            // Ignore AisleOrder (handled by separate API endpoint)
            .ForMember(dest => dest.AisleOrder, opt => opt.Ignore());
    }
}
