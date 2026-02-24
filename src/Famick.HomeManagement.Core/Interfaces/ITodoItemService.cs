using Famick.HomeManagement.Core.DTOs.TodoItems;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing TODO items and follow-up tasks
/// </summary>
public interface ITodoItemService
{
    /// <summary>
    /// Creates a new TODO item
    /// </summary>
    Task<TodoItemDto> CreateAsync(
        CreateTodoItemRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a TODO item by ID
    /// </summary>
    Task<TodoItemDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all TODO items, optionally including completed ones
    /// </summary>
    Task<List<TodoItemDto>> GetAllAsync(
        bool includeCompleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets TODO items filtered by task type
    /// </summary>
    Task<List<TodoItemDto>> GetByTypeAsync(
        TaskType taskType,
        bool includeCompleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a TODO item
    /// </summary>
    Task<TodoItemDto> UpdateAsync(
        Guid id,
        UpdateTodoItemRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a TODO item as completed
    /// </summary>
    Task<TodoItemDto> MarkCompletedAsync(
        Guid id,
        string? completedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a TODO item
    /// </summary>
    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
