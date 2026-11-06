using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class RecipeImportJobConfiguration : IEntityTypeConfiguration<RecipeImportJob>
{
    public void Configure(EntityTypeBuilder<RecipeImportJob> builder)
    {
        builder.ToTable("recipe_import_jobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id).HasColumnName("id").HasColumnType("uuid").IsRequired();
        builder.Property(j => j.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(j => j.UserId).HasColumnName("user_id").HasColumnType("uuid");

        builder.Property(j => j.Url).HasColumnName("url")
            .HasColumnType("character varying(2000)").HasMaxLength(2000).IsRequired();
        builder.Property(j => j.AllowDuplicate).HasColumnName("allow_duplicate").IsRequired();

        // Text rather than an int, as for data transfers: reordering the enum can't reinterpret old rows.
        builder.Property(j => j.Status).HasColumnName("status").HasConversion<string>()
            .HasColumnType("character varying(20)").HasMaxLength(20).IsRequired();
        builder.Property(j => j.Version).HasColumnName("version").HasColumnType("uuid").IsRequired()
            .IsConcurrencyToken();

        builder.Property(j => j.RecipeId).HasColumnName("recipe_id").HasColumnType("uuid");
        builder.Property(j => j.IngredientsJson).HasColumnName("ingredients_json").HasColumnType("jsonb");
        builder.Property(j => j.ErrorCode).HasColumnName("error_code")
            .HasColumnType("character varying(64)").HasMaxLength(64);
        builder.Property(j => j.ErrorMessage).HasColumnName("error_message")
            .HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(j => j.ExistingRecipeId).HasColumnName("existing_recipe_id").HasColumnType("uuid");

        builder.Property(j => j.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");
        builder.Property(j => j.CreatedAt).HasColumnName("created_at")
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(j => j.UpdatedAt).HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        // Old jobs are cleared per household by age.
        builder.HasIndex(j => new { j.TenantId, j.CreatedAt }).HasDatabaseName("ix_recipe_import_jobs_tenant_id_created_at");
    }
}
