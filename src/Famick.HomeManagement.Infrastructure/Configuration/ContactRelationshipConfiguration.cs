using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class ContactRelationshipConfiguration : IEntityTypeConfiguration<ContactRelationship>
{
    public void Configure(EntityTypeBuilder<ContactRelationship> builder)
    {
        builder.ToTable("contact_relationships");

        builder.HasKey(cr => cr.Id);

        builder.Property(cr => cr.TenantId)
            .IsRequired();

        builder.HasIndex(cr => cr.TenantId);

        builder.Property(cr => cr.RelationshipType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(cr => cr.CustomLabel)
            .HasMaxLength(100);

        builder.Property(cr => cr.Notes)
            .HasMaxLength(500);

        builder.Property(cr => cr.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint - only one relationship type between two contacts
        builder.HasIndex(cr => new { cr.SourceContactId, cr.TargetContactId, cr.RelationshipType })
            .IsUnique();

        // Indexes for lookups
        builder.HasIndex(cr => cr.SourceContactId);
        builder.HasIndex(cr => cr.TargetContactId);

        // FK to Source Contact
        builder.HasOne(cr => cr.SourceContact)
            .WithMany(c => c.RelationshipsAsSource)
            .HasForeignKey(cr => cr.SourceContactId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to Target Contact
        builder.HasOne(cr => cr.TargetContact)
            .WithMany(c => c.RelationshipsAsTarget)
            .HasForeignKey(cr => cr.TargetContactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
