using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class UserJwtMinIatConfiguration : IEntityTypeConfiguration<UserJwtMinIat>
{
    public void Configure(EntityTypeBuilder<UserJwtMinIat> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MinIat)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique on UserId — at most one row per user. Lookups are by UserId on the
        // hot path of every authenticated request, so the unique index doubles as the
        // covering index for that query.
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.TenantId);

        builder.HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<UserJwtMinIat>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
