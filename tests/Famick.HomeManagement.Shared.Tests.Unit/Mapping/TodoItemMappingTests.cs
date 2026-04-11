using AutoMapper;
using Famick.HomeManagement.Core.DTOs.TodoItems;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class TodoItemMappingTests
{
    private readonly IMapper _mapper;

    public TodoItemMappingTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<TodoItemMappingProfile>();
        }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        // Validation skipped: profiles are tested in isolation
        _mapper = config.CreateMapper();
    }

    [Fact]
    public void TodoItem_To_TodoItemDto_MapsAllProperties()
    {
        var item = new TodoItem
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            TaskType = TaskType.Product,
            DateEntered = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
            Reason = "New product added",
            RelatedEntityId = Guid.NewGuid(),
            RelatedEntityType = "Product",
            Description = "Complete product details",
            AdditionalData = "{\"key\":\"value\"}",
            IsCompleted = false,
            CompletedAt = null,
            CompletedBy = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var dto = _mapper.Map<TodoItemDto>(item);

        dto.Id.Should().Be(item.Id);
        dto.TaskType.Should().Be(TaskType.Product);
        dto.DateEntered.Should().Be(item.DateEntered);
        dto.Reason.Should().Be("New product added");
        dto.RelatedEntityId.Should().Be(item.RelatedEntityId);
        dto.RelatedEntityType.Should().Be("Product");
        dto.Description.Should().Be("Complete product details");
        dto.AdditionalData.Should().Be("{\"key\":\"value\"}");
        dto.IsCompleted.Should().BeFalse();
        dto.CompletedAt.Should().BeNull();
        dto.CompletedBy.Should().BeNull();
        dto.CreatedAt.Should().Be(item.CreatedAt);
        dto.UpdatedAt.Should().Be(item.UpdatedAt);
    }

    [Fact]
    public void CreateTodoItemRequest_To_TodoItem_SetsDefaults()
    {
        var request = new CreateTodoItemRequest
        {
            TaskType = TaskType.Product,
            Reason = "Test reason",
            Description = "Test desc"
        };

        var before = DateTime.UtcNow;
        var entity = _mapper.Map<TodoItem>(request);
        var after = DateTime.UtcNow;

        entity.TaskType.Should().Be(TaskType.Product);
        entity.Reason.Should().Be("Test reason");
        entity.Description.Should().Be("Test desc");
        entity.DateEntered.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        entity.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void CreateTodoItemRequest_To_TodoItem_IgnoresSystemFields()
    {
        var request = new CreateTodoItemRequest
        {
            TaskType = TaskType.Product,
            Reason = "Test"
        };

        var entity = _mapper.Map<TodoItem>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.CompletedAt.Should().BeNull();
        entity.CompletedBy.Should().BeNull();
    }

    [Fact]
    public void UpdateTodoItemRequest_To_TodoItem_OnlyMapsNonNullFields()
    {
        var existing = new TodoItem
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            TaskType = TaskType.Product,
            Reason = "Original reason",
            Description = "Original description",
            DateEntered = DateTime.UtcNow.AddDays(-1),
            IsCompleted = false
        };

        var request = new UpdateTodoItemRequest
        {
            Description = "Updated description",
            Reason = null // null should NOT overwrite existing
        };

        _mapper.Map(request, existing);

        existing.Description.Should().Be("Updated description");
        existing.Reason.Should().Be("Original reason"); // preserved because source was null
        existing.Id.Should().NotBe(Guid.Empty); // system fields preserved
        existing.TenantId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void UpdateTodoItemRequest_To_TodoItem_IgnoresSystemFields()
    {
        var existingId = Guid.NewGuid();
        var existingTenantId = Guid.NewGuid();
        var existing = new TodoItem
        {
            Id = existingId,
            TenantId = existingTenantId,
            DateEntered = DateTime.UtcNow.AddDays(-5),
            IsCompleted = true,
            CompletedAt = DateTime.UtcNow.AddDays(-1)
        };

        var request = new UpdateTodoItemRequest
        {
            Description = "New description"
        };

        _mapper.Map(request, existing);

        existing.Id.Should().Be(existingId);
        existing.TenantId.Should().Be(existingTenantId);
        existing.DateEntered.Should().NotBe(default);
        existing.IsCompleted.Should().BeTrue();
    }
}
