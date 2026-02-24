using Famick.HomeManagement.Core.DTOs.Vehicles;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing vehicles, mileage tracking, and maintenance schedules
/// </summary>
[ApiController]
[Route("api/v1/vehicles")]
[Authorize]
public class VehiclesController : ApiControllerBase
{
    private readonly IVehicleService _vehicleService;

    public VehiclesController(
        IVehicleService vehicleService,
        ITenantProvider tenantProvider,
        ILogger<VehiclesController> logger)
        : base(tenantProvider, logger)
    {
        _vehicleService = vehicleService;
    }

    #region Vehicle CRUD

    /// <summary>
    /// Gets all vehicles for the current tenant
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<VehicleSummaryDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetVehicles(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting vehicles for tenant {TenantId}", TenantId);

        var vehicles = await _vehicleService.GetVehiclesAsync(includeInactive, cancellationToken);

        return ApiResponse(vehicles);
    }

    /// <summary>
    /// Gets a vehicle by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(VehicleDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetVehicle(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting vehicle {VehicleId} for tenant {TenantId}", id, TenantId);

        var vehicle = await _vehicleService.GetVehicleAsync(id, cancellationToken);

        if (vehicle == null)
        {
            return NotFoundResponse("Vehicle not found");
        }

        return ApiResponse(vehicle);
    }

    /// <summary>
    /// Creates a new vehicle
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(VehicleDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CreateVehicle(
        [FromBody] CreateVehicleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating vehicle {Year} {Make} {Model} for tenant {TenantId}",
            request.Year, request.Make, request.Model, TenantId);

        var vehicle = await _vehicleService.CreateVehicleAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetVehicle), new { id = vehicle.Id }, vehicle);
    }

    /// <summary>
    /// Updates a vehicle
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(VehicleDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> UpdateVehicle(
        Guid id,
        [FromBody] UpdateVehicleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating vehicle {VehicleId} for tenant {TenantId}", id, TenantId);

        var vehicle = await _vehicleService.UpdateVehicleAsync(id, request, cancellationToken);

        return ApiResponse(vehicle);
    }

    /// <summary>
    /// Deletes a vehicle
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteVehicle(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting vehicle {VehicleId} for tenant {TenantId}", id, TenantId);

        await _vehicleService.DeleteVehicleAsync(id, cancellationToken);

        return NoContent();
    }

    #endregion

    #region Mileage Tracking

    /// <summary>
    /// Logs a mileage reading
    /// </summary>
    [HttpPost("{id}/mileage")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(VehicleMileageLogDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> LogMileage(
        Guid id,
        [FromBody] LogMileageRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Logging mileage {Mileage} for vehicle {VehicleId}", request.Mileage, id);

        var log = await _vehicleService.LogMileageAsync(id, request, cancellationToken);

        return CreatedAtAction(nameof(GetVehicle), new { id }, log);
    }

    /// <summary>
    /// Gets mileage history for a vehicle
    /// </summary>
    [HttpGet("{id}/mileage")]
    [ProducesResponseType(typeof(List<VehicleMileageLogDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMileageHistory(
        Guid id,
        [FromQuery] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting mileage history for vehicle {VehicleId}", id);

        var logs = await _vehicleService.GetMileageHistoryAsync(id, limit, cancellationToken);

        return ApiResponse(logs);
    }

    #endregion

    #region Maintenance Records

    /// <summary>
    /// Gets maintenance records for a vehicle
    /// </summary>
    [HttpGet("{id}/maintenance")]
    [ProducesResponseType(typeof(List<VehicleMaintenanceRecordDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMaintenanceRecords(
        Guid id,
        [FromQuery] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting maintenance records for vehicle {VehicleId}", id);

        var records = await _vehicleService.GetMaintenanceRecordsAsync(id, limit, cancellationToken);

        return ApiResponse(records);
    }

    /// <summary>
    /// Creates a maintenance record
    /// </summary>
    [HttpPost("{id}/maintenance")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(VehicleMaintenanceRecordDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CreateMaintenanceRecord(
        Guid id,
        [FromBody] CreateMaintenanceRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating maintenance record for vehicle {VehicleId}: {Description}", id, request.Description);

        var record = await _vehicleService.CreateMaintenanceRecordAsync(id, request, cancellationToken);

        return CreatedAtAction(nameof(GetVehicle), new { id }, record);
    }

    #endregion

    #region Maintenance Schedules

    /// <summary>
    /// Gets maintenance schedules for a vehicle
    /// </summary>
    [HttpGet("{id}/schedules")]
    [ProducesResponseType(typeof(List<VehicleMaintenanceScheduleDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMaintenanceSchedules(
        Guid id,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting maintenance schedules for vehicle {VehicleId}", id);

        var schedules = await _vehicleService.GetMaintenanceSchedulesAsync(id, includeInactive, cancellationToken);

        return ApiResponse(schedules);
    }

    /// <summary>
    /// Creates a maintenance schedule
    /// </summary>
    [HttpPost("{id}/schedules")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(VehicleMaintenanceScheduleDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CreateMaintenanceSchedule(
        Guid id,
        [FromBody] CreateMaintenanceScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating maintenance schedule for vehicle {VehicleId}: {Name}", id, request.Name);

        var schedule = await _vehicleService.CreateMaintenanceScheduleAsync(id, request, cancellationToken);

        return CreatedAtAction(nameof(GetVehicle), new { id }, schedule);
    }

    /// <summary>
    /// Updates a maintenance schedule
    /// </summary>
    [HttpPut("{id}/schedules/{scheduleId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(VehicleMaintenanceScheduleDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> UpdateMaintenanceSchedule(
        Guid id,
        Guid scheduleId,
        [FromBody] UpdateMaintenanceScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating maintenance schedule {ScheduleId} for vehicle {VehicleId}", scheduleId, id);

        var schedule = await _vehicleService.UpdateMaintenanceScheduleAsync(id, scheduleId, request, cancellationToken);

        return ApiResponse(schedule);
    }

    /// <summary>
    /// Deletes a maintenance schedule
    /// </summary>
    [HttpDelete("{id}/schedules/{scheduleId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteMaintenanceSchedule(
        Guid id,
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting maintenance schedule {ScheduleId} for vehicle {VehicleId}", scheduleId, id);

        await _vehicleService.DeleteMaintenanceScheduleAsync(id, scheduleId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Marks a maintenance schedule as completed
    /// </summary>
    [HttpPost("{id}/schedules/{scheduleId}/complete")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(VehicleMaintenanceRecordDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CompleteMaintenanceSchedule(
        Guid id,
        Guid scheduleId,
        [FromBody] CompleteMaintenanceScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Completing maintenance schedule {ScheduleId} for vehicle {VehicleId}", scheduleId, id);

        var record = await _vehicleService.CompleteMaintenanceScheduleAsync(id, scheduleId, request, cancellationToken);

        return CreatedAtAction(nameof(GetVehicle), new { id }, record);
    }

    #endregion
}
