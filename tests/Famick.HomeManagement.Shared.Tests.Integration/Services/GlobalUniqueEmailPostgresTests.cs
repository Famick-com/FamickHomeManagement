using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// Issue #122: the database itself holds a sign-in email to one person across every household,
/// and to one spelling, whatever code path writes the row.
/// </summary>
public class GlobalUniqueEmailPostgresTests(PostgresContainerFixture fixture)
    : IClassFixture<PostgresContainerFixture>
{
    [Fact]
    public async Task The_same_email_cannot_be_a_user_in_two_households()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        await AddUserAsync(Guid.NewGuid(), email);

        var second = () => AddUserAsync(Guid.NewGuid(), email);

        var thrown = await second.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Theory]
    [InlineData("Ann@Example.com")]
    [InlineData(" ann@example.com")]
    public async Task An_email_that_is_not_normalised_is_refused(string spelling)
    {
        var email = spelling.Replace("ann", $"ann{Guid.NewGuid():N}", StringComparison.OrdinalIgnoreCase);

        var add = () => AddUserAsync(Guid.NewGuid(), email);

        var thrown = await add.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task An_invitations_roles_round_trip()
    {
        var id = Guid.NewGuid();
        await using (var db = fixture.CreateDbContext())
        {
            db.HouseholdInvitations.Add(new HouseholdInvitation
            {
                Id = id, TenantId = Guid.NewGuid(), Email = "invited@example.com", FirstName = "In", LastName = "Vited",
                Roles = [Role.Admin, Role.Viewer], TokenHash = Guid.NewGuid().ToString("N"),
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
            await db.SaveChangesAsync();
        }

        await using var check = fixture.CreateDbContext();
        (await check.HouseholdInvitations.IgnoreQueryFilters().SingleAsync(i => i.Id == id))
            .Roles.Should().Equal(Role.Admin, Role.Viewer);
    }

    private async Task AddUserAsync(Guid tenantId, string email)
    {
        await using var db = fixture.CreateDbContext();
        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId))
        {
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "Household" });
        }

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Email = email, Username = email,
            FirstName = "Test", LastName = "User", PasswordHash = "x", IsActive = true
        });
        await db.SaveChangesAsync();
    }
}
