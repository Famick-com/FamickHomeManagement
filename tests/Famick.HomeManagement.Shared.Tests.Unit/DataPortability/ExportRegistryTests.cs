using System.Text.RegularExpressions;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.DataPortability;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.DataPortability;

/// <summary>
/// Guards what an archive is allowed to contain.
/// </summary>
public class ExportRegistryTests
{
    /// <summary>
    /// Every tenant-scoped entity type and the disposition it resolves to.
    /// </summary>
    /// <remarks>
    /// Pinned rather than computed, and that is the whole point. Adding an entity fails this test,
    /// which forces somebody to decide whether a household's rows of it belong in a file the user
    /// downloads — instead of finding out later, from the wrong end.
    /// </remarks>
    private static readonly Dictionary<string, ExportDisposition> Pinned = new()
    {
        // Withheld: authenticates somebody.
        ["AuthProxyPairingConfig"] = ExportDisposition.ExcludeSecret,
        ["PasswordResetToken"] = ExportDisposition.ExcludeSecret,
        ["RecipeShareToken"] = ExportDisposition.ExcludeSecret,
        ["RefreshToken"] = ExportDisposition.ExcludeSecret,
        ["TenantIntegrationToken"] = ExportDisposition.ExcludeSecret,
        ["UserCalendarIcsToken"] = ExportDisposition.ExcludeSecret,
        ["UserContactVcfToken"] = ExportDisposition.ExcludeSecret,
        ["UserDeviceToken"] = ExportDisposition.ExcludeSecret,
        ["UserExternalLogin"] = ExportDisposition.ExcludeSecret,
        ["UserJwtMinIat"] = ExportDisposition.ExcludeSecret,
        ["UserPasskeyCredential"] = ExportDisposition.ExcludeSecret,

        // Withheld: belongs to the deployment or the account, not the household's records.
        ["ContactUserShare"] = ExportDisposition.ExcludeSystem,
        ["ExternalCalendarEvent"] = ExportDisposition.ExcludeSystem,
        ["ExternalCalendarSubscription"] = ExportDisposition.ExcludeSystem,
        ["HouseholdDataTransfer"] = ExportDisposition.ExcludeSystem,
        ["HouseholdDataTransferItem"] = ExportDisposition.ExcludeSystem,
        ["Notification"] = ExportDisposition.ExcludeSystem,
        ["NotificationPreference"] = ExportDisposition.ExcludeSystem,
        ["UserCloudLoginOptIn"] = ExportDisposition.ExcludeSystem,
        ["UserMealPlannerPreference"] = ExportDisposition.ExcludeSystem,
        ["UserMealPlannerTip"] = ExportDisposition.ExcludeSystem,
        ["UserPermission"] = ExportDisposition.ExcludeSystem,
        ["UserRole"] = ExportDisposition.ExcludeSystem,

        // The household's own records.
        ["BatchCookItem"] = ExportDisposition.Export,
        ["BatchCookItemUsage"] = ExportDisposition.Export,
        ["CalendarEvent"] = ExportDisposition.Export,
        ["CalendarEventException"] = ExportDisposition.Export,
        ["CalendarEventMember"] = ExportDisposition.Export,
        ["Chore"] = ExportDisposition.Export,
        ["ChoreLog"] = ExportDisposition.Export,
        ["Contact"] = ExportDisposition.Export,
        ["ContactAddress"] = ExportDisposition.Export,
        ["ContactAllergen"] = ExportDisposition.Export,
        ["ContactAuditLog"] = ExportDisposition.Export,
        ["ContactDietaryPreference"] = ExportDisposition.Export,
        ["ContactEmailAddress"] = ExportDisposition.Export,
        ["ContactPhoneNumber"] = ExportDisposition.Export,
        ["ContactRelationship"] = ExportDisposition.Export,
        ["ContactSocialMedia"] = ExportDisposition.Export,
        ["ContactTag"] = ExportDisposition.Export,
        ["ContactTagLink"] = ExportDisposition.Export,
        ["Equipment"] = ExportDisposition.Export,
        ["EquipmentCategory"] = ExportDisposition.Export,
        ["EquipmentDocument"] = ExportDisposition.Export,
        ["EquipmentDocumentTag"] = ExportDisposition.Export,
        ["EquipmentMaintenanceRecord"] = ExportDisposition.Export,
        ["EquipmentUsageLog"] = ExportDisposition.Export,
        ["Home"] = ExportDisposition.Export,
        ["HomeUtility"] = ExportDisposition.Export,
        ["Location"] = ExportDisposition.Export,
        ["Meal"] = ExportDisposition.Export,
        ["MealItem"] = ExportDisposition.Export,
        ["MealPlan"] = ExportDisposition.Export,
        ["MealPlanEntry"] = ExportDisposition.Export,
        ["MealType"] = ExportDisposition.Export,
        ["Product"] = ExportDisposition.Export,
        ["ProductAllergen"] = ExportDisposition.Export,
        ["ProductBarcode"] = ExportDisposition.Export,
        ["ProductDietaryConflict"] = ExportDisposition.Export,
        ["ProductGroup"] = ExportDisposition.Export,
        ["ProductImage"] = ExportDisposition.Export,
        ["ProductNutrition"] = ExportDisposition.Export,
        ["ProductStoreMetadata"] = ExportDisposition.Export,
        ["PropertyLink"] = ExportDisposition.Export,
        ["QuantityUnit"] = ExportDisposition.Export,
        ["Recipe"] = ExportDisposition.Export,
        ["RecipeImage"] = ExportDisposition.Export,
        ["RecipeNesting"] = ExportDisposition.Export,
        ["RecipePosition"] = ExportDisposition.Export,
        ["RecipeStep"] = ExportDisposition.Export,
        ["ShoppingList"] = ExportDisposition.Export,
        ["ShoppingListItem"] = ExportDisposition.Export,
        ["ShoppingLocation"] = ExportDisposition.Export,
        ["StockEntry"] = ExportDisposition.Export,
        ["StockLog"] = ExportDisposition.Export,
        ["StorageBin"] = ExportDisposition.Export,
        ["StorageBinPhoto"] = ExportDisposition.Export,
        ["TodoItem"] = ExportDisposition.Export,
        ["User"] = ExportDisposition.Export,
        ["UserAuditLog"] = ExportDisposition.Export,
        ["Vehicle"] = ExportDisposition.Export,
        ["VehicleDocument"] = ExportDisposition.Export,
        ["VehicleMaintenanceRecord"] = ExportDisposition.Export,
        ["VehicleMaintenanceSchedule"] = ExportDisposition.Export,
        ["VehicleMileageLog"] = ExportDisposition.Export,
    };

    [Fact]
    public void EveryTenantScopedEntityTypeHasAPinnedDisposition()
    {
        using var context = RelationalModelContext();

        var actual = TenantReachability.TenantScopedEntityTypes(context.Model)
            .ToDictionary(t => t.ClrType.Name, t => ExportRegistry.For(t.ClrType).Export);

        var added = actual.Keys.Except(Pinned.Keys).ToList();
        var removed = Pinned.Keys.Except(actual.Keys).ToList();

        added.Should().BeEmpty(
            "a new household entity must be looked at before it can travel in a file the user " +
            "downloads. Decide in ExportRegistry, then pin it here. New: {0}",
            string.Join(", ", added));

        removed.Should().BeEmpty(
            "these are pinned but no longer in the model; a stale pin hides the next entity that " +
            "takes the name. Removed: {0}", string.Join(", ", removed));

        actual.Should().BeEquivalentTo(Pinned,
            "a disposition changed. That is a decision about what leaves the building, so it " +
            "should be a deliberate edit here rather than a surprise.");
    }

    [Fact]
    public void NoExportedColumnLooksLikeASecret()
    {
        using var context = RelationalModelContext();

        // Catches the case the type-level registry cannot: a sensitive column added to a table
        // that already exports. This is the guard that survives the model changing under it.
        var suspicious = new Regex(
            "password|secret|token|credential|apikey|api_key|privatekey|private_key|salt|passphrase",
            RegexOptions.IgnoreCase);

        var findings = new List<string>();

        foreach (var entityType in TenantReachability.TenantScopedEntityTypes(context.Model))
        {
            if (ExportRegistry.For(entityType.ClrType).Export != ExportDisposition.Export) continue;

            var excluded = ExportRegistry.ColumnsExcludedFrom(entityType.ClrType);

            foreach (var property in entityType.GetProperties())
            {
                if (!suspicious.IsMatch(property.Name)) continue;
                if (excluded.Contains(property.Name)) continue;
                if (ExportRegistry.KnownSafeColumnNames.ContainsKey(property.Name)) continue;

                findings.Add($"{entityType.ClrType.Name}.{property.Name}");
            }
        }

        findings.Should().BeEmpty(
            "these columns are named like credentials and would travel in the archive. Either " +
            "exclude the column in ExportRegistry, or add the name to KnownSafeColumnNames with " +
            "the reason it is harmless. Found: {0}", string.Join(", ", findings));
    }

    [Fact]
    public void ExcludedColumnsResolveToRealProperties()
    {
        using var context = RelationalModelContext();

        foreach (var (clrType, columns) in ExportRegistry.AllExcludedColumns)
        {
            var entityType = context.Model.FindEntityType(clrType);
            entityType.Should().NotBeNull("{0} is named in ExportRegistry", clrType.Name);

            foreach (var column in columns)
            {
                // A rename would otherwise silently un-exclude the column: the exclusion keeps
                // matching nothing, and the renamed property starts exporting.
                entityType!.FindProperty(column).Should().NotBeNull(
                    "{0}.{1} is excluded from the archive but no longer exists — a rename has " +
                    "quietly un-excluded it", clrType.Name, column);
            }
        }
    }

    [Fact]
    public void CredentialBearingTypesAreNeverExported()
    {
        // Belt and braces over the pinned list: these are the ones that actually matter, named
        // directly, so a careless bulk edit to Pinned cannot let them through unnoticed.
        Type[] mustNeverExport =
        [
            typeof(RefreshToken), typeof(PasswordResetToken), typeof(UserPasskeyCredential),
            typeof(UserJwtMinIat), typeof(UserExternalLogin), typeof(TenantIntegrationToken),
            typeof(UserDeviceToken), typeof(UserCalendarIcsToken), typeof(UserContactVcfToken),
            typeof(RecipeShareToken), typeof(AuthProxyPairingConfig),
        ];

        foreach (var type in mustNeverExport)
        {
            ExportRegistry.For(type).Export.Should().Be(ExportDisposition.ExcludeSecret, "{0}", type.Name);
            ExportRegistry.For(type).Import.Should().Be(ImportPolicy.Never, "{0}", type.Name);
        }
    }

    [Fact]
    public void PersonalDataThatMayLeaveIsStillNeverWrittenBack()
    {
        // The two-axis case: exporting these is right, restoring them is not.
        foreach (var type in new[] { typeof(User), typeof(UserAuditLog), typeof(ContactAuditLog) })
        {
            ExportRegistry.For(type).Export.Should().Be(ExportDisposition.Export, "{0}", type.Name);
            ExportRegistry.For(type).Import.Should().Be(ImportPolicy.Never, "{0}", type.Name);
        }
    }

    [Fact]
    public void EveryDeclaredDispositionCarriesAReason()
    {
        using var context = RelationalModelContext();

        foreach (var entityType in TenantReachability.TenantScopedEntityTypes(context.Model))
        {
            if (!ExportRegistry.IsDeclared(entityType.ClrType)) continue;

            ExportRegistry.For(entityType.ClrType).Reason
                .Should().NotBeNullOrWhiteSpace("{0}", entityType.ClrType.Name);
        }
    }

    private static HomeManagementDbContext RelationalModelContext() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only")
            .Options);
}
