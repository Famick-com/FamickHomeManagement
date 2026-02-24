using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.ToTable("user_permissions");

        builder.HasKey(up => up.Id);

        builder.Property(up => up.TenantId)
            .IsRequired();

        builder.HasIndex(up => up.TenantId);

        builder.Property(up => up.UserId)
            .IsRequired();

        builder.Property(up => up.PermissionId)
            .IsRequired();

        // Unique constraint to prevent duplicate permission assignments
        builder.HasIndex(up => new { up.TenantId, up.UserId, up.PermissionId })
            .IsUnique();

        builder.Property(up => up.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Navigation
        builder.HasOne(up => up.User)
            .WithMany(u => u.UserPermissions)
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(up => up.Permission)
            .WithMany(p => p.UserPermissions)
            .HasForeignKey(up => up.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
