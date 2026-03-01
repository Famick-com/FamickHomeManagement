using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class MealPlannerOnboardingService : IMealPlannerOnboardingService
{
    private readonly HomeManagementDbContext _context;
    private readonly ILogger<MealPlannerOnboardingService> _logger;

    // All known tip keys
    private static readonly string[] AllTipKeys = new[]
    {
        "drag-and-drop",
        "batch-cooking",
        "shopping-list",
        "allergen-warnings",
        "nutrition-sidebar",
        "meal-suggestions"
    };

    public MealPlannerOnboardingService(
        HomeManagementDbContext context,
        ILogger<MealPlannerOnboardingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OnboardingStateDto> GetOnboardingStateAsync(Guid userId, CancellationToken ct = default)
    {
        var pref = await _context.UserMealPlannerPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (pref == null)
        {
            return new OnboardingStateDto
            {
                HasCompletedOnboarding = false,
                PlanningStyle = null,
                CollapsedMealTypeIds = new()
            };
        }

        return new OnboardingStateDto
        {
            HasCompletedOnboarding = pref.HasCompletedOnboarding,
            PlanningStyle = pref.PlanningStyle,
            CollapsedMealTypeIds = DeserializeCollapsedIds(pref.CollapsedMealTypeIds)
        };
    }

    public async Task SaveOnboardingAsync(Guid userId, SaveOnboardingRequest request, CancellationToken ct = default)
    {
        var pref = await _context.UserMealPlannerPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (pref == null)
        {
            pref = new UserMealPlannerPreference { UserId = userId };
            _context.UserMealPlannerPreferences.Add(pref);
        }

        pref.HasCompletedOnboarding = true;
        pref.PlanningStyle = request.PlanningStyle;
        pref.CollapsedMealTypeIds = request.CollapsedMealTypeIds != null
            ? JsonSerializer.Serialize(request.CollapsedMealTypeIds)
            : null;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Saved onboarding preferences for user {UserId}", userId);
    }

    public async Task ResetOnboardingAsync(Guid userId, CancellationToken ct = default)
    {
        var pref = await _context.UserMealPlannerPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (pref != null)
        {
            pref.HasCompletedOnboarding = false;
            pref.PlanningStyle = null;
            pref.CollapsedMealTypeIds = null;
            await _context.SaveChangesAsync(ct);
        }

        // Also reset all tips
        var tips = await _context.UserMealPlannerTips
            .Where(t => t.UserId == userId)
            .ToListAsync(ct);

        if (tips.Count > 0)
        {
            _context.UserMealPlannerTips.RemoveRange(tips);
            await _context.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Reset onboarding for user {UserId}", userId);
    }

    public async Task<List<FeatureTipDto>> GetUndismissedTipsAsync(Guid userId, CancellationToken ct = default)
    {
        var dismissedKeys = await _context.UserMealPlannerTips
            .Where(t => t.UserId == userId)
            .Select(t => t.TipKey)
            .ToListAsync(ct);

        return AllTipKeys.Select(key => new FeatureTipDto
        {
            TipKey = key,
            IsDismissed = dismissedKeys.Contains(key)
        })
        .Where(t => !t.IsDismissed)
        .ToList();
    }

    public async Task DismissTipAsync(Guid userId, string tipKey, CancellationToken ct = default)
    {
        var existing = await _context.UserMealPlannerTips
            .FirstOrDefaultAsync(t => t.UserId == userId && t.TipKey == tipKey, ct);

        if (existing != null)
            return; // Already dismissed, idempotent

        _context.UserMealPlannerTips.Add(new UserMealPlannerTip
        {
            UserId = userId,
            TipKey = tipKey,
            DismissedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} dismissed tip '{TipKey}'", userId, tipKey);
    }

    private static List<Guid> DeserializeCollapsedIds(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return new List<Guid>();

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? new List<Guid>();
        }
        catch
        {
            return new List<Guid>();
        }
    }
}
