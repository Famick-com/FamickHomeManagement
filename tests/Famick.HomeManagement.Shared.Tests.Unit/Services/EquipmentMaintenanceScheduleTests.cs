using Famick.HomeManagement.Core.DTOs.Equipment;
using Famick.HomeManagement.Core.Exceptions;
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

/// <summary>
/// Covers the maintenance schedules that moved onto equipment when vehicles were folded in.
/// </summary>
/// <remarks>
/// These replace the vehicle-side schedule tests. The behaviour they pin is deliberately the same
/// as the vehicle implementation had — the 30-day and 1000-unit windows, the overdue rule, the
/// unique-name-per-asset rule — plus the usage-based cases that vehicles supported in the data
/// model but never actually surfaced.
/// </remarks>
public class EquipmentMaintenanceScheduleTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly EquipmentService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public EquipmentMaintenanceScheduleTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options);

        _service = new EquipmentService(
            _context,
            new Mock<IFileStorageService>().Object,
            new Mock<IFileUrlService>().Object,
            new Mock<ILogger<EquipmentService>>().Object);
    }

    public void Dispose() => _context.Dispose();

    private async Task<Equipment> SeedEquipmentAsync(string usageUnit = "miles", EquipmentKind kind = EquipmentKind.Vehicle)
    {
        var equipment = new Equipment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "2023 Acura RDX",
            Kind = kind,
            UsageUnit = usageUnit
        };
        _context.Equipment.Add(equipment);
        await _context.SaveChangesAsync();
        return equipment;
    }

    private async Task AddUsageAsync(Guid equipmentId, decimal reading, DateTime date)
    {
        _context.EquipmentUsageLogs.Add(new EquipmentUsageLog
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            EquipmentId = equipmentId,
            Reading = reading,
            Date = date
        });
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateMaintenanceScheduleAsync_DerivesNextDueDateFromMonthInterval()
    {
        var equipment = await SeedEquipmentAsync();

        var schedule = await _service.CreateMaintenanceScheduleAsync(equipment.Id, new CreateEquipmentMaintenanceScheduleRequest
        {
            Name = "Oil Change",
            IntervalMonths = 6,
            LastCompletedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        schedule.NextDueDate.Should().Be(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task CreateMaintenanceScheduleAsync_DerivesNextDueUsageFromUsageInterval()
    {
        var equipment = await SeedEquipmentAsync();

        var schedule = await _service.CreateMaintenanceScheduleAsync(equipment.Id, new CreateEquipmentMaintenanceScheduleRequest
        {
            Name = "Tire Rotation",
            IntervalUsage = 5000,
            LastCompletedUsage = 45000
        });

        schedule.NextDueUsage.Should().Be(50000);
    }

    [Fact]
    public async Task CreateMaintenanceScheduleAsync_DoesNotOverwriteAnExplicitNextDue()
    {
        var equipment = await SeedEquipmentAsync();
        var stated = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);

        var schedule = await _service.CreateMaintenanceScheduleAsync(equipment.Id, new CreateEquipmentMaintenanceScheduleRequest
        {
            Name = "Brake Inspection",
            IntervalMonths = 6,
            LastCompletedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            NextDueDate = stated
        });

        schedule.NextDueDate.Should().Be(stated, "an explicitly stated next-due beats the derived one");
    }

    [Fact]
    public async Task CreateMaintenanceScheduleAsync_RejectsADuplicateNameOnTheSameEquipment()
    {
        var equipment = await SeedEquipmentAsync();
        var request = new CreateEquipmentMaintenanceScheduleRequest { Name = "Oil Change", IntervalMonths = 6 };

        await _service.CreateMaintenanceScheduleAsync(equipment.Id, request);

        var act = () => _service.CreateMaintenanceScheduleAsync(equipment.Id, new CreateEquipmentMaintenanceScheduleRequest
        {
            Name = "oil change",
            IntervalMonths = 3
        });

        await act.Should().ThrowAsync<DuplicateEntityException>();
    }

    [Fact]
    public async Task CreateMaintenanceScheduleAsync_AllowsTheSameNameOnDifferentEquipment()
    {
        var first = await SeedEquipmentAsync();
        var second = await SeedEquipmentAsync();

        await _service.CreateMaintenanceScheduleAsync(first.Id, new CreateEquipmentMaintenanceScheduleRequest { Name = "Oil Change", IntervalMonths = 6 });
        var act = () => _service.CreateMaintenanceScheduleAsync(second.Id, new CreateEquipmentMaintenanceScheduleRequest { Name = "Oil Change", IntervalMonths = 6 });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetMaintenanceSchedulesAsync_FlagsAPastDueDateAsOverdue()
    {
        var equipment = await SeedEquipmentAsync();
        await _service.CreateMaintenanceScheduleAsync(equipment.Id, new CreateEquipmentMaintenanceScheduleRequest
        {
            Name = "Oil Change",
            IntervalMonths = 6,
            NextDueDate = DateTime.UtcNow.AddDays(-1)
        });

        var schedules = await _service.GetMaintenanceSchedulesAsync(equipment.Id);

        schedules.Single().IsOverdue.Should().BeTrue();
        schedules.Single().IsDueSoon.Should().BeFalse("overdue and due-soon are exclusive");
    }

    [Fact]
    public async Task GetMaintenanceSchedulesAsync_FlagsWithinThirtyDaysAsDueSoon()
    {
        var equipment = await SeedEquipmentAsync();
        await _service.CreateMaintenanceScheduleAsync(equipment.Id, new CreateEquipmentMaintenanceScheduleRequest
        {
            Name = "Oil Change",
            IntervalMonths = 6,
            NextDueDate = DateTime.UtcNow.AddDays(10)
        });

        var schedules = await _service.GetMaintenanceSchedulesAsync(equipment.Id);

        schedules.Single().IsDueSoon.Should().BeTrue();
        schedules.Single().IsOverdue.Should().BeFalse();
    }

    [Fact]
    public async Task GetMaintenanceSchedulesAsync_TreatsUsagePastTheThresholdAsOverdue()
    {
        // The case vehicles modelled but never surfaced: a usage-only schedule with no due date.
        var equipment = await SeedEquipmentAsync();
        await AddUsageAsync(equipment.Id, 50_100, DateTime.UtcNow);

        await _service.CreateMaintenanceScheduleAsync(equipment.Id, new CreateEquipmentMaintenanceScheduleRequest
        {
            Name = "Tire Rotation",
            IntervalUsage = 5000,
            NextDueUsage = 50_000
        });

        var schedule = (await _service.GetMaintenanceSchedulesAsync(equipment.Id)).Single();

        schedule.NextDueDate.Should().BeNull("this schedule is measured in usage, not time");
        schedule.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public async Task GetMaintenanceSchedulesAsync_ReportsTheEquipmentsUsageUnit()
    {
        var equipment = await SeedEquipmentAsync(usageUnit: "hours", kind: EquipmentKind.Appliance);
        await _service.CreateMaintenanceScheduleAsync(equipment.Id, new CreateEquipmentMaintenanceScheduleRequest
        {
            Name = "Replace Filter",
            IntervalUsage = 200
        });

        var schedule = (await _service.GetMaintenanceSchedulesAsync(equipment.Id)).Single();

        schedule.UsageUnit.Should().Be("hours", "a client labels the usage figure without refetching the parent");
    }

    [Fact]
    public async Task GetMaintenanceSchedulesAsync_HidesInactiveSchedulesUnlessAsked()
    {
        var equipment = await SeedEquipmentAsync();
        var created = await _service.CreateMaintenanceScheduleAsync(equipment.Id, new CreateEquipmentMaintenanceScheduleRequest { Name = "Oil Change", IntervalMonths = 6 });

        await _service.UpdateMaintenanceScheduleAsync(equipment.Id, created.Id, new UpdateEquipmentMaintenanceScheduleRequest
        {
            Name = "Oil Change",
            IntervalMonths = 6,
            IsActive = false
        });

        (await _service.GetMaintenanceSchedulesAsync(equipment.Id)).Should().BeEmpty();
        (await _service.GetMaintenanceSchedulesAsync(equipment.Id, includeInactive: true)).Should().ContainSingle();
    }

    [Fact]
    public async Task CompleteMaintenanceScheduleAsync_LogsARecordAndRollsTheScheduleForward()
    {
        var equipment = await SeedEquipmentAsync();
        var created = await _service.CreateMaintenanceScheduleAsync(equipment.Id, new CreateEquipmentMaintenanceScheduleRequest
        {
            Name = "Oil Change",
            IntervalMonths = 6,
            IntervalUsage = 5000
        });

        var completedOn = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var record = await _service.CompleteMaintenanceScheduleAsync(equipment.Id, created.Id, new CompleteEquipmentMaintenanceScheduleRequest
        {
            CompletedDate = completedOn,
            UsageAtCompletion = 50_000,
            Cost = 79.95m,
            ServiceProvider = "Corner Garage"
        });

        record.Description.Should().Be("Oil Change");
        record.Cost.Should().Be(79.95m);
        record.ServiceProvider.Should().Be("Corner Garage", "cost and provider would have been lost if the record had not been widened");
        record.MaintenanceScheduleId.Should().Be(created.Id);

        var schedule = (await _service.GetMaintenanceSchedulesAsync(equipment.Id)).Single();
        schedule.LastCompletedDate.Should().Be(completedOn);
        schedule.NextDueDate.Should().Be(completedOn.AddMonths(6));
        schedule.NextDueUsage.Should().Be(55_000);
    }

    [Fact]
    public async Task CompleteMaintenanceScheduleAsync_FallsBackToTheLatestUsageReading()
    {
        var equipment = await SeedEquipmentAsync();
        await AddUsageAsync(equipment.Id, 30_000, DateTime.UtcNow.AddDays(-30));
        await AddUsageAsync(equipment.Id, 42_000, DateTime.UtcNow);

        var created = await _service.CreateMaintenanceScheduleAsync(equipment.Id, new CreateEquipmentMaintenanceScheduleRequest
        {
            Name = "Tire Rotation",
            IntervalUsage = 5000
        });

        // No UsageAtCompletion given — without the fallback a usage-based schedule could never
        // roll forward from the UI's "mark done" button.
        var record = await _service.CompleteMaintenanceScheduleAsync(equipment.Id, created.Id, new CompleteEquipmentMaintenanceScheduleRequest());

        record.UsageAtCompletion.Should().Be(42_000);
        (await _service.GetMaintenanceSchedulesAsync(equipment.Id)).Single().NextDueUsage.Should().Be(47_000);
    }

    [Fact]
    public async Task DeleteMaintenanceScheduleAsync_KeepsTheRecordsLoggedAgainstIt()
    {
        var equipment = await SeedEquipmentAsync();
        var created = await _service.CreateMaintenanceScheduleAsync(equipment.Id, new CreateEquipmentMaintenanceScheduleRequest { Name = "Oil Change", IntervalMonths = 6 });
        await _service.CompleteMaintenanceScheduleAsync(equipment.Id, created.Id, new CompleteEquipmentMaintenanceScheduleRequest());

        await _service.DeleteMaintenanceScheduleAsync(equipment.Id, created.Id);

        var records = await _service.GetMaintenanceRecordsAsync(equipment.Id);
        records.Should().ContainSingle("deleting a schedule must not erase the history of work done under it");
    }

    [Fact]
    public async Task GetEquipmentTreeAsync_HidesRetiredEquipmentUnlessAsked()
    {
        var active = await SeedEquipmentAsync();
        var retired = await SeedEquipmentAsync();
        retired.Name = "Sold Truck";
        retired.IsActive = false;
        await _context.SaveChangesAsync();

        var visible = await _service.GetEquipmentTreeAsync();
        var all = await _service.GetEquipmentTreeAsync(includeInactive: true);

        visible.Should().ContainSingle().Which.Id.Should().Be(active.Id);
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetEquipmentTreeAsync_PromotesAnActiveChildWhoseParentIsRetired()
    {
        var parent = await SeedEquipmentAsync();
        parent.IsActive = false;
        var child = new Equipment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Roof Rack",
            Kind = EquipmentKind.Other,
            ParentEquipmentId = parent.Id
        };
        _context.Equipment.Add(child);
        await _context.SaveChangesAsync();

        var tree = await _service.GetEquipmentTreeAsync();

        tree.Should().ContainSingle("hiding a retired parent must not hide its active child")
            .Which.Id.Should().Be(child.Id);
    }

    [Fact]
    public async Task GetMaintenanceSchedulesAsync_ThrowsForUnknownEquipment()
    {
        var act = () => _service.GetMaintenanceSchedulesAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
