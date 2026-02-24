using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Vehicles;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Mapping;

public class VehicleMappingProfile : Profile
{
    public VehicleMappingProfile()
    {
        // Vehicle mappings
        CreateMap<CreateVehicleRequest, Vehicle>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.MileageAsOfDate, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.Ignore())
            .ForMember(dest => dest.PrimaryDriver, opt => opt.Ignore())
            .ForMember(dest => dest.MileageLogs, opt => opt.Ignore())
            .ForMember(dest => dest.Documents, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceRecords, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceSchedules, opt => opt.Ignore());

        CreateMap<UpdateVehicleRequest, Vehicle>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.MileageAsOfDate, opt => opt.Ignore())
            .ForMember(dest => dest.PrimaryDriver, opt => opt.Ignore())
            .ForMember(dest => dest.MileageLogs, opt => opt.Ignore())
            .ForMember(dest => dest.Documents, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceRecords, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceSchedules, opt => opt.Ignore());

        // Mileage log mappings
        CreateMap<VehicleMileageLog, VehicleMileageLogDto>();

        // Maintenance record mappings
        CreateMap<CreateMaintenanceRecordRequest, VehicleMaintenanceRecord>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.VehicleId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Vehicle, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceSchedule, opt => opt.Ignore());

        // Maintenance schedule mappings
        CreateMap<CreateMaintenanceScheduleRequest, VehicleMaintenanceSchedule>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.VehicleId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.Ignore())
            .ForMember(dest => dest.Vehicle, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceRecords, opt => opt.Ignore());

        CreateMap<UpdateMaintenanceScheduleRequest, VehicleMaintenanceSchedule>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.VehicleId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.LastCompletedDate, opt => opt.Ignore())
            .ForMember(dest => dest.LastCompletedMileage, opt => opt.Ignore())
            .ForMember(dest => dest.Vehicle, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceRecords, opt => opt.Ignore());

        // Document mappings
        CreateMap<VehicleDocument, VehicleDocumentDto>();
    }
}
