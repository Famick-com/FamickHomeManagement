using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("addresses");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(a => a.AddressLine1)
            .HasColumnName("address_line_1")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(a => a.AddressLine2)
            .HasColumnName("address_line_2")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(a => a.AddressLine3)
            .HasColumnName("address_line_3")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(a => a.AddressLine4)
            .HasColumnName("address_line_4")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(a => a.City)
            .HasColumnName("city")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(a => a.StateProvince)
            .HasColumnName("state_province")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(a => a.PostalCode)
            .HasColumnName("postal_code")
            .HasColumnType("character varying(20)")
            .HasMaxLength(20);

        builder.Property(a => a.Country)
            .HasColumnName("country")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(a => a.CountryCode)
            .HasColumnName("country_code")
            .HasColumnType("character varying(2)")
            .HasMaxLength(2);

        builder.Property(a => a.Latitude)
            .HasColumnName("latitude")
            .HasColumnType("double precision");

        builder.Property(a => a.Longitude)
            .HasColumnName("longitude")
            .HasColumnType("double precision");

        builder.Property(a => a.GeoapifyPlaceId)
            .HasColumnName("geoapify_place_id")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(a => a.FormattedAddress)
            .HasColumnName("formatted_address")
            .HasColumnType("character varying(500)")
            .HasMaxLength(500);

        builder.Property(a => a.NormalizedHash)
            .HasColumnName("normalized_hash")
            .HasColumnType("character varying(64)")
            .HasMaxLength(64);

        // Audit timestamps
        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        // Index for duplicate detection
        builder.HasIndex(a => a.NormalizedHash)
            .HasDatabaseName("ix_addresses_normalized_hash");

        // Index for geolocation queries
        builder.HasIndex(a => new { a.Latitude, a.Longitude })
            .HasDatabaseName("ix_addresses_lat_lng")
            .HasFilter("latitude IS NOT NULL AND longitude IS NOT NULL");
    }
}
