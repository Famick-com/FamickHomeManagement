using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class UserDeviceTokenConfiguration : IEntityTypeConfiguration<UserDeviceToken>
{
    public void Configure(EntityTypeBuilder<UserDeviceToken> builder)
    {
        builder.ToTable("user_device_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenantId)
            .IsRequired();

        builder.HasIndex(t => t.TenantId);

        builder.Property(t => t.UserId)
            .IsRequired();

        builder.Property(t => t.Token)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.Platform)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(t => new { t.TenantId, t.UserId })
            .HasDatabaseName("ix_user_device_tokens_tenant_user");

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
