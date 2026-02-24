using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Interfaces;
using Famick.HomeManagement.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Infrastructure.Data;

/// <summary>
/// Main database context for the HomeManagement application with configurable multi-tenancy support.
/// Supports both single-tenant (self-hosted) and multi-tenant (cloud) deployments.
/// </summary>
public class HomeManagementDbContext : DbContext
{
    private readonly ITenantProvider? _tenantProvider;
    private readonly IMultiTenancyOptions _multiTenancyOptions;

    /// <summary>
    /// Gets the current tenant ID for query filtering.
    /// Returns fixed tenant ID if configured for single-tenant mode,
    /// otherwise returns the tenant ID from the tenant provider.
    /// </summary>
    private Guid? CurrentTenantId => _multiTenancyOptions.FixedTenantId ?? _tenantProvider?.TenantId;

    public HomeManagementDbContext(
        DbContextOptions<HomeManagementDbContext> options,
        ITenantProvider? tenantProvider = null,
        IMultiTenancyOptions? multiTenancyOptions = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
        // Default to multi-tenant mode for backwards compatibility with existing code
        _multiTenancyOptions = multiTenancyOptions ?? new MultiTenancyOptions { IsMultiTenantEnabled = true };
    }

    // Core entities
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();
    public DbSet<UserPasskeyCredential> UserPasskeyCredentials => Set<UserPasskeyCredential>();

    // Household management entities
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<QuantityUnit> QuantityUnits => Set<QuantityUnit>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductBarcode> ProductBarcodes => Set<ProductBarcode>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductNutrition> ProductNutrition => Set<ProductNutrition>();

    // Stock management
    public DbSet<StockEntry> Stock => Set<StockEntry>();
    public DbSet<StockLog> StockLog => Set<StockLog>();

    // Phase 2 - Product categorization and shopping
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();
    public DbSet<ShoppingLocation> ShoppingLocations => Set<ShoppingLocation>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();

    // Store integrations
    public DbSet<ProductStoreMetadata> ProductStoreMetadata => Set<ProductStoreMetadata>();
    public DbSet<TenantIntegrationToken> TenantIntegrationTokens => Set<TenantIntegrationToken>();

    // Phase 2 - Recipes
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<RecipePosition> RecipePositions => Set<RecipePosition>();
    public DbSet<RecipeNesting> RecipeNestings => Set<RecipeNesting>();
    public DbSet<RecipeImage> RecipeImages => Set<RecipeImage>();
    public DbSet<RecipeShareToken> RecipeShareTokens => Set<RecipeShareToken>();

    // Phase 2 - Chores
    public DbSet<Chore> Chores => Set<Chore>();
    public DbSet<ChoreLog> ChoresLog => Set<ChoreLog>();

    // My Home
    public DbSet<Home> Homes => Set<Home>();
    public DbSet<HomeUtility> HomeUtilities => Set<HomeUtility>();
    public DbSet<PropertyLink> PropertyLinks => Set<PropertyLink>();

    // Vehicles
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleMileageLog> VehicleMileageLogs => Set<VehicleMileageLog>();
    public DbSet<VehicleDocument> VehicleDocuments => Set<VehicleDocument>();
    public DbSet<VehicleMaintenanceRecord> VehicleMaintenanceRecords => Set<VehicleMaintenanceRecord>();
    public DbSet<VehicleMaintenanceSchedule> VehicleMaintenanceSchedules => Set<VehicleMaintenanceSchedule>();

    // Equipment
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<EquipmentCategory> EquipmentCategories => Set<EquipmentCategory>();
    public DbSet<EquipmentDocument> EquipmentDocuments => Set<EquipmentDocument>();
    public DbSet<EquipmentDocumentTag> EquipmentDocumentTags => Set<EquipmentDocumentTag>();
    public DbSet<EquipmentUsageLog> EquipmentUsageLogs => Set<EquipmentUsageLog>();
    public DbSet<EquipmentMaintenanceRecord> EquipmentMaintenanceRecords => Set<EquipmentMaintenanceRecord>();

    // Storage Bins
    public DbSet<StorageBin> StorageBins => Set<StorageBin>();
    public DbSet<StorageBinPhoto> StorageBinPhotos => Set<StorageBinPhoto>();

    // Contacts
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<ContactAddress> ContactAddresses => Set<ContactAddress>();
    public DbSet<ContactPhoneNumber> ContactPhoneNumbers => Set<ContactPhoneNumber>();
    public DbSet<ContactEmailAddress> ContactEmailAddresses => Set<ContactEmailAddress>();
    public DbSet<ContactSocialMedia> ContactSocialMedia => Set<ContactSocialMedia>();
    public DbSet<ContactRelationship> ContactRelationships => Set<ContactRelationship>();
    public DbSet<ContactTag> ContactTags => Set<ContactTag>();
    public DbSet<ContactTagLink> ContactTagLinks => Set<ContactTagLink>();
    public DbSet<ContactUserShare> ContactUserShares => Set<ContactUserShare>();
    public DbSet<ContactAuditLog> ContactAuditLogs => Set<ContactAuditLog>();

    // TODO Items
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    // Notifications
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<UserDeviceToken> UserDeviceTokens => Set<UserDeviceToken>();

    // Calendar
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<CalendarEventMember> CalendarEventMembers => Set<CalendarEventMember>();
    public DbSet<CalendarEventException> CalendarEventExceptions => Set<CalendarEventException>();
    public DbSet<ExternalCalendarSubscription> ExternalCalendarSubscriptions => Set<ExternalCalendarSubscription>();
    public DbSet<ExternalCalendarEvent> ExternalCalendarEvents => Set<ExternalCalendarEvent>();
    public DbSet<UserCalendarIcsToken> UserCalendarIcsTokens => Set<UserCalendarIcsToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HomeManagementDbContext).Assembly);

        // Apply global query filters for tenant isolation
        // Behavior depends on multi-tenancy configuration:
        // - Multi-tenant mode: Dynamic filtering by current tenant context
        // - Single-tenant mode: Optional filtering by fixed tenant ID
        if (_multiTenancyOptions.IsMultiTenantEnabled)
        {
            ApplyMultiTenantQueryFilters(modelBuilder);
        }
        else
        {
            ApplySingleTenantQueryFilters(modelBuilder);
        }
    }

    /// <summary>
    /// Applies query filters for multi-tenant (cloud) mode.
    /// Filters by the current tenant context from ITenantProvider.
    /// If no tenant is set (CurrentTenantId == null), returns all records (for admin operations).
    /// </summary>
    private void ApplyMultiTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                // Build expression: e => CurrentTenantId == null || e.TenantId == CurrentTenantId
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var entityTenantId = System.Linq.Expressions.Expression.Property(parameter, nameof(ITenantEntity.TenantId));

                var currentTenantIdProperty = System.Linq.Expressions.Expression.Property(
                    System.Linq.Expressions.Expression.Constant(this, typeof(HomeManagementDbContext)),
                    nameof(CurrentTenantId));

                // CurrentTenantId == null (allows admin to see all tenants)
                var nullCheck = System.Linq.Expressions.Expression.Equal(
                    currentTenantIdProperty,
                    System.Linq.Expressions.Expression.Constant(null, typeof(Guid?)));

                // Convert e.TenantId (Guid) to Guid? for comparison
                var entityTenantIdNullable = System.Linq.Expressions.Expression.Convert(entityTenantId, typeof(Guid?));

                // e.TenantId == CurrentTenantId
                var tenantMatch = System.Linq.Expressions.Expression.Equal(entityTenantIdNullable, currentTenantIdProperty);

                // CurrentTenantId == null || e.TenantId == CurrentTenantId
                var filter = System.Linq.Expressions.Expression.OrElse(nullCheck, tenantMatch);

                var lambda = System.Linq.Expressions.Expression.Lambda(filter, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        // Filter non-tenant child entities through their parent's TenantId
        ApplyChildEntityQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Applies query filters for single-tenant (self-hosted) mode.
    /// If FixedTenantId is configured, filters all queries to that tenant.
    /// If FixedTenantId is null, no filtering is applied (all records returned).
    /// </summary>
    private void ApplySingleTenantQueryFilters(ModelBuilder modelBuilder)
    {
        // If no fixed tenant ID is configured, skip filtering (single deployment, all data visible)
        if (!_multiTenancyOptions.FixedTenantId.HasValue)
            return;

        var fixedTenantId = _multiTenancyOptions.FixedTenantId.Value;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                // Build expression: e => e.TenantId == fixedTenantId
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var entityTenantId = System.Linq.Expressions.Expression.Property(parameter, nameof(ITenantEntity.TenantId));
                var fixedTenantIdConstant = System.Linq.Expressions.Expression.Constant(fixedTenantId, typeof(Guid));

                var filter = System.Linq.Expressions.Expression.Equal(entityTenantId, fixedTenantIdConstant);

                var lambda = System.Linq.Expressions.Expression.Lambda(filter, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        // Filter non-tenant child entities through their parent's TenantId
        ApplyChildEntityQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Adds query filters for child entities that don't have their own TenantId
    /// but are always accessed through a tenant-filtered parent.
    /// </summary>
    private void ApplyChildEntityQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CalendarEventException>()
            .HasQueryFilter(e => CurrentTenantId == null || e.CalendarEvent!.TenantId == CurrentTenantId);

        modelBuilder.Entity<CalendarEventMember>()
            .HasQueryFilter(m => CurrentTenantId == null || m.CalendarEvent!.TenantId == CurrentTenantId);

        modelBuilder.Entity<ExternalCalendarEvent>()
            .HasQueryFilter(e => CurrentTenantId == null || e.Subscription!.TenantId == CurrentTenantId);
    }

    public override int SaveChanges()
    {
        UpdateEntityTimestamps();
        SetTenantId();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateEntityTimestamps();
        SetTenantId();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Automatically set CreatedAt and UpdatedAt timestamps
    /// </summary>
    private void UpdateEntityTimestamps()
    {
        var entries = ChangeTracker.Entries<IEntity>();
        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }

    /// <summary>
    /// Automatically set TenantId for new tenant entities
    /// </summary>
    private void SetTenantId()
    {
        if (_tenantProvider?.TenantId == null)
            return;

        var tenantId = _tenantProvider.TenantId.Value;
        var entries = ChangeTracker.Entries<ITenantEntity>()
            .Where(e => e.State == EntityState.Added);

        foreach (var entry in entries)
        {
            if (entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = tenantId;
            }
        }
    }
}
