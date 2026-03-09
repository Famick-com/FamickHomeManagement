using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class TenantProductOnboardingStateConfiguration : IEntityTypeConfiguration<TenantProductOnboardingState>
{
    public void Configure(EntityTypeBuilder<TenantProductOnboardingState> builder)
    {
        builder.ToTable("tenant_product_onboarding_states");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.HasCompletedOnboarding)
            .HasColumnName("has_completed_onboarding")
            .HasColumnType("boolean")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.QuestionnaireAnswersJson)
            .HasColumnName("questionnaire_answers_json")
            .HasColumnType("jsonb");

        builder.Property(s => s.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(s => s.ProductsCreatedCount)
            .HasColumnName("products_created_count")
            .HasColumnType("integer")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        // One record per tenant
        builder.HasIndex(s => s.TenantId)
            .IsUnique()
            .HasDatabaseName("ux_tenant_product_onboarding_states_tenant_id");
    }
}
