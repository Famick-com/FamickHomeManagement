using Famick.HomeManagement.Core.DTOs.TodoItems;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class TodoItemService : ITodoItemService
{
    private readonly HomeManagementDbContext _context;
    private readonly ILogger<TodoItemService> _logger;

    public TodoItemService(
        HomeManagementDbContext context,
        ILogger<TodoItemService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TodoItemDto> CreateAsync(
        CreateTodoItemRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating TODO item: {TaskType} - {Reason}", request.TaskType, request.Reason);

        var todoItem = TodoItemMapper.FromCreateRequest(request);
        todoItem.Id = Guid.NewGuid();

        _context.TodoItems.Add(todoItem);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created TODO item: {Id}", todoItem.Id);

        return TodoItemMapper.ToDto(todoItem);
    }

    public async Task<TodoItemDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var todoItem = await _context.TodoItems
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return todoItem is not null ? TodoItemMapper.ToDto(todoItem) : null;
    }

    public async Task<List<TodoItemDto>> GetAllAsync(
        bool includeCompleted = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TodoItems.AsQueryable();

        if (!includeCompleted)
        {
            query = query.Where(t => !t.IsCompleted);
        }

        var todoItems = await query
            .OrderByDescending(t => t.DateEntered)
            .ToListAsync(cancellationToken);

        return todoItems.Select(TodoItemMapper.ToDto).ToList();
    }

    public async Task<List<TodoItemDto>> GetByTypeAsync(
        TaskType taskType,
        bool includeCompleted = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TodoItems
            .Where(t => t.TaskType == taskType);

        if (!includeCompleted)
        {
            query = query.Where(t => !t.IsCompleted);
        }

        var todoItems = await query
            .OrderByDescending(t => t.DateEntered)
            .ToListAsync(cancellationToken);

        return todoItems.Select(TodoItemMapper.ToDto).ToList();
    }

    public async Task<TodoItemDto> UpdateAsync(
        Guid id,
        UpdateTodoItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var todoItem = await _context.TodoItems
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (todoItem == null)
        {
            throw new EntityNotFoundException(nameof(TodoItem), id);
        }

        TodoItemMapper.UpdateTodoItem(request, todoItem);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated TODO item: {Id}", id);

        return TodoItemMapper.ToDto(todoItem);
    }

    public async Task<TodoItemDto> MarkCompletedAsync(
        Guid id,
        string? completedBy = null,
        CancellationToken cancellationToken = default)
    {
        var todoItem = await _context.TodoItems
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (todoItem == null)
        {
            throw new EntityNotFoundException(nameof(TodoItem), id);
        }

        todoItem.IsCompleted = true;
        todoItem.CompletedAt = DateTime.UtcNow;
        todoItem.CompletedBy = completedBy;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Marked TODO item as completed: {Id}", id);

        return TodoItemMapper.ToDto(todoItem);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var todoItem = await _context.TodoItems
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (todoItem == null)
        {
            throw new EntityNotFoundException(nameof(TodoItem), id);
        }

        _context.TodoItems.Remove(todoItem);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted TODO item: {Id}", id);
    }
}
