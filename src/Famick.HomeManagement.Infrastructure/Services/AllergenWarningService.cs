using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class AllergenWarningService : IAllergenWarningService
{
    private readonly HomeManagementDbContext _context;
    private readonly ILogger<AllergenWarningService> _logger;

    public AllergenWarningService(
        HomeManagementDbContext context,
        ILogger<AllergenWarningService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AllergenCheckResultDto> CheckMealAsync(Guid mealId, CancellationToken ct = default)
    {
        var meal = await _context.Meals
            .Include(m => m.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p!.Allergens)
            .Include(m => m.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p!.DietaryConflicts)
            .FirstOrDefaultAsync(m => m.Id == mealId, ct)
            ?? throw new KeyNotFoundException($"Meal with ID {mealId} not found");

        var householdMembers = await GetHouseholdMembersWithProfiles(ct);
        var warnings = CheckProductsAgainstMembers(meal.Items, householdMembers);

        return new AllergenCheckResultDto
        {
            MealId = mealId,
            HasWarnings = warnings.Count > 0,
            Warnings = warnings
        };
    }

    public async Task<MealPlanAllergenWarningsDto> CheckMealPlanAsync(Guid mealPlanId, CancellationToken ct = default)
    {
        var plan = await _context.MealPlans
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.Meal)
                    .ThenInclude(m => m!.Items)
                        .ThenInclude(i => i.Product)
                            .ThenInclude(p => p!.Allergens)
            .Include(mp => mp.Entries)
                .ThenInclude(e => e.Meal)
                    .ThenInclude(m => m!.Items)
                        .ThenInclude(i => i.Product)
                            .ThenInclude(p => p!.DietaryConflicts)
            .FirstOrDefaultAsync(mp => mp.Id == mealPlanId, ct)
            ?? throw new KeyNotFoundException($"Meal plan with ID {mealPlanId} not found");

        var householdMembers = await GetHouseholdMembersWithProfiles(ct);
        var allWarnings = new List<AllergenWarningDto>();

        foreach (var entry in plan.Entries.Where(e => e.Meal != null))
        {
            var warnings = CheckProductsAgainstMembers(entry.Meal!.Items, householdMembers);
            allWarnings.AddRange(warnings);
        }

        return new MealPlanAllergenWarningsDto
        {
            MealPlanId = mealPlanId,
            HasWarnings = allWarnings.Count > 0,
            Warnings = allWarnings
        };
    }

    private async Task<List<Contact>> GetHouseholdMembersWithProfiles(CancellationToken ct)
    {
        // Find the tenant household group
        var householdGroup = await _context.Contacts
            .FirstOrDefaultAsync(c => c.IsTenantHousehold, ct);

        if (householdGroup == null)
            return new List<Contact>();

        // Load members with allergen profiles
        return await _context.Contacts
            .Include(c => c.Allergens)
            .Include(c => c.DietaryPreferences)
            .Where(c => c.ParentContactId == householdGroup.Id)
            .ToListAsync(ct);
    }

    private static List<AllergenWarningDto> CheckProductsAgainstMembers(
        ICollection<MealItem> items, List<Contact> members)
    {
        var warnings = new List<AllergenWarningDto>();

        foreach (var item in items.Where(i => i.Product != null))
        {
            var product = item.Product!;

            foreach (var member in members)
            {
                // Check allergens
                foreach (var memberAllergen in member.Allergens)
                {
                    if (product.Allergens.Any(pa => pa.AllergenType == memberAllergen.AllergenType))
                    {
                        warnings.Add(new AllergenWarningDto
                        {
                            ContactId = member.Id,
                            ContactName = member.DisplayName,
                            AllergenType = memberAllergen.AllergenType,
                            Severity = memberAllergen.Severity,
                            ProductName = product.Name,
                            ProductId = product.Id
                        });
                    }
                }

                // Check dietary conflicts
                foreach (var memberPref in member.DietaryPreferences)
                {
                    if (product.DietaryConflicts.Any(dc => dc.DietaryPreference == memberPref.DietaryPreference))
                    {
                        // Map dietary conflict to a warning with Sensitivity severity
                        warnings.Add(new AllergenWarningDto
                        {
                            ContactId = member.Id,
                            ContactName = member.DisplayName,
                            AllergenType = AllergenType.Milk, // Placeholder - dietary conflicts don't map to allergen types
                            Severity = AllergenSeverity.Sensitivity,
                            ProductName = $"{product.Name} (conflicts with {memberPref.DietaryPreference})",
                            ProductId = product.Id
                        });
                    }
                }
            }
        }

        return warnings;
    }
}
