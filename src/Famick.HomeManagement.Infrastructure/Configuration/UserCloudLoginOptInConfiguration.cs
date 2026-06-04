using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class UserCloudLoginOptInConfiguration : IEntityTypeConfiguration<UserCloudLoginOptIn>
{
    public void Configure(EntityTypeBuilder<UserCloudLoginOptIn> builder)
    {
        builder.ToTable("user_cloud_login_optins");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(o => o.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(o => o.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(o => o.OptedInAt)
            .HasColumnName("opted_in_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(o => o.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        // One opt-in row per user. Presence = opted-in; absence = opted-out.
        builder.HasIndex(o => o.UserId).IsUnique();
        builder.HasIndex(o => o.TenantId);

        builder.HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
