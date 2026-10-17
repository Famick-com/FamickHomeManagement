using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class HouseholdInvitationConfiguration : IEntityTypeConfiguration<HouseholdInvitation>
{
    public void Configure(EntityTypeBuilder<HouseholdInvitation> builder)
    {
        builder.ToTable("household_invitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Email).IsRequired().HasMaxLength(255);
        builder.Property(i => i.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(i => i.LastName).IsRequired().HasMaxLength(100);
        builder.Property(i => i.TokenHash).IsRequired().HasMaxLength(100);

        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => new { i.TenantId, i.Email });
    }
}
