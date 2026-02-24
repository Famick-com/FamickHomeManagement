using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class RecipeStepConfiguration : IEntityTypeConfiguration<RecipeStep>
{
    public void Configure(EntityTypeBuilder<RecipeStep> builder)
    {
        builder.ToTable("recipe_steps");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId)
            .IsRequired();

        builder.HasIndex(s => s.TenantId);

        builder.HasIndex(s => s.RecipeId);

        builder.Property(s => s.StepOrder)
            .IsRequired();

        builder.HasIndex(s => new { s.RecipeId, s.StepOrder })
            .IsUnique();

        builder.Property(s => s.Title)
            .HasMaxLength(200);

        builder.Property(s => s.Description)
            .HasMaxLength(2000);

        builder.Property(s => s.Instructions)
            .IsRequired()
            .HasMaxLength(10000);

        builder.Property(s => s.ImageFileName)
            .HasMaxLength(255);

        builder.Property(s => s.ImageOriginalFileName)
            .HasMaxLength(255);

        builder.Property(s => s.ImageContentType)
            .HasMaxLength(100);

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Navigation: Ingredients (cascade)
        builder.HasMany(s => s.Ingredients)
            .WithOne(p => p.RecipeStep)
            .HasForeignKey(p => p.RecipeStepId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
