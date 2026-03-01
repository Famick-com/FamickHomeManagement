using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

public class MealPlannerOnboardingServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly MealPlannerOnboardingService _service;
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000099");

    public MealPlannerOnboardingServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options, null);

        var logger = new Mock<ILogger<MealPlannerOnboardingService>>();

        _service = new MealPlannerOnboardingService(_context, logger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetOnboardingStateAsync_NewUser_ReturnsDefaults()
    {
        var result = await _service.GetOnboardingStateAsync(_userId);

        result.Should().NotBeNull();
        result.HasCompletedOnboarding.Should().BeFalse();
        result.PlanningStyle.Should().BeNull();
        result.CollapsedMealTypeIds.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveOnboardingAsync_NewUser_CreatesPreference()
    {
        var request = new SaveOnboardingRequest
        {
            PlanningStyle = PlanningStyle.WeekAtAGlance,
            CollapsedMealTypeIds = new List<Guid> { Guid.NewGuid() }
        };

        await _service.SaveOnboardingAsync(_userId, request);

        var pref = await _context.Set<UserMealPlannerPreference>()
            .FirstOrDefaultAsync(p => p.UserId == _userId);
        pref.Should().NotBeNull();
        pref!.HasCompletedOnboarding.Should().BeTrue();
        pref.PlanningStyle.Should().Be(PlanningStyle.WeekAtAGlance);
    }

    [Fact]
    public async Task SaveOnboardingAsync_ExistingUser_UpdatesPreference()
    {
        _context.Set<UserMealPlannerPreference>().Add(new UserMealPlannerPreference
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            HasCompletedOnboarding = false,
            PlanningStyle = PlanningStyle.DayByDay
        });
        await _context.SaveChangesAsync();

        var request = new SaveOnboardingRequest
        {
            PlanningStyle = PlanningStyle.WeekAtAGlance
        };

        await _service.SaveOnboardingAsync(_userId, request);

        var pref = await _context.Set<UserMealPlannerPreference>()
            .FirstOrDefaultAsync(p => p.UserId == _userId);
        pref!.HasCompletedOnboarding.Should().BeTrue();
        pref.PlanningStyle.Should().Be(PlanningStyle.WeekAtAGlance);
    }

    [Fact]
    public async Task ResetOnboardingAsync_ExistingUser_ResetsState()
    {
        _context.Set<UserMealPlannerPreference>().Add(new UserMealPlannerPreference
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            HasCompletedOnboarding = true,
            PlanningStyle = PlanningStyle.WeekAtAGlance
        });
        _context.Set<UserMealPlannerTip>().Add(new UserMealPlannerTip
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            TipKey = "drag-and-drop",
            DismissedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        await _service.ResetOnboardingAsync(_userId);

        var pref = await _context.Set<UserMealPlannerPreference>()
            .FirstOrDefaultAsync(p => p.UserId == _userId);
        pref!.HasCompletedOnboarding.Should().BeFalse();
        pref.PlanningStyle.Should().BeNull();

        var tips = await _context.Set<UserMealPlannerTip>()
            .Where(t => t.UserId == _userId).ToListAsync();
        tips.Should().BeEmpty();
    }

    [Fact]
    public async Task ResetOnboardingAsync_NoExistingPreference_DoesNotThrow()
    {
        var act = () => _service.ResetOnboardingAsync(_userId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DismissTipAsync_NewTip_CreatesDismissal()
    {
        await _service.DismissTipAsync(_userId, "batch-cooking");

        var tip = await _context.Set<UserMealPlannerTip>()
            .FirstOrDefaultAsync(t => t.UserId == _userId && t.TipKey == "batch-cooking");
        tip.Should().NotBeNull();
        tip!.DismissedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DismissTipAsync_AlreadyDismissed_IsIdempotent()
    {
        _context.Set<UserMealPlannerTip>().Add(new UserMealPlannerTip
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            TipKey = "batch-cooking",
            DismissedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();

        var act = () => _service.DismissTipAsync(_userId, "batch-cooking");

        await act.Should().NotThrowAsync();

        var tips = await _context.Set<UserMealPlannerTip>()
            .Where(t => t.UserId == _userId && t.TipKey == "batch-cooking")
            .ToListAsync();
        tips.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUndismissedTipsAsync_NoDismissals_ReturnsAllTips()
    {
        var result = await _service.GetUndismissedTipsAsync(_userId);

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(t => !t.IsDismissed);
    }

    [Fact]
    public async Task GetUndismissedTipsAsync_SomeDismissed_ReturnsOnlyUndismissed()
    {
        _context.Set<UserMealPlannerTip>().Add(new UserMealPlannerTip
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            TipKey = "drag-and-drop",
            DismissedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetUndismissedTipsAsync(_userId);

        result.Should().NotContain(t => t.TipKey == "drag-and-drop");
        result.Count.Should().BeGreaterThan(0);
    }
}
