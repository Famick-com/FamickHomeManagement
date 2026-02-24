using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Mapping;

/// <summary>
/// AutoMapper profile for authentication-related entity-to-DTO mappings
/// </summary>
public class AuthenticationMappingProfile : Profile
{
    public AuthenticationMappingProfile()
    {
        // User -> UserDto
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.Permissions,
                opt => opt.MapFrom(src => src.UserPermissions
                    .Select(up => up.Permission.Name)
                    .ToList()));

        // NOTE: Tenant -> TenantDto mapping is cloud-specific and moved to
        // CloudAuthenticationMappingProfile in homemanagement-cloud project
    }
}
