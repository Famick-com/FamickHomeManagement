using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class HouseholdDataTransferConfiguration : IEntityTypeConfiguration<HouseholdDataTransfer>
{
    public void Configure(EntityTypeBuilder<HouseholdDataTransfer> builder)
    {
        builder.ToTable("household_data_transfers");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").HasColumnType("uuid").IsRequired();
        builder.Property(t => t.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();

        // Stored as text rather than an int: a migration that reorders an enum silently
        // reinterprets every historical row, and these rows outlive the run they describe.
        builder.Property(t => t.Kind).HasColumnName("kind").HasConversion<string>()
            .HasColumnType("character varying(20)").HasMaxLength(20).IsRequired();
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<string>()
            .HasColumnType("character varying(20)").HasMaxLength(20).IsRequired();
        builder.Property(t => t.ChangedSincePolicy).HasColumnName("changed_since_policy")
            .HasConversion<string>().HasColumnType("character varying(20)").HasMaxLength(20).IsRequired();

        builder.Property(t => t.RequestedByUserId).HasColumnName("requested_by_user_id")
            .HasColumnType("uuid").IsRequired();

        builder.Property(t => t.StartedAt).HasColumnName("started_at").HasColumnType("timestamp with time zone");
        builder.Property(t => t.HeartbeatAt).HasColumnName("heartbeat_at").HasColumnType("timestamp with time zone");
        builder.Property(t => t.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone");

        builder.Property(t => t.IncludeFiles).HasColumnName("include_files").IsRequired();

        builder.Property(t => t.ArchiveFileName).HasColumnName("archive_file_name")
            .HasColumnType("character varying(255)").HasMaxLength(255);
        builder.Property(t => t.ArchiveBytes).HasColumnName("archive_bytes");
        builder.Property(t => t.ArchiveSha256).HasColumnName("archive_sha256")
            .HasColumnType("character varying(64)").HasMaxLength(64);
        builder.Property(t => t.ManifestJson).HasColumnName("manifest_json").HasColumnType("jsonb");

        builder.Property(t => t.UploadFileName).HasColumnName("upload_file_name")
            .HasColumnType("character varying(255)").HasMaxLength(255);
        builder.Property(t => t.UploadBytes).HasColumnName("upload_bytes");

        builder.Property(t => t.ProgressLabel).HasColumnName("progress_label")
            .HasColumnType("character varying(200)").HasMaxLength(200);
        builder.Property(t => t.ProgressCurrent).HasColumnName("progress_current").IsRequired();
        builder.Property(t => t.ProgressTotal).HasColumnName("progress_total").IsRequired();

        builder.Property(t => t.ErrorCode).HasColumnName("error_code")
            .HasColumnType("character varying(64)").HasMaxLength(64);
        builder.Property(t => t.ErrorMessage).HasColumnName("error_message").HasColumnType("text");

        builder.Property(t => t.CreatedAt).HasColumnName("created_at")
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.HasOne(t => t.RequestedByUser)
            .WithMany()
            .HasForeignKey(t => t.RequestedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // "Is one already running for this household?" — asked on every request that starts one.
        builder.HasIndex(t => new { t.TenantId, t.Kind, t.Status });

        // Finding this household's expired archives to clean up.
        builder.HasIndex(t => new { t.TenantId, t.ExpiresAt });
    }
}
