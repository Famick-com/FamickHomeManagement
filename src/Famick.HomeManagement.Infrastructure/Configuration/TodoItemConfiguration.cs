using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class TodoItemConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        builder.ToTable("todo_items");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenantId)
            .IsRequired();

        builder.HasIndex(t => t.TenantId);

        builder.Property(t => t.TaskType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.DateEntered)
            .IsRequired();

        builder.Property(t => t.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.RelatedEntityId);

        builder.Property(t => t.RelatedEntityType)
            .HasMaxLength(100);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.AdditionalData)
            .HasColumnType("jsonb");

        builder.Property(t => t.IsCompleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.CompletedAt);

        builder.Property(t => t.CompletedBy)
            .HasMaxLength(100);

        builder.Property(t => t.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(t => t.UpdatedAt);

        // Indexes for common queries
        builder.HasIndex(t => new { t.TenantId, t.IsCompleted });
        builder.HasIndex(t => new { t.TenantId, t.TaskType });
        builder.HasIndex(t => new { t.TenantId, t.RelatedEntityId });
    }
}
