using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Data.Configurations;

public class ContactAllergenConfiguration : IEntityTypeConfiguration<ContactAllergen>
{
    public void Configure(EntityTypeBuilder<ContactAllergen> builder)
    {
        builder.ToTable("contact_allergens");

        builder.HasKey(ca => ca.Id);

        builder.Property(ca => ca.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(ca => ca.ContactId)
            .HasColumnName("contact_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(ca => ca.AllergenType)
            .HasColumnName("allergen_type")
            .IsRequired();

        builder.Property(ca => ca.Severity)
            .HasColumnName("severity")
            .IsRequired();

        builder.Property(ca => ca.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(ca => ca.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint: one allergen type per contact
        builder.HasIndex(ca => new { ca.ContactId, ca.AllergenType })
            .IsUnique()
            .HasDatabaseName("ux_contact_allergens_contact_type");

        // Foreign keys
        builder.HasOne(ca => ca.Contact)
            .WithMany(c => c.Allergens)
            .HasForeignKey(ca => ca.ContactId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_contact_allergens_contact");
    }
}
