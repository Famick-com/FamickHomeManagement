using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class UserAuditLogConfiguration : IEntityTypeConfiguration<UserAuditLog>
{
    public void Configure(EntityTypeBuilder<UserAuditLog> builder)
    {
        builder.ToTable("user_audit_logs");

        builder.HasKey(ual => ual.Id);

        builder.Property(ual => ual.TenantId)
            .IsRequired();

        builder.Property(ual => ual.Action)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(ual => ual.OldValues)
            .HasColumnType("jsonb");

        builder.Property(ual => ual.NewValues)
            .HasColumnType("jsonb");

        builder.Property(ual => ual.Description)
            .HasMaxLength(500);

        builder.Property(ual => ual.IpAddress)
            .HasMaxLength(45);

        builder.Property(ual => ual.UserAgent)
            .HasMaxLength(512);

        builder.Property(ual => ual.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(ual => new { ual.UserId, ual.CreatedAt });
        builder.HasIndex(ual => new { ual.TenantId, ual.CreatedAt });

        builder.HasOne(ual => ual.User)
            .WithMany()
            .HasForeignKey(ual => ual.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
