using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Vehicles;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

public class VehicleServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly IMapper _mapper;
    private readonly VehicleService _service;

    public VehicleServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options);

        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<VehicleMappingProfile>();
        });
        _mapper = config.CreateMapper();

        var logger = new Mock<ILogger<VehicleService>>();
        _service = new VehicleService(_context, _mapper, logger.Object);
    }

    #region Vehicle CRUD

    [Fact]
    public async Task CreateVehicleAsync_ShouldCreateAndReturn()
    {
        var request = new CreateVehicleRequest
        {
            Year = 2023,
            Make = "Toyota",
            Model = "Camry",
            CurrentMileage = 15000
        };

        var result = await _service.CreateVehicleAsync(request);

        result.Should().NotBeNull();
        result.Year.Should().Be(2023);
        result.Make.Should().Be("Toyota");
        result.Model.Should().Be("Camry");
        result.CurrentMileage.Should().Be(15000);
        result.MileageAsOfDate.Should().NotBeNull();
        result.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetVehicleAsync_ShouldReturnVehicle()
    {
        var created = await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic"
        });

        var result = await _service.GetVehicleAsync(created.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.Make.Should().Be("Honda");
    }

    [Fact]
    public async Task GetVehicleAsync_NotFound_ShouldReturnNull()
    {
        var result = await _service.GetVehicleAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetVehiclesAsync_ShouldReturnAll()
    {
        await _service.CreateVehicleAsync(new CreateVehicleRequest { Year = 2022, Make = "Honda", Model = "Civic" });
        await _service.CreateVehicleAsync(new CreateVehicleRequest { Year = 2023, Make = "Toyota", Model = "Camry" });

        var result = await _service.GetVehiclesAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateVehicleAsync_ShouldUpdate()
    {
        var created = await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic"
        });

        var result = await _service.UpdateVehicleAsync(created.Id, new UpdateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Accord", IsActive = true
        });

        result.Model.Should().Be("Accord");
    }

    [Fact]
    public async Task UpdateVehicleAsync_NotFound_ShouldThrow()
    {
        var act = () => _service.UpdateVehicleAsync(Guid.NewGuid(), new UpdateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic", IsActive = true
        });

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task DeleteVehicleAsync_ShouldDelete()
    {
        var created = await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic"
        });

        await _service.DeleteVehicleAsync(created.Id);

        var result = await _service.GetVehicleAsync(created.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteVehicleAsync_NotFound_ShouldThrow()
    {
        var act = () => _service.DeleteVehicleAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    #endregion

    #region Duplicate VIN Detection

    [Fact]
    public async Task CreateVehicleAsync_DuplicateVin_ShouldThrow()
    {
        await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic", Vin = "1HGBH41JXMN109186"
        });

        var act = () => _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2023, Make = "Toyota", Model = "Camry", Vin = "1HGBH41JXMN109186"
        });

        await act.Should().ThrowAsync<DuplicateEntityException>();
    }

    [Fact]
    public async Task UpdateVehicleAsync_DuplicateVin_ShouldThrow()
    {
        await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic", Vin = "VIN1"
        });
        var second = await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2023, Make = "Toyota", Model = "Camry", Vin = "VIN2"
        });

        var act = () => _service.UpdateVehicleAsync(second.Id, new UpdateVehicleRequest
        {
            Year = 2023, Make = "Toyota", Model = "Camry", Vin = "VIN1", IsActive = true
        });

        await act.Should().ThrowAsync<DuplicateEntityException>();
    }

    [Fact]
    public async Task UpdateVehicleAsync_SameVin_ShouldNotThrow()
    {
        var created = await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic", Vin = "VIN1"
        });

        var result = await _service.UpdateVehicleAsync(created.Id, new UpdateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Accord", Vin = "VIN1", IsActive = true
        });

        result.Model.Should().Be("Accord");
    }

    #endregion

    #region Mileage Tracking

    [Fact]
    public async Task LogMileageAsync_ShouldLogAndUpdateVehicle()
    {
        var vehicle = await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic", CurrentMileage = 10000
        });

        var log = await _service.LogMileageAsync(vehicle.Id, new LogMileageRequest { Mileage = 15000 });

        log.Should().NotBeNull();
        log.Mileage.Should().Be(15000);

        var updated = await _service.GetVehicleAsync(vehicle.Id);
        updated!.CurrentMileage.Should().Be(15000);
    }

    [Fact]
    public async Task LogMileageAsync_VehicleNotFound_ShouldThrow()
    {
        var act = () => _service.LogMileageAsync(Guid.NewGuid(), new LogMileageRequest { Mileage = 5000 });
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task GetMileageHistoryAsync_ShouldReturnLogs()
    {
        var vehicle = await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic"
        });

        await _service.LogMileageAsync(vehicle.Id, new LogMileageRequest { Mileage = 10000 });
        await _service.LogMileageAsync(vehicle.Id, new LogMileageRequest { Mileage = 15000 });

        var history = await _service.GetMileageHistoryAsync(vehicle.Id);
        history.Should().HaveCount(2);
    }

    #endregion

    #region Maintenance Records

    [Fact]
    public async Task CreateMaintenanceRecordAsync_ShouldCreateAndUpdateMileage()
    {
        var vehicle = await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic", CurrentMileage = 10000
        });

        var record = await _service.CreateMaintenanceRecordAsync(vehicle.Id, new CreateMaintenanceRecordRequest
        {
            Description = "Oil Change",
            CompletedDate = DateTime.UtcNow,
            MileageAtCompletion = 15000
        });

        record.Should().NotBeNull();
        record.Description.Should().Be("Oil Change");

        var updated = await _service.GetVehicleAsync(vehicle.Id);
        updated!.CurrentMileage.Should().Be(15000);
    }

    [Fact]
    public async Task CreateMaintenanceRecordAsync_VehicleNotFound_ShouldThrow()
    {
        var act = () => _service.CreateMaintenanceRecordAsync(Guid.NewGuid(), new CreateMaintenanceRecordRequest
        {
            Description = "Oil Change", CompletedDate = DateTime.UtcNow
        });
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    #endregion

    #region Maintenance Schedules

    [Fact]
    public async Task CreateMaintenanceScheduleAsync_ShouldCreate()
    {
        var vehicle = await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic"
        });

        var schedule = await _service.CreateMaintenanceScheduleAsync(vehicle.Id, new CreateMaintenanceScheduleRequest
        {
            Name = "Oil Change",
            IntervalMonths = 6,
            IntervalMiles = 5000
        });

        schedule.Should().NotBeNull();
        schedule.Name.Should().Be("Oil Change");
    }

    [Fact]
    public async Task CreateMaintenanceScheduleAsync_DuplicateName_ShouldThrow()
    {
        var vehicle = await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic"
        });

        await _service.CreateMaintenanceScheduleAsync(vehicle.Id, new CreateMaintenanceScheduleRequest
        {
            Name = "Oil Change"
        });

        var act = () => _service.CreateMaintenanceScheduleAsync(vehicle.Id, new CreateMaintenanceScheduleRequest
        {
            Name = "Oil Change"
        });

        await act.Should().ThrowAsync<DuplicateEntityException>();
    }

    [Fact]
    public async Task CreateMaintenanceScheduleAsync_VehicleNotFound_ShouldThrow()
    {
        var act = () => _service.CreateMaintenanceScheduleAsync(Guid.NewGuid(), new CreateMaintenanceScheduleRequest
        {
            Name = "Oil Change"
        });
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task UpdateMaintenanceScheduleAsync_ShouldUpdate()
    {
        var vehicle = await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic"
        });

        var schedule = await _service.CreateMaintenanceScheduleAsync(vehicle.Id, new CreateMaintenanceScheduleRequest
        {
            Name = "Oil Change", IntervalMiles = 5000
        });

        var result = await _service.UpdateMaintenanceScheduleAsync(vehicle.Id, schedule.Id, new UpdateMaintenanceScheduleRequest
        {
            Name = "Full Synthetic Oil Change", IntervalMiles = 7500, IsActive = true
        });

        result.Name.Should().Be("Full Synthetic Oil Change");
        result.IntervalMiles.Should().Be(7500);
    }

    [Fact]
    public async Task UpdateMaintenanceScheduleAsync_DuplicateName_ShouldThrow()
    {
        var vehicle = await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic"
        });

        await _service.CreateMaintenanceScheduleAsync(vehicle.Id, new CreateMaintenanceScheduleRequest { Name = "Oil Change" });
        var second = await _service.CreateMaintenanceScheduleAsync(vehicle.Id, new CreateMaintenanceScheduleRequest { Name = "Tire Rotation" });

        var act = () => _service.UpdateMaintenanceScheduleAsync(vehicle.Id, second.Id, new UpdateMaintenanceScheduleRequest
        {
            Name = "Oil Change", IsActive = true
        });

        await act.Should().ThrowAsync<DuplicateEntityException>();
    }

    [Fact]
    public async Task DeleteMaintenanceScheduleAsync_ShouldDelete()
    {
        var vehicle = await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic"
        });

        var schedule = await _service.CreateMaintenanceScheduleAsync(vehicle.Id, new CreateMaintenanceScheduleRequest
        {
            Name = "Oil Change"
        });

        await _service.DeleteMaintenanceScheduleAsync(vehicle.Id, schedule.Id);

        var schedules = await _service.GetMaintenanceSchedulesAsync(vehicle.Id, includeInactive: true);
        schedules.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteMaintenanceScheduleAsync_NotFound_ShouldThrow()
    {
        var act = () => _service.DeleteMaintenanceScheduleAsync(Guid.NewGuid(), Guid.NewGuid());
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task CompleteMaintenanceScheduleAsync_ShouldCreateRecordAndUpdateScheduleAndMileage()
    {
        var vehicle = await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic", CurrentMileage = 10000
        });

        var schedule = await _service.CreateMaintenanceScheduleAsync(vehicle.Id, new CreateMaintenanceScheduleRequest
        {
            Name = "Oil Change", IntervalMonths = 6, IntervalMiles = 5000
        });

        var record = await _service.CompleteMaintenanceScheduleAsync(vehicle.Id, schedule.Id, new CompleteMaintenanceScheduleRequest
        {
            MileageAtCompletion = 15000,
            Cost = 49.99m,
            ServiceProvider = "Quick Lube"
        });

        record.Should().NotBeNull();
        record.Description.Should().Be("Oil Change");
        record.MileageAtCompletion.Should().Be(15000);
        record.Cost.Should().Be(49.99m);

        var updated = await _service.GetVehicleAsync(vehicle.Id);
        updated!.CurrentMileage.Should().Be(15000);
    }

    [Fact]
    public async Task CompleteMaintenanceScheduleAsync_VehicleNotFound_ShouldThrow()
    {
        var act = () => _service.CompleteMaintenanceScheduleAsync(Guid.NewGuid(), Guid.NewGuid(), new CompleteMaintenanceScheduleRequest());
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task CompleteMaintenanceScheduleAsync_ScheduleNotFound_ShouldThrow()
    {
        var vehicle = await _service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Year = 2022, Make = "Honda", Model = "Civic"
        });

        var act = () => _service.CompleteMaintenanceScheduleAsync(vehicle.Id, Guid.NewGuid(), new CompleteMaintenanceScheduleRequest());
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
