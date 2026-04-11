#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class AuthenticationMapper
{
    public static UserDto ToDto(User source)
    {
        var dto = ToDtoPartial(source);
        dto.Permissions = source.UserPermissions
            .Select(up => up.Permission.Name)
            .ToList();
        return dto;
    }

    [MapperIgnoreTarget(nameof(UserDto.Permissions))]
    private static partial UserDto ToDtoPartial(User source);
}
