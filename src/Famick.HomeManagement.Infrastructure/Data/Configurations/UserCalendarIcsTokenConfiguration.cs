using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class UserCalendarIcsTokenConfiguration : IEntityTypeConfiguration<UserCalendarIcsToken>
{
    public void Configure(EntityTypeBuilder<UserCalendarIcsToken> builder)
    {
        builder.ToTable("user_calendar_ics_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(t => t.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(t => t.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(t => t.Token)
            .HasColumnName("token")
            .HasColumnType("character varying(128)")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(t => t.IsRevoked)
            .HasColumnName("is_revoked")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.Label)
            .HasColumnName("label")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique index on token for fast lookup
        builder.HasIndex(t => t.Token)
            .IsUnique()
            .HasDatabaseName("ix_user_calendar_ics_tokens_token");

        builder.HasIndex(t => t.TenantId)
            .HasDatabaseName("ix_user_calendar_ics_tokens_tenant_id");

        builder.HasIndex(t => new { t.TenantId, t.UserId })
            .HasDatabaseName("ix_user_calendar_ics_tokens_tenant_user");

        // Foreign key
        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_calendar_ics_tokens_user");
    }
}
