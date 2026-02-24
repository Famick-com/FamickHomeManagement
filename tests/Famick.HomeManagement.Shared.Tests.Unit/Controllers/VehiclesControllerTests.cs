using Famick.HomeManagement.Core.DTOs.Vehicles;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

public class VehiclesControllerTests
{
    private readonly Mock<IVehicleService> _mockService;
    private readonly Mock<ITenantProvider> _mockTenantProvider;
    private readonly VehiclesController _controller;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public VehiclesControllerTests()
    {
        _mockService = new Mock<IVehicleService>();
        _mockTenantProvider = new Mock<ITenantProvider>();
        _mockTenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        var logger = new Mock<ILogger<VehiclesController>>();

        _controller = new VehiclesController(
            _mockService.Object,
            _mockTenantProvider.Object,
            logger.Object);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task GetVehicles_ShouldReturnOk()
    {
        _mockService.Setup(s => s.GetVehiclesAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VehicleSummaryDto> { new() { Id = Guid.NewGuid(), Make = "Toyota" } });

        var result = await _controller.GetVehicles();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetVehicle_Found_ShouldReturnOk()
    {
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.GetVehicleAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VehicleDto { Id = id });

        var result = await _controller.GetVehicle(id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetVehicle_NotFound_ShouldReturn404()
    {
        _mockService.Setup(s => s.GetVehicleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleDto?)null);

        var result = await _controller.GetVehicle(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateVehicle_ShouldReturn201()
    {
        var vehicleId = Guid.NewGuid();
        _mockService.Setup(s => s.CreateVehicleAsync(It.IsAny<CreateVehicleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VehicleDto { Id = vehicleId });

        var result = await _controller.CreateVehicle(new CreateVehicleRequest
        {
            Year = 2023, Make = "Toyota", Model = "Camry"
        });

        result.Should().BeOfType<CreatedAtActionResult>();
        var created = (CreatedAtActionResult)result;
        created.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task UpdateVehicle_ShouldReturnOk()
    {
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.UpdateVehicleAsync(id, It.IsAny<UpdateVehicleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VehicleDto { Id = id });

        var result = await _controller.UpdateVehicle(id, new UpdateVehicleRequest
        {
            Year = 2023, Make = "Toyota", Model = "Camry", IsActive = true
        });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteVehicle_ShouldReturn204()
    {
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.DeleteVehicleAsync(id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.DeleteVehicle(id);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task LogMileage_ShouldReturn201()
    {
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.LogMileageAsync(id, It.IsAny<LogMileageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VehicleMileageLogDto { Id = Guid.NewGuid() });

        var result = await _controller.LogMileage(id, new LogMileageRequest { Mileage = 50000 });

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task GetMileageHistory_ShouldReturnOk()
    {
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.GetMileageHistoryAsync(id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VehicleMileageLogDto>());

        var result = await _controller.GetMileageHistory(id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMaintenanceRecords_ShouldReturnOk()
    {
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.GetMaintenanceRecordsAsync(id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VehicleMaintenanceRecordDto>());

        var result = await _controller.GetMaintenanceRecords(id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateMaintenanceRecord_ShouldReturn201()
    {
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.CreateMaintenanceRecordAsync(id, It.IsAny<CreateMaintenanceRecordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VehicleMaintenanceRecordDto { Id = Guid.NewGuid() });

        var result = await _controller.CreateMaintenanceRecord(id, new CreateMaintenanceRecordRequest
        {
            Description = "Oil Change", CompletedDate = DateTime.UtcNow
        });

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task GetMaintenanceSchedules_ShouldReturnOk()
    {
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.GetMaintenanceSchedulesAsync(id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VehicleMaintenanceScheduleDto>());

        var result = await _controller.GetMaintenanceSchedules(id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateMaintenanceSchedule_ShouldReturn201()
    {
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.CreateMaintenanceScheduleAsync(id, It.IsAny<CreateMaintenanceScheduleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VehicleMaintenanceScheduleDto { Id = Guid.NewGuid() });

        var result = await _controller.CreateMaintenanceSchedule(id, new CreateMaintenanceScheduleRequest { Name = "Oil Change" });

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task UpdateMaintenanceSchedule_ShouldReturnOk()
    {
        var id = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        _mockService.Setup(s => s.UpdateMaintenanceScheduleAsync(id, scheduleId, It.IsAny<UpdateMaintenanceScheduleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VehicleMaintenanceScheduleDto { Id = scheduleId });

        var result = await _controller.UpdateMaintenanceSchedule(id, scheduleId, new UpdateMaintenanceScheduleRequest
        {
            Name = "Updated", IsActive = true
        });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteMaintenanceSchedule_ShouldReturn204()
    {
        var id = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        _mockService.Setup(s => s.DeleteMaintenanceScheduleAsync(id, scheduleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.DeleteMaintenanceSchedule(id, scheduleId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task CompleteMaintenanceSchedule_ShouldReturn201()
    {
        var id = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        _mockService.Setup(s => s.CompleteMaintenanceScheduleAsync(id, scheduleId, It.IsAny<CompleteMaintenanceScheduleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VehicleMaintenanceRecordDto { Id = Guid.NewGuid() });

        var result = await _controller.CompleteMaintenanceSchedule(id, scheduleId, new CompleteMaintenanceScheduleRequest());

        result.Should().BeOfType<CreatedAtActionResult>();
    }
}
