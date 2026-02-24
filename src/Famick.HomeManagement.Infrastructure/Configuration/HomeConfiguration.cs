using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Famick.HomeManagement.Infrastructure.Configuration;

public class HomeConfiguration : IEntityTypeConfiguration<Home>
{
    public void Configure(EntityTypeBuilder<Home> builder)
    {
        builder.ToTable("homes");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(h => h.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        // Property Basics
        builder.Property(h => h.Unit)
            .HasColumnName("unit")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50);

        builder.Property(h => h.YearBuilt)
            .HasColumnName("year_built")
            .HasColumnType("integer");

        builder.Property(h => h.SquareFootage)
            .HasColumnName("square_footage")
            .HasColumnType("integer");

        builder.Property(h => h.Bedrooms)
            .HasColumnName("bedrooms")
            .HasColumnType("integer");

        builder.Property(h => h.Bathrooms)
            .HasColumnName("bathrooms")
            .HasColumnType("numeric(3,1)");

        builder.Property(h => h.HoaName)
            .HasColumnName("hoa_name")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(h => h.HoaContactInfo)
            .HasColumnName("hoa_contact_info")
            .HasColumnType("text");

        builder.Property(h => h.HoaRulesLink)
            .HasColumnName("hoa_rules_link")
            .HasColumnType("character varying(1000)")
            .HasMaxLength(1000);

        // HVAC
        builder.Property(h => h.AcFilterSizes)
            .HasColumnName("ac_filter_sizes")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        // Maintenance & Consumables
        builder.Property(h => h.AcFilterReplacementIntervalDays)
            .HasColumnName("ac_filter_replacement_interval_days")
            .HasColumnType("integer");

        builder.Property(h => h.FridgeWaterFilterType)
            .HasColumnName("fridge_water_filter_type")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(h => h.UnderSinkFilterType)
            .HasColumnName("under_sink_filter_type")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(h => h.WholeHouseFilterType)
            .HasColumnName("whole_house_filter_type")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(h => h.SmokeCoDetectorBatteryType)
            .HasColumnName("smoke_co_detector_battery_type")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50);

        builder.Property(h => h.HvacServiceSchedule)
            .HasColumnName("hvac_service_schedule")
            .HasColumnType("text");

        builder.Property(h => h.PestControlSchedule)
            .HasColumnName("pest_control_schedule")
            .HasColumnType("text");

        // Insurance & Financial
        builder.Property(h => h.InsuranceType)
            .HasColumnName("insurance_type")
            .HasColumnType("integer");

        builder.Property(h => h.InsurancePolicyNumber)
            .HasColumnName("insurance_policy_number")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(h => h.InsuranceAgentName)
            .HasColumnName("insurance_agent_name")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(h => h.InsuranceAgentPhone)
            .HasColumnName("insurance_agent_phone")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50);

        builder.Property(h => h.InsuranceAgentEmail)
            .HasColumnName("insurance_agent_email")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(h => h.MortgageInfo)
            .HasColumnName("mortgage_info")
            .HasColumnType("text");

        builder.Property(h => h.PropertyTaxAccountNumber)
            .HasColumnName("property_tax_account_number")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(h => h.EscrowDetails)
            .HasColumnName("escrow_details")
            .HasColumnType("text");

        builder.Property(h => h.AppraisalValue)
            .HasColumnName("appraisal_value")
            .HasColumnType("numeric(18,2)");

        builder.Property(h => h.AppraisalDate)
            .HasColumnName("appraisal_date")
            .HasColumnType("timestamp with time zone");

        // Setup Status
        builder.Property(h => h.IsSetupComplete)
            .HasColumnName("is_setup_complete")
            .HasColumnType("boolean")
            .IsRequired()
            .HasDefaultValue(false);

        // Audit timestamps
        builder.Property(h => h.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(h => h.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Indexes
        builder.HasIndex(h => h.TenantId)
            .HasDatabaseName("ix_homes_tenant_id");

        // Unique constraint: one home per tenant
        builder.HasIndex(h => h.TenantId)
            .IsUnique()
            .HasDatabaseName("ux_homes_tenant_id");

        // Navigation properties
        builder.HasMany(h => h.Utilities)
            .WithOne(u => u.Home)
            .HasForeignKey(u => u.HomeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
