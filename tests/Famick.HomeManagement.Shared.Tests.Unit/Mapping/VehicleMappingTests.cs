using Famick.HomeManagement.Core.DTOs.Vehicles;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class VehicleMappingTests
{

    #region CreateVehicleRequest -> Vehicle

    [Fact]
    public void CreateVehicleRequest_To_Vehicle_MapsAllProperties()
    {
        var driverContactId = Guid.NewGuid();
        var request = new CreateVehicleRequest
        {
            Year = 2024,
            Make = "Toyota",
            Model = "Camry",
            Trim = "SE",
            Vin = "1HGBH41JXMN109186",
            LicensePlate = "ABC-1234",
            Color = "Silver",
            CurrentMileage = 15000,
            PrimaryDriverContactId = driverContactId,
            PurchaseDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            PurchasePrice = 28500.00m,
            PurchaseLocation = "Toyota of Springfield",
            Notes = "Extended warranty included"
        };

        var entity = VehicleMapper.FromCreateRequest(request);

        entity.Year.Should().Be(2024);
        entity.Make.Should().Be("Toyota");
        entity.Model.Should().Be("Camry");
        entity.Trim.Should().Be("SE");
        entity.Vin.Should().Be("1HGBH41JXMN109186");
        entity.LicensePlate.Should().Be("ABC-1234");
        entity.Color.Should().Be("Silver");
        entity.CurrentMileage.Should().Be(15000);
        entity.PrimaryDriverContactId.Should().Be(driverContactId);
        entity.PurchaseDate.Should().Be(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        entity.PurchasePrice.Should().Be(28500.00m);
        entity.PurchaseLocation.Should().Be("Toyota of Springfield");
        entity.Notes.Should().Be("Extended warranty included");
    }

    [Fact]
    public void CreateVehicleRequest_To_Vehicle_IgnoresSystemFields()
    {
        var request = new CreateVehicleRequest
        {
            Year = 2023,
            Make = "Ford",
            Model = "F-150"
        };

        var entity = VehicleMapper.FromCreateRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.CreatedAt.Should().NotBe(default);
        entity.UpdatedAt.Should().BeNull();
        entity.MileageAsOfDate.Should().BeNull();
        entity.IsActive.Should().BeTrue(); // default from entity
        entity.PrimaryDriver.Should().BeNull();
        entity.MileageLogs.Should().BeEmpty();
        entity.Documents.Should().BeEmpty();
        entity.MaintenanceRecords.Should().BeEmpty();
        entity.MaintenanceSchedules.Should().BeEmpty();
    }

    [Fact]
    public void CreateVehicleRequest_To_Vehicle_NullOptionalFields()
    {
        var request = new CreateVehicleRequest
        {
            Year = 2022,
            Make = "Honda",
            Model = "Civic",
            Trim = null,
            Vin = null,
            LicensePlate = null,
            Color = null,
            CurrentMileage = null,
            PrimaryDriverContactId = null,
            PurchaseDate = null,
            PurchasePrice = null,
            PurchaseLocation = null,
            Notes = null
        };

        var entity = VehicleMapper.FromCreateRequest(request);

        entity.Trim.Should().BeNull();
        entity.Vin.Should().BeNull();
        entity.LicensePlate.Should().BeNull();
        entity.Color.Should().BeNull();
        entity.CurrentMileage.Should().BeNull();
        entity.PrimaryDriverContactId.Should().BeNull();
        entity.PurchaseDate.Should().BeNull();
        entity.PurchasePrice.Should().BeNull();
        entity.PurchaseLocation.Should().BeNull();
        entity.Notes.Should().BeNull();
    }

    #endregion

    #region UpdateVehicleRequest -> Vehicle

    [Fact]
    public void UpdateVehicleRequest_To_Vehicle_MapsAllProperties()
    {
        var request = new UpdateVehicleRequest
        {
            Year = 2025,
            Make = "Tesla",
            Model = "Model 3",
            Trim = "Long Range",
            Vin = "5YJ3E1EA1NF000001",
            LicensePlate = "EV-9999",
            Color = "White",
            CurrentMileage = 5000,
            PrimaryDriverContactId = Guid.NewGuid(),
            PurchaseDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            PurchasePrice = 42000m,
            PurchaseLocation = "Tesla Online",
            Notes = "Home delivery",
            IsActive = true
        };

        var entity = VehicleMapper.FromUpdateRequest(request);

        entity.Year.Should().Be(2025);
        entity.Make.Should().Be("Tesla");
        entity.Model.Should().Be("Model 3");
        entity.Trim.Should().Be("Long Range");
        entity.Vin.Should().Be("5YJ3E1EA1NF000001");
        entity.LicensePlate.Should().Be("EV-9999");
        entity.Color.Should().Be("White");
        entity.CurrentMileage.Should().Be(5000);
        entity.PrimaryDriverContactId.Should().Be(request.PrimaryDriverContactId);
        entity.PurchaseDate.Should().Be(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.PurchasePrice.Should().Be(42000m);
        entity.PurchaseLocation.Should().Be("Tesla Online");
        entity.Notes.Should().Be("Home delivery");
        entity.IsActive.Should().BeTrue();
    }

    [Fact]
    public void UpdateVehicleRequest_To_Vehicle_IgnoresSystemFields()
    {
        var existingId = Guid.NewGuid();
        var existingTenantId = Guid.NewGuid();
        var mileageDate = DateTime.UtcNow.AddDays(-1);
        var existing = new Vehicle
        {
            Id = existingId,
            TenantId = existingTenantId,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            MileageAsOfDate = mileageDate,
            MileageLogs = new List<VehicleMileageLog> { new() },
            Documents = new List<VehicleDocument> { new() },
            MaintenanceRecords = new List<VehicleMaintenanceRecord> { new() },
            MaintenanceSchedules = new List<VehicleMaintenanceSchedule> { new() }
        };

        var request = new UpdateVehicleRequest
        {
            Year = 2024,
            Make = "Updated Make",
            Model = "Updated Model",
            IsActive = false
        };

        VehicleMapper.ApplyUpdateRequest(request, existing);

        existing.Id.Should().Be(existingId);
        existing.TenantId.Should().Be(existingTenantId);
        existing.MileageAsOfDate.Should().Be(mileageDate);
        existing.Make.Should().Be("Updated Make");
        existing.Model.Should().Be("Updated Model");
        existing.IsActive.Should().BeFalse();
        existing.MileageLogs.Should().HaveCount(1);
        existing.Documents.Should().HaveCount(1);
        existing.MaintenanceRecords.Should().HaveCount(1);
        existing.MaintenanceSchedules.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateVehicleRequest_To_Vehicle_DeactivateVehicle()
    {
        var existing = new Vehicle
        {
            Id = Guid.NewGuid(),
            Year = 2020,
            Make = "Chevrolet",
            Model = "Malibu",
            IsActive = true
        };

        var request = new UpdateVehicleRequest
        {
            Year = 2020,
            Make = "Chevrolet",
            Model = "Malibu",
            IsActive = false
        };

        VehicleMapper.ApplyUpdateRequest(request, existing);

        existing.IsActive.Should().BeFalse();
    }

    #endregion

    #region VehicleMileageLog -> VehicleMileageLogDto

    [Fact]
    public void VehicleMileageLog_To_VehicleMileageLogDto_MapsAllProperties()
    {
        var vehicleId = Guid.NewGuid();
        var readingDate = new DateTime(2025, 3, 15, 8, 0, 0, DateTimeKind.Utc);
        var createdAt = DateTime.UtcNow;

        var entity = new VehicleMileageLog
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            Mileage = 52340,
            ReadingDate = readingDate,
            Notes = "At oil change",
            CreatedAt = createdAt
        };

        var dto = VehicleMapper.ToMileageLogDto(entity);

        dto.Id.Should().Be(entity.Id);
        dto.VehicleId.Should().Be(vehicleId);
        dto.Mileage.Should().Be(52340);
        dto.ReadingDate.Should().Be(readingDate);
        dto.Notes.Should().Be("At oil change");
        dto.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void VehicleMileageLog_To_VehicleMileageLogDto_NullNotes()
    {
        var entity = new VehicleMileageLog
        {
            Id = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            Mileage = 10000,
            ReadingDate = DateTime.UtcNow,
            Notes = null,
            CreatedAt = DateTime.UtcNow
        };

        var dto = VehicleMapper.ToMileageLogDto(entity);

        dto.Notes.Should().BeNull();
    }

    #endregion

    #region CreateMaintenanceRecordRequest -> VehicleMaintenanceRecord

    [Fact]
    public void CreateMaintenanceRecordRequest_To_VehicleMaintenanceRecord_MapsAllProperties()
    {
        var scheduleId = Guid.NewGuid();
        var completedDate = new DateTime(2025, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        var request = new CreateMaintenanceRecordRequest
        {
            Description = "Oil change - full synthetic",
            CompletedDate = completedDate,
            MileageAtCompletion = 55000,
            Cost = 89.99m,
            ServiceProvider = "Jiffy Lube",
            Notes = "Used Mobil 1 5W-30",
            MaintenanceScheduleId = scheduleId
        };

        var entity = VehicleMapper.FromCreateMaintenanceRecordRequest(request);

        entity.Description.Should().Be("Oil change - full synthetic");
        entity.CompletedDate.Should().Be(completedDate);
        entity.MileageAtCompletion.Should().Be(55000);
        entity.Cost.Should().Be(89.99m);
        entity.ServiceProvider.Should().Be("Jiffy Lube");
        entity.Notes.Should().Be("Used Mobil 1 5W-30");
        entity.MaintenanceScheduleId.Should().Be(scheduleId);
    }

    [Fact]
    public void CreateMaintenanceRecordRequest_To_VehicleMaintenanceRecord_IgnoresSystemFields()
    {
        var request = new CreateMaintenanceRecordRequest
        {
            Description = "Tire rotation",
            CompletedDate = DateTime.UtcNow
        };

        var entity = VehicleMapper.FromCreateMaintenanceRecordRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.VehicleId.Should().Be(Guid.Empty);
        entity.CreatedAt.Should().NotBe(default);
        entity.UpdatedAt.Should().BeNull();
        entity.Vehicle.Should().BeNull();
        entity.MaintenanceSchedule.Should().BeNull();
    }

    [Fact]
    public void CreateMaintenanceRecordRequest_To_VehicleMaintenanceRecord_NullOptionalFields()
    {
        var request = new CreateMaintenanceRecordRequest
        {
            Description = "Brake inspection",
            CompletedDate = DateTime.UtcNow,
            MileageAtCompletion = null,
            Cost = null,
            ServiceProvider = null,
            Notes = null,
            MaintenanceScheduleId = null
        };

        var entity = VehicleMapper.FromCreateMaintenanceRecordRequest(request);

        entity.MileageAtCompletion.Should().BeNull();
        entity.Cost.Should().BeNull();
        entity.ServiceProvider.Should().BeNull();
        entity.Notes.Should().BeNull();
        entity.MaintenanceScheduleId.Should().BeNull();
    }

    #endregion

    #region CreateMaintenanceScheduleRequest -> VehicleMaintenanceSchedule

    [Fact]
    public void CreateMaintenanceScheduleRequest_To_VehicleMaintenanceSchedule_MapsAllProperties()
    {
        var lastCompleted = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var nextDue = new DateTime(2025, 4, 15, 0, 0, 0, DateTimeKind.Utc);
        var request = new CreateMaintenanceScheduleRequest
        {
            Name = "Oil Change",
            Description = "Full synthetic oil change",
            IntervalMonths = 3,
            IntervalMiles = 5000,
            LastCompletedDate = lastCompleted,
            LastCompletedMileage = 50000,
            NextDueDate = nextDue,
            NextDueMileage = 55000,
            Notes = "Use Mobil 1"
        };

        var entity = VehicleMapper.FromCreateMaintenanceScheduleRequest(request);

        entity.Name.Should().Be("Oil Change");
        entity.Description.Should().Be("Full synthetic oil change");
        entity.IntervalMonths.Should().Be(3);
        entity.IntervalMiles.Should().Be(5000);
        entity.LastCompletedDate.Should().Be(lastCompleted);
        entity.LastCompletedMileage.Should().Be(50000);
        entity.NextDueDate.Should().Be(nextDue);
        entity.NextDueMileage.Should().Be(55000);
        entity.Notes.Should().Be("Use Mobil 1");
    }

    [Fact]
    public void CreateMaintenanceScheduleRequest_To_VehicleMaintenanceSchedule_IgnoresSystemFields()
    {
        var request = new CreateMaintenanceScheduleRequest
        {
            Name = "Tire Rotation",
            IntervalMonths = 6
        };

        var entity = VehicleMapper.FromCreateMaintenanceScheduleRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.VehicleId.Should().Be(Guid.Empty);
        entity.CreatedAt.Should().NotBe(default);
        entity.UpdatedAt.Should().BeNull();
        entity.IsActive.Should().BeTrue(); // default from entity
        entity.Vehicle.Should().BeNull();
        entity.MaintenanceRecords.Should().BeEmpty();
    }

    [Fact]
    public void CreateMaintenanceScheduleRequest_To_VehicleMaintenanceSchedule_NullOptionalFields()
    {
        var request = new CreateMaintenanceScheduleRequest
        {
            Name = "Brake Inspection",
            Description = null,
            IntervalMonths = null,
            IntervalMiles = null,
            LastCompletedDate = null,
            LastCompletedMileage = null,
            NextDueDate = null,
            NextDueMileage = null,
            Notes = null
        };

        var entity = VehicleMapper.FromCreateMaintenanceScheduleRequest(request);

        entity.Description.Should().BeNull();
        entity.IntervalMonths.Should().BeNull();
        entity.IntervalMiles.Should().BeNull();
        entity.LastCompletedDate.Should().BeNull();
        entity.LastCompletedMileage.Should().BeNull();
        entity.NextDueDate.Should().BeNull();
        entity.NextDueMileage.Should().BeNull();
        entity.Notes.Should().BeNull();
    }

    #endregion

    #region UpdateMaintenanceScheduleRequest -> VehicleMaintenanceSchedule

    [Fact]
    public void UpdateMaintenanceScheduleRequest_To_VehicleMaintenanceSchedule_MapsAllProperties()
    {
        var nextDue = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var request = new UpdateMaintenanceScheduleRequest
        {
            Name = "Updated Oil Change",
            Description = "Synthetic blend OK",
            IntervalMonths = 4,
            IntervalMiles = 6000,
            NextDueDate = nextDue,
            NextDueMileage = 61000,
            IsActive = true,
            Notes = "Updated notes"
        };

        var entity = VehicleMapper.FromUpdateMaintenanceScheduleRequest(request);

        entity.Name.Should().Be("Updated Oil Change");
        entity.Description.Should().Be("Synthetic blend OK");
        entity.IntervalMonths.Should().Be(4);
        entity.IntervalMiles.Should().Be(6000);
        entity.NextDueDate.Should().Be(nextDue);
        entity.NextDueMileage.Should().Be(61000);
        entity.IsActive.Should().BeTrue();
        entity.Notes.Should().Be("Updated notes");
    }

    [Fact]
    public void UpdateMaintenanceScheduleRequest_To_VehicleMaintenanceSchedule_IgnoresSystemFields()
    {
        var existingId = Guid.NewGuid();
        var existingTenantId = Guid.NewGuid();
        var existingVehicleId = Guid.NewGuid();
        var lastCompletedDate = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var existing = new VehicleMaintenanceSchedule
        {
            Id = existingId,
            TenantId = existingTenantId,
            VehicleId = existingVehicleId,
            Name = "Old Name",
            CreatedAt = DateTime.UtcNow.AddDays(-60),
            LastCompletedDate = lastCompletedDate,
            LastCompletedMileage = 48000,
            MaintenanceRecords = new List<VehicleMaintenanceRecord> { new() }
        };

        var request = new UpdateMaintenanceScheduleRequest
        {
            Name = "New Name",
            IntervalMonths = 6,
            IsActive = false
        };

        VehicleMapper.ApplyUpdateMaintenanceScheduleRequest(request, existing);

        existing.Id.Should().Be(existingId);
        existing.TenantId.Should().Be(existingTenantId);
        existing.VehicleId.Should().Be(existingVehicleId);
        existing.LastCompletedDate.Should().Be(lastCompletedDate);
        existing.LastCompletedMileage.Should().Be(48000);
        existing.Name.Should().Be("New Name");
        existing.IntervalMonths.Should().Be(6);
        existing.IsActive.Should().BeFalse();
        existing.MaintenanceRecords.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateMaintenanceScheduleRequest_To_VehicleMaintenanceSchedule_DeactivateSchedule()
    {
        var existing = new VehicleMaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            Name = "Tire Rotation",
            IsActive = true
        };

        var request = new UpdateMaintenanceScheduleRequest
        {
            Name = "Tire Rotation",
            IsActive = false
        };

        VehicleMapper.ApplyUpdateMaintenanceScheduleRequest(request, existing);

        existing.IsActive.Should().BeFalse();
    }

    #endregion

    #region VehicleDocument -> VehicleDocumentDto

    [Fact]
    public void VehicleDocument_To_VehicleDocumentDto_MapsAllProperties()
    {
        var vehicleId = Guid.NewGuid();
        var expirationDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var createdAt = DateTime.UtcNow;

        var entity = new VehicleDocument
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            FileName = "abc123-registration.pdf",
            OriginalFileName = "my-registration.pdf",
            ContentType = "application/pdf",
            FileSize = 204800,
            DisplayName = "Vehicle Registration",
            DocumentType = "Registration",
            ExpirationDate = expirationDate,
            SortOrder = 1,
            CreatedAt = createdAt
        };

        var dto = VehicleMapper.ToDocumentDto(entity);

        dto.Id.Should().Be(entity.Id);
        dto.VehicleId.Should().Be(vehicleId);
        dto.FileName.Should().Be("abc123-registration.pdf");
        dto.OriginalFileName.Should().Be("my-registration.pdf");
        dto.ContentType.Should().Be("application/pdf");
        dto.FileSize.Should().Be(204800);
        dto.DisplayName.Should().Be("Vehicle Registration");
        dto.DocumentType.Should().Be("Registration");
        dto.ExpirationDate.Should().Be(expirationDate);
        dto.SortOrder.Should().Be(1);
        dto.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void VehicleDocument_To_VehicleDocumentDto_NullOptionalFields()
    {
        var entity = new VehicleDocument
        {
            Id = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            FileName = "file.jpg",
            OriginalFileName = "photo.jpg",
            ContentType = "image/jpeg",
            FileSize = 1024,
            DisplayName = null,
            DocumentType = null,
            ExpirationDate = null,
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow
        };

        var dto = VehicleMapper.ToDocumentDto(entity);

        dto.DisplayName.Should().BeNull();
        dto.DocumentType.Should().BeNull();
        dto.ExpirationDate.Should().BeNull();
    }

    [Fact]
    public void VehicleDocument_To_VehicleDocumentDto_CollectionMapping()
    {
        var vehicleId = Guid.NewGuid();
        var documents = new List<VehicleDocument>
        {
            new()
            {
                Id = Guid.NewGuid(),
                VehicleId = vehicleId,
                FileName = "reg.pdf",
                OriginalFileName = "registration.pdf",
                ContentType = "application/pdf",
                FileSize = 1024,
                DocumentType = "Registration",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                VehicleId = vehicleId,
                FileName = "ins.pdf",
                OriginalFileName = "insurance.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                DocumentType = "Insurance",
                CreatedAt = DateTime.UtcNow
            }
        };

        var dtos = documents.Select(VehicleMapper.ToDocumentDto).ToList();

        dtos.Should().HaveCount(2);
        dtos[0].DocumentType.Should().Be("Registration");
        dtos[1].DocumentType.Should().Be("Insurance");
    }

    #endregion
}
