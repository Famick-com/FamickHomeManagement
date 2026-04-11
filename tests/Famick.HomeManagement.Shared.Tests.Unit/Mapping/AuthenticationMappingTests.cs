using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class AuthenticationMappingTests
{
    private readonly IMapper _mapper;

    public AuthenticationMappingTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<AuthenticationMappingProfile>();
        }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        // Validation skipped: profiles are tested in isolation
        _mapper = config.CreateMapper();
    }

    [Fact]
    public void User_To_UserDto_MapsBasicProperties()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            UserPermissions = new List<UserPermission>()
        };

        var dto = _mapper.Map<UserDto>(user);

        dto.Id.Should().Be(user.Id);
        dto.Email.Should().Be("test@example.com");
        dto.FirstName.Should().Be("John");
        dto.LastName.Should().Be("Doe");
    }

    [Fact]
    public void User_To_UserDto_MapsPermissions()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            FirstName = "Admin",
            LastName = "User",
            UserPermissions = new List<UserPermission>
            {
                new() { Permission = new Permission { Name = "Admin" } },
                new() { Permission = new Permission { Name = "Editor" } }
            }
        };

        var dto = _mapper.Map<UserDto>(user);

        dto.Permissions.Should().HaveCount(2);
        dto.Permissions.Should().Contain("Admin");
        dto.Permissions.Should().Contain("Editor");
    }

    [Fact]
    public void User_To_UserDto_HandlesEmptyPermissions()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "viewer@example.com",
            FirstName = "View",
            LastName = "Only",
            UserPermissions = new List<UserPermission>()
        };

        var dto = _mapper.Map<UserDto>(user);

        dto.Permissions.Should().BeEmpty();
    }
}
