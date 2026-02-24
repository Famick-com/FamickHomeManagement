using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class HomeUtilityConfiguration : IEntityTypeConfiguration<HomeUtility>
{
    public void Configure(EntityTypeBuilder<HomeUtility> builder)
    {
        builder.ToTable("home_utilities");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(u => u.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(u => u.HomeId)
            .HasColumnName("home_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(u => u.UtilityType)
            .HasColumnName("utility_type")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(u => u.CompanyName)
            .HasColumnName("company_name")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(u => u.AccountNumber)
            .HasColumnName("account_number")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(u => u.PhoneNumber)
            .HasColumnName("phone_number")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50);

        builder.Property(u => u.Website)
            .HasColumnName("website")
            .HasColumnType("character varying(500)")
            .HasMaxLength(500);

        builder.Property(u => u.LoginEmail)
            .HasColumnName("login_email")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(u => u.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        // Audit timestamps
        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(u => u.TenantId)
            .HasDatabaseName("ix_home_utilities_tenant_id");

        builder.HasIndex(u => u.HomeId)
            .HasDatabaseName("ix_home_utilities_home_id");

        // Unique constraint: one utility of each type per home
        builder.HasIndex(u => new { u.HomeId, u.UtilityType })
            .IsUnique()
            .HasDatabaseName("ux_home_utilities_home_type");

        // Foreign key
        builder.HasOne(u => u.Home)
            .WithMany(h => h.Utilities)
            .HasForeignKey(u => u.HomeId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_home_utilities_homes");
    }
}
