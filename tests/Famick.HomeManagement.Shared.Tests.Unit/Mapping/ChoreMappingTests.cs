using Famick.HomeManagement.Core.DTOs.Chores;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class ChoreMappingTests
{
    #region Chore -> ChoreDto

    [Fact]
    public void Chore_To_ChoreDto_MapsAllProperties()
    {
        var choreId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var chore = new Chore
        {
            Id = choreId,
            TenantId = Guid.NewGuid(),
            Name = "Water plants",
            Description = "Water all indoor plants",
            PeriodType = "weekly",
            PeriodDays = 7,
            TrackDateOnly = true,
            Rollover = true,
            AssignmentType = "round-robin",
            AssignmentConfig = "user1,user2",
            NextExecutionAssignedToUserId = userId,
            StartDate = now.AddDays(-30),
            ConsumeProductOnExecution = true,
            ProductId = productId,
            ProductAmount = 2.5m,
            CreatedAt = now.AddDays(-60),
            UpdatedAt = now,
            NextExecutionAssignedToUser = new User { FirstName = "Alice", LastName = "Wonder" },
            Product = new Product { Name = "Plant Food" }
        };

        var dto = ChoreMapper.ToDto(chore);

        dto.Id.Should().Be(choreId);
        dto.Name.Should().Be("Water plants");
        dto.Description.Should().Be("Water all indoor plants");
        dto.PeriodType.Should().Be("weekly");
        dto.PeriodDays.Should().Be(7);
        dto.TrackDateOnly.Should().BeTrue();
        dto.Rollover.Should().BeTrue();
        dto.AssignmentType.Should().Be("round-robin");
        dto.AssignmentConfig.Should().Be("user1,user2");
        dto.NextExecutionAssignedToUserId.Should().Be(userId);
        dto.NextExecutionAssignedToUserName.Should().Be("Alice Wonder");
        dto.StartDate.Should().Be(now.AddDays(-30));
        dto.ConsumeProductOnExecution.Should().BeTrue();
        dto.ProductId.Should().Be(productId);
        dto.ProductName.Should().Be("Plant Food");
        dto.ProductAmount.Should().Be(2.5m);
        dto.CreatedAt.Should().Be(now.AddDays(-60));
        dto.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void Chore_To_ChoreDto_NextExecutionDate_IsIgnored()
    {
        var chore = new Chore
        {
            Id = Guid.NewGuid(),
            Name = "Test"
        };

        var dto = ChoreMapper.ToDto(chore);

        dto.NextExecutionDate.Should().BeNull();
    }

    [Fact]
    public void Chore_To_ChoreDto_NullNavigationProperties_DoNotThrow()
    {
        var chore = new Chore
        {
            Id = Guid.NewGuid(),
            Name = "Clean kitchen",
            NextExecutionAssignedToUser = null,
            Product = null
        };

        var dto = ChoreMapper.ToDto(chore);

        dto.NextExecutionAssignedToUserName.Should().BeNull();
        dto.ProductName.Should().BeNull();
    }

    [Fact]
    public void Chore_To_ChoreDto_UserWithOnlyFirstName_TrimsResult()
    {
        var chore = new Chore
        {
            Id = Guid.NewGuid(),
            Name = "Chore",
            NextExecutionAssignedToUser = new User { FirstName = "Bob", LastName = "" }
        };

        var dto = ChoreMapper.ToDto(chore);

        dto.NextExecutionAssignedToUserName.Should().Be("Bob");
    }

    #endregion

    #region Chore -> ChoreSummaryDto

    [Fact]
    public void Chore_To_ChoreSummaryDto_MapsAllProperties()
    {
        var choreId = Guid.NewGuid();
        var chore = new Chore
        {
            Id = choreId,
            Name = "Vacuum",
            PeriodType = "daily",
            NextExecutionAssignedToUser = new User { FirstName = "Charlie", LastName = "Brown" }
        };

        var dto = ChoreMapper.ToSummaryDto(chore);

        dto.Id.Should().Be(choreId);
        dto.Name.Should().Be("Vacuum");
        dto.PeriodType.Should().Be("daily");
        dto.AssignedToUserName.Should().Be("Charlie Brown");
    }

    [Fact]
    public void Chore_To_ChoreSummaryDto_IgnoredFieldsAreDefault()
    {
        var chore = new Chore
        {
            Id = Guid.NewGuid(),
            Name = "Test"
        };

        var dto = ChoreMapper.ToSummaryDto(chore);

        dto.NextExecutionDate.Should().BeNull();
        dto.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void Chore_To_ChoreSummaryDto_NullUser_ReturnsNullName()
    {
        var chore = new Chore
        {
            Id = Guid.NewGuid(),
            Name = "Sweep",
            NextExecutionAssignedToUser = null
        };

        var dto = ChoreMapper.ToSummaryDto(chore);

        dto.AssignedToUserName.Should().BeNull();
    }

    #endregion

    #region CreateChoreRequest -> Chore

    [Fact]
    public void CreateChoreRequest_To_Chore_MapsEditableFields()
    {
        var productId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(1);

        var request = new CreateChoreRequest
        {
            Name = "Mow lawn",
            Description = "Front and back yard",
            PeriodType = "weekly",
            PeriodDays = 7,
            TrackDateOnly = true,
            Rollover = true,
            AssignmentType = "specific-user",
            AssignmentConfig = "user-id-1",
            StartDate = startDate,
            ConsumeProductOnExecution = true,
            ProductId = productId,
            ProductAmount = 1.0m
        };

        var entity = ChoreMapper.FromCreateRequest(request);

        entity.Name.Should().Be("Mow lawn");
        entity.Description.Should().Be("Front and back yard");
        entity.PeriodType.Should().Be("weekly");
        entity.PeriodDays.Should().Be(7);
        entity.TrackDateOnly.Should().BeTrue();
        entity.Rollover.Should().BeTrue();
        entity.AssignmentType.Should().Be("specific-user");
        entity.AssignmentConfig.Should().Be("user-id-1");
        entity.StartDate.Should().Be(startDate);
        entity.ConsumeProductOnExecution.Should().BeTrue();
        entity.ProductId.Should().Be(productId);
        entity.ProductAmount.Should().Be(1.0m);
    }

    [Fact]
    public void CreateChoreRequest_To_Chore_IgnoresSystemFields()
    {
        var request = new CreateChoreRequest
        {
            Name = "Test"
        };

        var entity = ChoreMapper.FromCreateRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.NextExecutionAssignedToUserId.Should().BeNull();
        entity.EquipmentId.Should().BeNull();
        entity.Product.Should().BeNull();
        entity.NextExecutionAssignedToUser.Should().BeNull();
        entity.Equipment.Should().BeNull();
        entity.LogEntries.Should().BeNull();
    }

    #endregion

    #region UpdateChoreRequest -> Chore

    [Fact]
    public void UpdateChoreRequest_To_Chore_MapsEditableFields()
    {
        var request = new UpdateChoreRequest
        {
            Name = "Updated chore",
            Description = "Updated desc",
            PeriodType = "monthly",
            PeriodDays = 30,
            TrackDateOnly = false,
            Rollover = false,
            AssignmentType = "round-robin",
            AssignmentConfig = "a,b,c",
            StartDate = DateTime.UtcNow,
            ConsumeProductOnExecution = false,
            ProductId = null,
            ProductAmount = null
        };

        var entity = new Chore();
        ChoreMapper.Update(request, entity);

        entity.Name.Should().Be("Updated chore");
        entity.Description.Should().Be("Updated desc");
        entity.PeriodType.Should().Be("monthly");
        entity.PeriodDays.Should().Be(30);
        entity.TrackDateOnly.Should().BeFalse();
        entity.Rollover.Should().BeFalse();
    }

    [Fact]
    public void UpdateChoreRequest_To_Chore_IgnoresSystemFields()
    {
        var request = new UpdateChoreRequest
        {
            Name = "Test"
        };

        var entity = new Chore();
        ChoreMapper.Update(request, entity);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.NextExecutionAssignedToUserId.Should().BeNull();
        entity.EquipmentId.Should().BeNull();
        entity.Product.Should().BeNull();
        entity.NextExecutionAssignedToUser.Should().BeNull();
        entity.Equipment.Should().BeNull();
        entity.LogEntries.Should().BeNull();
    }

    #endregion

    #region ChoreLog -> ChoreLogDto

    [Fact]
    public void ChoreLog_To_ChoreLogDto_MapsAllProperties()
    {
        var logId = Guid.NewGuid();
        var choreId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var log = new ChoreLog
        {
            Id = logId,
            TenantId = Guid.NewGuid(),
            ChoreId = choreId,
            TrackedTime = now,
            DoneByUserId = userId,
            Undone = false,
            UndoneTimestamp = null,
            Skipped = false,
            ScheduledExecutionTime = now.AddHours(-1),
            CreatedAt = now,
            Chore = new Chore { Name = "Dishes" },
            DoneByUser = new User { FirstName = "Dan", LastName = "Smith" }
        };

        var dto = ChoreMapper.ToLogDto(log);

        dto.Id.Should().Be(logId);
        dto.ChoreId.Should().Be(choreId);
        dto.ChoreName.Should().Be("Dishes");
        dto.TrackedTime.Should().Be(now);
        dto.DoneByUserId.Should().Be(userId);
        dto.DoneByUserName.Should().Be("Dan Smith");
        dto.Undone.Should().BeFalse();
        dto.UndoneTimestamp.Should().BeNull();
        dto.Skipped.Should().BeFalse();
        dto.ScheduledExecutionTime.Should().Be(now.AddHours(-1));
        dto.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void ChoreLog_To_ChoreLogDto_NullNavigationProperties_DoNotThrow()
    {
        var log = new ChoreLog
        {
            Id = Guid.NewGuid(),
            ChoreId = Guid.NewGuid(),
            Chore = null,
            DoneByUser = null
        };

        var dto = ChoreMapper.ToLogDto(log);

        dto.ChoreName.Should().Be(string.Empty);
        dto.DoneByUserName.Should().BeNull();
    }

    [Fact]
    public void ChoreLog_To_ChoreLogDto_UndoneEntry()
    {
        var now = DateTime.UtcNow;
        var log = new ChoreLog
        {
            Id = Guid.NewGuid(),
            ChoreId = Guid.NewGuid(),
            Undone = true,
            UndoneTimestamp = now,
            Chore = new Chore { Name = "Sweep" },
            DoneByUser = new User { FirstName = "Eve", LastName = "Adams" }
        };

        var dto = ChoreMapper.ToLogDto(log);

        dto.Undone.Should().BeTrue();
        dto.UndoneTimestamp.Should().Be(now);
    }

    #endregion
}
