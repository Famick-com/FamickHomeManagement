using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class RecipeNestingConfiguration : IEntityTypeConfiguration<RecipeNesting>
{
    public void Configure(EntityTypeBuilder<RecipeNesting> builder)
    {
        builder.ToTable("recipe_nestings");

        builder.HasKey(rn => rn.Id);

        builder.Property(rn => rn.TenantId)
            .IsRequired();

        builder.HasIndex(rn => rn.TenantId);

        builder.HasIndex(rn => new { rn.RecipeId, rn.IncludesRecipeId })
            .IsUnique();

        builder.Property(rn => rn.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
