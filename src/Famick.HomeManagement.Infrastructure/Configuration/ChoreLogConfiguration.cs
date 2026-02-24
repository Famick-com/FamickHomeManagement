using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration
{
    public class ChoreLogConfiguration : IEntityTypeConfiguration<ChoreLog>
    {
        public void Configure(EntityTypeBuilder<ChoreLog> builder)
        {
            builder.ToTable("chores_log");

            builder.HasKey(cl => cl.Id);

            builder.Property(cl => cl.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(cl => cl.TenantId)
                .HasColumnName("tenant_id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(cl => cl.ChoreId)
                .HasColumnName("chore_id")
                .HasColumnType("uuid")
                .IsRequired();

            builder.Property(cl => cl.TrackedTime)
                .HasColumnName("tracked_time")
                .HasColumnType("timestamp with time zone");

            builder.Property(cl => cl.DoneByUserId)
                .HasColumnName("done_by_user_id")
                .HasColumnType("uuid");

            builder.Property(cl => cl.Undone)
                .HasColumnName("undone")
                .HasColumnType("smallint")
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(cl => cl.UndoneTimestamp)
                .HasColumnName("undone_timestamp")
                .HasColumnType("timestamp with time zone");

            builder.Property(cl => cl.Skipped)
                .HasColumnName("skipped")
                .HasColumnType("smallint")
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(cl => cl.ScheduledExecutionTime)
                .HasColumnName("scheduled_execution_time")
                .HasColumnType("timestamp with time zone");

            builder.Property(cl => cl.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(cl => cl.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            builder.HasIndex(cl => cl.TenantId)
                .HasDatabaseName("ix_chores_log_tenant_id");

            builder.HasIndex(cl => cl.ChoreId)
                .HasDatabaseName("ix_chores_log_chore_id");

            builder.HasIndex(cl => cl.DoneByUserId)
                .HasDatabaseName("ix_chores_log_done_by_user_id");

            // Foreign keys
            builder.HasOne(cl => cl.Chore)
                .WithMany(c => c.LogEntries)
                .HasForeignKey(cl => cl.ChoreId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_chores_log_chores");

            builder.HasOne(cl => cl.DoneByUser)
                .WithMany()
                .HasForeignKey(cl => cl.DoneByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_chores_log_users");
        }
    }
}
