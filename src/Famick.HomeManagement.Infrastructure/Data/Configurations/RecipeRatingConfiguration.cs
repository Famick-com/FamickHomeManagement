using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class RecipeRatingConfiguration : IEntityTypeConfiguration<RecipeRating>
{
    public void Configure(EntityTypeBuilder<RecipeRating> builder)
    {
        // Columns follow the other recipe tables: PascalCase.
        builder.ToTable("recipe_ratings", t => t.HasCheckConstraint(
            "ck_recipe_ratings_half_stars",
            $"\"HalfStars\" BETWEEN {RecipeRating.MinHalfStars} AND {RecipeRating.MaxHalfStars}"));

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .IsRequired();

        builder.HasIndex(r => r.TenantId);

        builder.Property(r => r.HalfStars)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // One rating per member and recipe. A departed member's ratings have no user, and Postgres
        // keeps nulls distinct, so any number of those can stay.
        builder.HasIndex(r => new { r.RecipeId, r.UserId })
            .IsUnique()
            .HasDatabaseName("ux_recipe_ratings_recipe_user");

        builder.HasIndex(r => r.UserId);

        builder.HasOne(r => r.Recipe)
            .WithMany(rec => rec.Ratings)
            .HasForeignKey(r => r.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        // A member who leaves keeps their rating in the household average, without their name.
        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_recipe_ratings_user");
    }
}
