using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Vehicles;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class VehicleService : IVehicleService
{
    private readonly HomeManagementDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<VehicleService> _logger;

    public VehicleService(
        HomeManagementDbContext context,
        IMapper mapper,
        ILogger<VehicleService> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    #region Vehicle CRUD

    public async Task<List<VehicleSummaryDto>> GetVehiclesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting vehicles for current tenant");

        var query = _context.Vehicles
            .Include(v => v.PrimaryDriver)
            .Include(v => v.MaintenanceSchedules.Where(s => s.IsActive))
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(v => v.IsActive);
        }

        var vehicles = await query
            .OrderBy(v => v.Year)
            .ThenBy(v => v.Make)
            .ThenBy(v => v.Model)
            .ToListAsync(cancellationToken);

        return vehicles.Select(v => MapToSummary(v)).ToList();
    }

    public async Task<VehicleDto?> GetVehicleAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting vehicle: {VehicleId}", vehicleId);

        var vehicle = await _context.Vehicles
            .Include(v => v.PrimaryDriver)
            .Include(v => v.MaintenanceSchedules)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle == null)
        {
            return null;
        }

        return MapToDto(vehicle);
    }

    public async Task<VehicleDto> CreateVehicleAsync(
        CreateVehicleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating vehicle: {Year} {Make} {Model}", request.Year, request.Make, request.Model);

        // Check for duplicate VIN if provided
        if (!string.IsNullOrWhiteSpace(request.Vin))
        {
            var existingVin = await _context.Vehicles
                .AnyAsync(v => v.Vin == request.Vin, cancellationToken);
            if (existingVin)
            {
                throw new DuplicateEntityException("Vehicle", "Vin", request.Vin);
            }
        }

        var vehicle = _mapper.Map<Vehicle>(request);
        vehicle.Id = Guid.NewGuid();

        // Set mileage date if mileage provided
        if (request.CurrentMileage.HasValue)
        {
            vehicle.MileageAsOfDate = DateTime.UtcNow;
        }

        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vehicle created: {Id}", vehicle.Id);

        // Reload with navigation properties
        return (await GetVehicleAsync(vehicle.Id, cancellationToken))!;
    }

    public async Task<VehicleDto> UpdateVehicleAsync(
        Guid vehicleId,
        UpdateVehicleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating vehicle: {VehicleId}", vehicleId);

        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle == null)
        {
            throw new EntityNotFoundException(nameof(Vehicle), vehicleId);
        }

        // Check for duplicate VIN if changing
        if (!string.IsNullOrWhiteSpace(request.Vin) && request.Vin != vehicle.Vin)
        {
            var existingVin = await _context.Vehicles
                .AnyAsync(v => v.Id != vehicleId && v.Vin == request.Vin, cancellationToken);
            if (existingVin)
            {
                throw new DuplicateEntityException("Vehicle", "Vin", request.Vin);
            }
        }

        // Track mileage update
        var oldMileage = vehicle.CurrentMileage;
        _mapper.Map(request, vehicle);

        // Update mileage date if mileage changed
        if (request.CurrentMileage != oldMileage && request.CurrentMileage.HasValue)
        {
            vehicle.MileageAsOfDate = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vehicle updated: {Id}", vehicle.Id);

        return (await GetVehicleAsync(vehicle.Id, cancellationToken))!;
    }

    public async Task DeleteVehicleAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting vehicle: {VehicleId}", vehicleId);

        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle == null)
        {
            throw new EntityNotFoundException(nameof(Vehicle), vehicleId);
        }

        _context.Vehicles.Remove(vehicle);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vehicle deleted: {Id}", vehicleId);
    }

    #endregion

    #region Mileage Tracking

    public async Task<VehicleMileageLogDto> LogMileageAsync(
        Guid vehicleId,
        LogMileageRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Logging mileage for vehicle {VehicleId}: {Mileage}", vehicleId, request.Mileage);

        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle == null)
        {
            throw new EntityNotFoundException(nameof(Vehicle), vehicleId);
        }

        var log = new VehicleMileageLog
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            Mileage = request.Mileage,
            ReadingDate = request.ReadingDate ?? DateTime.UtcNow,
            Notes = request.Notes
        };

        _context.VehicleMileageLogs.Add(log);

        // Update vehicle's current mileage
        vehicle.CurrentMileage = request.Mileage;
        vehicle.MileageAsOfDate = log.ReadingDate;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mileage logged: {LogId}", log.Id);

        return _mapper.Map<VehicleMileageLogDto>(log);
    }

    public async Task<List<VehicleMileageLogDto>> GetMileageHistoryAsync(
        Guid vehicleId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting mileage history for vehicle: {VehicleId}", vehicleId);

        var query = _context.VehicleMileageLogs
            .Where(m => m.VehicleId == vehicleId)
            .OrderByDescending(m => m.ReadingDate)
            .AsQueryable();

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        var logs = await query.ToListAsync(cancellationToken);

        return _mapper.Map<List<VehicleMileageLogDto>>(logs);
    }

    #endregion

    #region Maintenance Records

    public async Task<List<VehicleMaintenanceRecordDto>> GetMaintenanceRecordsAsync(
        Guid vehicleId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting maintenance records for vehicle: {VehicleId}", vehicleId);

        var query = _context.VehicleMaintenanceRecords
            .Include(r => r.MaintenanceSchedule)
            .Where(r => r.VehicleId == vehicleId)
            .OrderByDescending(r => r.CompletedDate)
            .AsQueryable();

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        var records = await query.ToListAsync(cancellationToken);

        return records.Select(r => MapToRecordDto(r)).ToList();
    }

    public async Task<VehicleMaintenanceRecordDto> CreateMaintenanceRecordAsync(
        Guid vehicleId,
        CreateMaintenanceRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating maintenance record for vehicle: {VehicleId}", vehicleId);

        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle == null)
        {
            throw new EntityNotFoundException(nameof(Vehicle), vehicleId);
        }

        var record = _mapper.Map<VehicleMaintenanceRecord>(request);
        record.Id = Guid.NewGuid();
        record.VehicleId = vehicleId;

        _context.VehicleMaintenanceRecords.Add(record);

        // Update vehicle mileage if provided
        if (request.MileageAtCompletion.HasValue)
        {
            vehicle.CurrentMileage = request.MileageAtCompletion;
            vehicle.MileageAsOfDate = request.CompletedDate;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Maintenance record created: {RecordId}", record.Id);

        return MapToRecordDto(record);
    }

    #endregion

    #region Maintenance Schedules

    public async Task<List<VehicleMaintenanceScheduleDto>> GetMaintenanceSchedulesAsync(
        Guid vehicleId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting maintenance schedules for vehicle: {VehicleId}", vehicleId);

        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle == null)
        {
            throw new EntityNotFoundException(nameof(Vehicle), vehicleId);
        }

        var query = _context.VehicleMaintenanceSchedules
            .Where(s => s.VehicleId == vehicleId)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        var schedules = await query
            .OrderBy(s => s.NextDueDate ?? DateTime.MaxValue)
            .ThenBy(s => s.NextDueMileage ?? int.MaxValue)
            .ToListAsync(cancellationToken);

        return schedules.Select(s => MapToScheduleDto(s, vehicle.CurrentMileage)).ToList();
    }

    public async Task<VehicleMaintenanceScheduleDto> CreateMaintenanceScheduleAsync(
        Guid vehicleId,
        CreateMaintenanceScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating maintenance schedule for vehicle {VehicleId}: {Name}", vehicleId, request.Name);

        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle == null)
        {
            throw new EntityNotFoundException(nameof(Vehicle), vehicleId);
        }

        // Check for duplicate name
        var exists = await _context.VehicleMaintenanceSchedules
            .AnyAsync(s => s.VehicleId == vehicleId && s.Name == request.Name, cancellationToken);
        if (exists)
        {
            throw new DuplicateEntityException("VehicleMaintenanceSchedule", "Name", request.Name);
        }

        var schedule = _mapper.Map<VehicleMaintenanceSchedule>(request);
        schedule.Id = Guid.NewGuid();
        schedule.VehicleId = vehicleId;

        // Calculate next due if not manually set
        if (!request.NextDueDate.HasValue)
        {
            schedule.CalculateNextDueDate();
        }
        if (!request.NextDueMileage.HasValue)
        {
            schedule.CalculateNextDueMileage();
        }

        _context.VehicleMaintenanceSchedules.Add(schedule);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Maintenance schedule created: {ScheduleId}", schedule.Id);

        return MapToScheduleDto(schedule, vehicle.CurrentMileage);
    }

    public async Task<VehicleMaintenanceScheduleDto> UpdateMaintenanceScheduleAsync(
        Guid vehicleId,
        Guid scheduleId,
        UpdateMaintenanceScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating maintenance schedule: {ScheduleId}", scheduleId);

        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle == null)
        {
            throw new EntityNotFoundException(nameof(Vehicle), vehicleId);
        }

        var schedule = await _context.VehicleMaintenanceSchedules
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.VehicleId == vehicleId, cancellationToken);

        if (schedule == null)
        {
            throw new EntityNotFoundException(nameof(VehicleMaintenanceSchedule), scheduleId);
        }

        // Check for duplicate name if changing
        if (request.Name != schedule.Name)
        {
            var exists = await _context.VehicleMaintenanceSchedules
                .AnyAsync(s => s.VehicleId == vehicleId && s.Name == request.Name && s.Id != scheduleId, cancellationToken);
            if (exists)
            {
                throw new DuplicateEntityException("VehicleMaintenanceSchedule", "Name", request.Name);
            }
        }

        _mapper.Map(request, schedule);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Maintenance schedule updated: {ScheduleId}", schedule.Id);

        return MapToScheduleDto(schedule, vehicle.CurrentMileage);
    }

    public async Task DeleteMaintenanceScheduleAsync(
        Guid vehicleId,
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting maintenance schedule: {ScheduleId}", scheduleId);

        var schedule = await _context.VehicleMaintenanceSchedules
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.VehicleId == vehicleId, cancellationToken);

        if (schedule == null)
        {
            throw new EntityNotFoundException(nameof(VehicleMaintenanceSchedule), scheduleId);
        }

        _context.VehicleMaintenanceSchedules.Remove(schedule);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Maintenance schedule deleted: {ScheduleId}", scheduleId);
    }

    public async Task<VehicleMaintenanceRecordDto> CompleteMaintenanceScheduleAsync(
        Guid vehicleId,
        Guid scheduleId,
        CompleteMaintenanceScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Completing maintenance schedule: {ScheduleId}", scheduleId);

        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle == null)
        {
            throw new EntityNotFoundException(nameof(Vehicle), vehicleId);
        }

        var schedule = await _context.VehicleMaintenanceSchedules
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.VehicleId == vehicleId, cancellationToken);

        if (schedule == null)
        {
            throw new EntityNotFoundException(nameof(VehicleMaintenanceSchedule), scheduleId);
        }

        var completedDate = request.CompletedDate ?? DateTime.UtcNow;

        // Create maintenance record
        var record = new VehicleMaintenanceRecord
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            Description = schedule.Name,
            CompletedDate = completedDate,
            MileageAtCompletion = request.MileageAtCompletion,
            Cost = request.Cost,
            ServiceProvider = request.ServiceProvider,
            Notes = request.Notes,
            MaintenanceScheduleId = scheduleId
        };

        _context.VehicleMaintenanceRecords.Add(record);

        // Update schedule with completion info and calculate next due
        schedule.MarkCompleted(completedDate, request.MileageAtCompletion);

        // Update vehicle mileage if provided
        if (request.MileageAtCompletion.HasValue)
        {
            vehicle.CurrentMileage = request.MileageAtCompletion;
            vehicle.MileageAsOfDate = completedDate;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Maintenance schedule completed: {ScheduleId}, Record: {RecordId}", scheduleId, record.Id);

        return MapToRecordDto(record);
    }

    #endregion

    #region Private Mapping Methods

    private VehicleSummaryDto MapToSummary(Vehicle vehicle)
    {
        var nextSchedule = vehicle.MaintenanceSchedules?
            .Where(s => s.IsActive)
            .OrderBy(s => s.NextDueDate ?? DateTime.MaxValue)
            .ThenBy(s => s.NextDueMileage ?? int.MaxValue)
            .FirstOrDefault();

        return new VehicleSummaryDto
        {
            Id = vehicle.Id,
            Year = vehicle.Year,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Trim = vehicle.Trim,
            LicensePlate = vehicle.LicensePlate,
            Color = vehicle.Color,
            CurrentMileage = vehicle.CurrentMileage,
            PrimaryDriverName = vehicle.PrimaryDriver?.DisplayName,
            IsActive = vehicle.IsActive,
            DisplayName = vehicle.DisplayName,
            NextMaintenanceDueDate = nextSchedule?.NextDueDate,
            NextMaintenanceDueMileage = nextSchedule?.NextDueMileage
        };
    }

    private VehicleDto MapToDto(Vehicle vehicle)
    {
        return new VehicleDto
        {
            Id = vehicle.Id,
            Year = vehicle.Year,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Trim = vehicle.Trim,
            Vin = vehicle.Vin,
            LicensePlate = vehicle.LicensePlate,
            Color = vehicle.Color,
            CurrentMileage = vehicle.CurrentMileage,
            MileageAsOfDate = vehicle.MileageAsOfDate,
            PrimaryDriverContactId = vehicle.PrimaryDriverContactId,
            PrimaryDriverName = vehicle.PrimaryDriver?.DisplayName,
            PurchaseDate = vehicle.PurchaseDate,
            PurchasePrice = vehicle.PurchasePrice,
            PurchaseLocation = vehicle.PurchaseLocation,
            IsActive = vehicle.IsActive,
            Notes = vehicle.Notes,
            DisplayName = vehicle.DisplayName,
            FullName = vehicle.FullName,
            MaintenanceSchedules = vehicle.MaintenanceSchedules?
                .Select(s => MapToScheduleDto(s, vehicle.CurrentMileage))
                .ToList() ?? new List<VehicleMaintenanceScheduleDto>(),
            CreatedAt = vehicle.CreatedAt,
            UpdatedAt = vehicle.UpdatedAt
        };
    }

    private VehicleMaintenanceRecordDto MapToRecordDto(VehicleMaintenanceRecord record)
    {
        return new VehicleMaintenanceRecordDto
        {
            Id = record.Id,
            VehicleId = record.VehicleId,
            Description = record.Description,
            CompletedDate = record.CompletedDate,
            MileageAtCompletion = record.MileageAtCompletion,
            Cost = record.Cost,
            ServiceProvider = record.ServiceProvider,
            Notes = record.Notes,
            MaintenanceScheduleId = record.MaintenanceScheduleId,
            MaintenanceScheduleName = record.MaintenanceSchedule?.Name,
            CreatedAt = record.CreatedAt
        };
    }

    private VehicleMaintenanceScheduleDto MapToScheduleDto(VehicleMaintenanceSchedule schedule, int? currentMileage)
    {
        var now = DateTime.UtcNow;
        var isOverdue = (schedule.NextDueDate.HasValue && schedule.NextDueDate.Value < now) ||
                        (schedule.NextDueMileage.HasValue && currentMileage.HasValue && schedule.NextDueMileage.Value < currentMileage.Value);

        var isDueSoon = !isOverdue &&
                        ((schedule.NextDueDate.HasValue && schedule.NextDueDate.Value < now.AddDays(30)) ||
                         (schedule.NextDueMileage.HasValue && currentMileage.HasValue && schedule.NextDueMileage.Value < currentMileage.Value + 1000));

        return new VehicleMaintenanceScheduleDto
        {
            Id = schedule.Id,
            VehicleId = schedule.VehicleId,
            Name = schedule.Name,
            Description = schedule.Description,
            IntervalMonths = schedule.IntervalMonths,
            IntervalMiles = schedule.IntervalMiles,
            LastCompletedDate = schedule.LastCompletedDate,
            LastCompletedMileage = schedule.LastCompletedMileage,
            NextDueDate = schedule.NextDueDate,
            NextDueMileage = schedule.NextDueMileage,
            IsActive = schedule.IsActive,
            Notes = schedule.Notes,
            CreatedAt = schedule.CreatedAt,
            UpdatedAt = schedule.UpdatedAt,
            IsOverdue = isOverdue,
            IsDueSoon = isDueSoon
        };
    }

    #endregion
}
