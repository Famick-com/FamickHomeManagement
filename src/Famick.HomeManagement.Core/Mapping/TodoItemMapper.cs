#pragma warning disable RMG020 // Unmapped source member
using Famick.HomeManagement.Core.DTOs.TodoItems;
using Famick.HomeManagement.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace Famick.HomeManagement.Core.Mapping;

[Mapper]
public static partial class TodoItemMapper
{
    public static partial TodoItemDto ToDto(TodoItem source);

    public static TodoItem FromCreateRequest(CreateTodoItemRequest source)
    {
        var entity = MapFromCreateRequest(source);
        entity.DateEntered = DateTime.UtcNow;
        entity.IsCompleted = false;
        return entity;
    }

    [MapperIgnoreTarget(nameof(TodoItem.Id))]
    [MapperIgnoreTarget(nameof(TodoItem.TenantId))]
    [MapperIgnoreTarget(nameof(TodoItem.DateEntered))]
    [MapperIgnoreTarget(nameof(TodoItem.IsCompleted))]
    [MapperIgnoreTarget(nameof(TodoItem.CompletedAt))]
    [MapperIgnoreTarget(nameof(TodoItem.CompletedBy))]
    [MapperIgnoreTarget(nameof(TodoItem.CreatedAt))]
    [MapperIgnoreTarget(nameof(TodoItem.UpdatedAt))]
    private static partial TodoItem MapFromCreateRequest(CreateTodoItemRequest source);

    /// <summary>
    /// Updates only non-null properties from the request (mirrors ForAllMembers null condition).
    /// </summary>
    public static void UpdateTodoItem(UpdateTodoItemRequest source, TodoItem target)
    {
        if (source.TaskType != null)
            target.TaskType = source.TaskType.Value;
        if (source.Reason != null)
            target.Reason = source.Reason;
        if (source.RelatedEntityId != null)
            target.RelatedEntityId = source.RelatedEntityId;
        if (source.RelatedEntityType != null)
            target.RelatedEntityType = source.RelatedEntityType;
        if (source.Description != null)
            target.Description = source.Description;
        if (source.AdditionalData != null)
            target.AdditionalData = source.AdditionalData;
    }
}
