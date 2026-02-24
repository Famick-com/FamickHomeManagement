using Famick.HomeManagement.Core.DTOs.Vehicles;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing vehicles, mileage tracking, and maintenance schedules
/// </summary>
public interface IVehicleService
{
    #region Vehicle CRUD

    /// <summary>
    /// Gets all vehicles for the current tenant
    /// </summary>
    Task<List<VehicleSummaryDto>> GetVehiclesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a vehicle by ID with full details
    /// </summary>
    Task<VehicleDto?> GetVehicleAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new vehicle
    /// </summary>
    Task<VehicleDto> CreateVehicleAsync(
        CreateVehicleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing vehicle
    /// </summary>
    Task<VehicleDto> UpdateVehicleAsync(
        Guid vehicleId,
        UpdateVehicleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a vehicle
    /// </summary>
    Task DeleteVehicleAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Mileage Tracking

    /// <summary>
    /// Logs a mileage reading for a vehicle
    /// </summary>
    Task<VehicleMileageLogDto> LogMileageAsync(
        Guid vehicleId,
        LogMileageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets mileage history for a vehicle
    /// </summary>
    Task<List<VehicleMileageLogDto>> GetMileageHistoryAsync(
        Guid vehicleId,
        int? limit = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region Maintenance Records

    /// <summary>
    /// Gets maintenance records for a vehicle
    /// </summary>
    Task<List<VehicleMaintenanceRecordDto>> GetMaintenanceRecordsAsync(
        Guid vehicleId,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a maintenance record
    /// </summary>
    Task<VehicleMaintenanceRecordDto> CreateMaintenanceRecordAsync(
        Guid vehicleId,
        CreateMaintenanceRecordRequest request,
        CancellationToken cancellationToken = default);

    #endregion

    #region Maintenance Schedules

    /// <summary>
    /// Gets maintenance schedules for a vehicle
    /// </summary>
    Task<List<VehicleMaintenanceScheduleDto>> GetMaintenanceSchedulesAsync(
        Guid vehicleId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a maintenance schedule
    /// </summary>
    Task<VehicleMaintenanceScheduleDto> CreateMaintenanceScheduleAsync(
        Guid vehicleId,
        CreateMaintenanceScheduleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a maintenance schedule
    /// </summary>
    Task<VehicleMaintenanceScheduleDto> UpdateMaintenanceScheduleAsync(
        Guid vehicleId,
        Guid scheduleId,
        UpdateMaintenanceScheduleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a maintenance schedule
    /// </summary>
    Task DeleteMaintenanceScheduleAsync(
        Guid vehicleId,
        Guid scheduleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a maintenance schedule as completed and creates a maintenance record
    /// </summary>
    Task<VehicleMaintenanceRecordDto> CompleteMaintenanceScheduleAsync(
        Guid vehicleId,
        Guid scheduleId,
        CompleteMaintenanceScheduleRequest request,
        CancellationToken cancellationToken = default);

    #endregion
}
