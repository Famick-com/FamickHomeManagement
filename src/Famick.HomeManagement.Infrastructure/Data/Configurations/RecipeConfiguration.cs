using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("recipes");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .IsRequired();

        builder.HasIndex(r => r.TenantId);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(r => new { r.TenantId, r.Name });

        builder.Property(r => r.Source)
            .HasMaxLength(2000);

        builder.Property(r => r.Servings)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(r => r.Attribution)
            .HasMaxLength(1000);

        builder.Property(r => r.IsMeal)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(r => r.CreatedByContactId);

        // Navigation: CreatedByContact
        builder.HasOne(r => r.CreatedByContact)
            .WithMany()
            .HasForeignKey(r => r.CreatedByContactId)
            .OnDelete(DeleteBehavior.SetNull);

        // Navigation: Steps (cascade)
        builder.HasMany(r => r.Steps)
            .WithOne(s => s.Recipe)
            .HasForeignKey(s => s.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Navigation: Images (cascade)
        builder.HasMany(r => r.Images)
            .WithOne(i => i.Recipe)
            .HasForeignKey(i => i.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Navigation: NestedRecipes (self-referential)
        builder.HasMany(r => r.NestedRecipes)
            .WithOne(rn => rn.Recipe)
            .HasForeignKey(rn => rn.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Navigation: ParentRecipes (self-referential)
        builder.HasMany(r => r.ParentRecipes)
            .WithOne(rn => rn.IncludedRecipe)
            .HasForeignKey(rn => rn.IncludesRecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Navigation: ShareTokens (cascade)
        builder.HasMany(r => r.ShareTokens)
            .WithOne(st => st.Recipe)
            .HasForeignKey(st => st.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
