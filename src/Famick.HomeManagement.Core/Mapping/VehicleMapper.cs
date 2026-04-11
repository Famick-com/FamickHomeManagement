#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.Vehicles;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class VehicleMapper
{
    // CreateVehicleRequest -> Vehicle
    [MapperIgnoreTarget(nameof(Vehicle.Id))]
    [MapperIgnoreTarget(nameof(Vehicle.TenantId))]
    [MapperIgnoreTarget(nameof(Vehicle.CreatedAt))]
    [MapperIgnoreTarget(nameof(Vehicle.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Vehicle.MileageAsOfDate))]
    [MapperIgnoreTarget(nameof(Vehicle.IsActive))]
    [MapperIgnoreTarget(nameof(Vehicle.PrimaryDriver))]
    [MapperIgnoreTarget(nameof(Vehicle.MileageLogs))]
    [MapperIgnoreTarget(nameof(Vehicle.Documents))]
    [MapperIgnoreTarget(nameof(Vehicle.MaintenanceRecords))]
    [MapperIgnoreTarget(nameof(Vehicle.MaintenanceSchedules))]
    public static partial Vehicle FromCreateRequest(CreateVehicleRequest source);

    // UpdateVehicleRequest -> Vehicle (new)
    [MapperIgnoreTarget(nameof(Vehicle.Id))]
    [MapperIgnoreTarget(nameof(Vehicle.TenantId))]
    [MapperIgnoreTarget(nameof(Vehicle.CreatedAt))]
    [MapperIgnoreTarget(nameof(Vehicle.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Vehicle.MileageAsOfDate))]
    [MapperIgnoreTarget(nameof(Vehicle.PrimaryDriver))]
    [MapperIgnoreTarget(nameof(Vehicle.MileageLogs))]
    [MapperIgnoreTarget(nameof(Vehicle.Documents))]
    [MapperIgnoreTarget(nameof(Vehicle.MaintenanceRecords))]
    [MapperIgnoreTarget(nameof(Vehicle.MaintenanceSchedules))]
    public static partial Vehicle FromUpdateRequest(UpdateVehicleRequest source);

    // UpdateVehicleRequest -> Vehicle (in-place)
    [MapperIgnoreTarget(nameof(Vehicle.Id))]
    [MapperIgnoreTarget(nameof(Vehicle.TenantId))]
    [MapperIgnoreTarget(nameof(Vehicle.CreatedAt))]
    [MapperIgnoreTarget(nameof(Vehicle.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Vehicle.MileageAsOfDate))]
    [MapperIgnoreTarget(nameof(Vehicle.PrimaryDriver))]
    [MapperIgnoreTarget(nameof(Vehicle.MileageLogs))]
    [MapperIgnoreTarget(nameof(Vehicle.Documents))]
    [MapperIgnoreTarget(nameof(Vehicle.MaintenanceRecords))]
    [MapperIgnoreTarget(nameof(Vehicle.MaintenanceSchedules))]
    public static partial void ApplyUpdateRequest(UpdateVehicleRequest source, Vehicle target);

    // VehicleMileageLog -> VehicleMileageLogDto
    public static partial VehicleMileageLogDto ToMileageLogDto(VehicleMileageLog source);

    // CreateMaintenanceRecordRequest -> VehicleMaintenanceRecord
    [MapperIgnoreTarget(nameof(VehicleMaintenanceRecord.Id))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceRecord.TenantId))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceRecord.VehicleId))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceRecord.CreatedAt))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceRecord.UpdatedAt))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceRecord.Vehicle))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceRecord.MaintenanceSchedule))]
    public static partial VehicleMaintenanceRecord FromCreateMaintenanceRecordRequest(CreateMaintenanceRecordRequest source);

    // CreateMaintenanceScheduleRequest -> VehicleMaintenanceSchedule
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.Id))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.TenantId))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.VehicleId))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.CreatedAt))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.UpdatedAt))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.IsActive))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.Vehicle))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.MaintenanceRecords))]
    public static partial VehicleMaintenanceSchedule FromCreateMaintenanceScheduleRequest(CreateMaintenanceScheduleRequest source);

    // UpdateMaintenanceScheduleRequest -> VehicleMaintenanceSchedule (new)
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.Id))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.TenantId))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.VehicleId))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.CreatedAt))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.UpdatedAt))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.LastCompletedDate))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.LastCompletedMileage))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.Vehicle))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.MaintenanceRecords))]
    public static partial VehicleMaintenanceSchedule FromUpdateMaintenanceScheduleRequest(UpdateMaintenanceScheduleRequest source);

    // UpdateMaintenanceScheduleRequest -> VehicleMaintenanceSchedule (in-place)
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.Id))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.TenantId))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.VehicleId))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.CreatedAt))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.UpdatedAt))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.LastCompletedDate))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.LastCompletedMileage))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.Vehicle))]
    [MapperIgnoreTarget(nameof(VehicleMaintenanceSchedule.MaintenanceRecords))]
    public static partial void ApplyUpdateMaintenanceScheduleRequest(UpdateMaintenanceScheduleRequest source, VehicleMaintenanceSchedule target);

    // VehicleDocument -> VehicleDocumentDto
    public static partial VehicleDocumentDto ToDocumentDto(VehicleDocument source);
}
