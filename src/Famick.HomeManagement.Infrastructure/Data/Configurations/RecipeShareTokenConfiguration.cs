using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class RecipeShareTokenConfiguration : IEntityTypeConfiguration<RecipeShareToken>
{
    public void Configure(EntityTypeBuilder<RecipeShareToken> builder)
    {
        builder.ToTable("recipe_share_tokens");

        builder.HasKey(st => st.Id);

        builder.Property(st => st.TenantId)
            .IsRequired();

        builder.HasIndex(st => st.TenantId);

        builder.HasIndex(st => st.RecipeId);

        builder.Property(st => st.Token)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(st => st.Token)
            .IsUnique();

        builder.Property(st => st.ExpiresAt)
            .IsRequired();

        builder.Property(st => st.IsRevoked)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(st => st.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
