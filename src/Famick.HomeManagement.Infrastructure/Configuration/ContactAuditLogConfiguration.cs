using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class ContactAuditLogConfiguration : IEntityTypeConfiguration<ContactAuditLog>
{
    public void Configure(EntityTypeBuilder<ContactAuditLog> builder)
    {
        builder.ToTable("contact_audit_logs");

        builder.HasKey(cal => cal.Id);

        builder.Property(cal => cal.TenantId)
            .IsRequired();

        builder.HasIndex(cal => cal.TenantId);

        builder.Property(cal => cal.Action)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(cal => cal.OldValues)
            .HasColumnType("jsonb");

        builder.Property(cal => cal.NewValues)
            .HasColumnType("jsonb");

        builder.Property(cal => cal.Description)
            .HasMaxLength(500);

        builder.Property(cal => cal.IpAddress)
            .HasMaxLength(50);

        builder.Property(cal => cal.UserAgent)
            .HasMaxLength(500);

        builder.Property(cal => cal.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes for querying audit logs
        builder.HasIndex(cal => new { cal.ContactId, cal.CreatedAt });
        builder.HasIndex(cal => new { cal.TenantId, cal.CreatedAt });

        // FK to Contact
        builder.HasOne(cal => cal.Contact)
            .WithMany(c => c.AuditLogs)
            .HasForeignKey(cal => cal.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to User
        builder.HasOne(cal => cal.User)
            .WithMany()
            .HasForeignKey(cal => cal.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
