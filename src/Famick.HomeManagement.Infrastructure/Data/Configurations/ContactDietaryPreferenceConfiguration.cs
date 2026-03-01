using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class ContactDietaryPreferenceConfiguration : IEntityTypeConfiguration<ContactDietaryPreference>
{
    public void Configure(EntityTypeBuilder<ContactDietaryPreference> builder)
    {
        builder.ToTable("contact_dietary_preferences");

        builder.HasKey(dp => dp.Id);

        builder.Property(dp => dp.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(dp => dp.ContactId)
            .HasColumnName("contact_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(dp => dp.DietaryPreference)
            .HasColumnName("dietary_preference")
            .IsRequired();

        builder.Property(dp => dp.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(dp => dp.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint: one dietary preference per contact
        builder.HasIndex(dp => new { dp.ContactId, dp.DietaryPreference })
            .IsUnique()
            .HasDatabaseName("ux_contact_dietary_prefs_contact_pref");

        // Foreign keys
        builder.HasOne(dp => dp.Contact)
            .WithMany(c => c.DietaryPreferences)
            .HasForeignKey(dp => dp.ContactId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_contact_dietary_prefs_contact");
    }
}
