using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class MasterProductBarcodeConfiguration : IEntityTypeConfiguration<MasterProductBarcode>
{
    public void Configure(EntityTypeBuilder<MasterProductBarcode> builder)
    {
        builder.ToTable("master_product_barcodes");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(b => b.MasterProductId)
            .HasColumnName("master_product_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(b => b.Barcode)
            .HasColumnName("barcode")
            .HasColumnType("character varying(100)")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.Note)
            .HasColumnName("note")
            .HasColumnType("character varying(500)")
            .HasMaxLength(500);

        builder.Property(b => b.Type2Prefix)
            .HasColumnName("type2_prefix")
            .HasColumnType("character varying(2)")
            .HasMaxLength(2);

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(b => b.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.HasOne(b => b.MasterProduct)
            .WithMany(mp => mp.Barcodes)
            .HasForeignKey(b => b.MasterProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for barcode lookups (unique — one barcode maps to one master product)
        builder.HasIndex(b => b.Barcode)
            .IsUnique()
            .HasDatabaseName("ux_master_product_barcodes_barcode");
    }
}
