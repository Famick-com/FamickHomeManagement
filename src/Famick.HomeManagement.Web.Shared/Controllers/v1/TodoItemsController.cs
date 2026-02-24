using Famick.HomeManagement.Core.DTOs.TodoItems;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Web.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing TODO items and follow-up tasks
/// </summary>
[ApiController]
[Route("api/v1/todoitems")]
[Authorize]
public class TodoItemsController : ApiControllerBase
{
    private readonly ITodoItemService _todoItemService;

    public TodoItemsController(
        ITodoItemService todoItemService,
        ITenantProvider tenantProvider,
        ILogger<TodoItemsController> logger)
        : base(tenantProvider, logger)
    {
        _todoItemService = todoItemService;
    }

    /// <summary>
    /// Lists all TODO items
    /// </summary>
    /// <param name="includeCompleted">Include completed items (default: false)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of TODO items</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<TodoItemDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> List(
        [FromQuery] bool includeCompleted = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing TODO items for tenant {TenantId}", TenantId);

        var items = await _todoItemService.GetAllAsync(includeCompleted, cancellationToken);
        return ApiResponse(items);
    }

    /// <summary>
    /// Gets a specific TODO item by ID
    /// </summary>
    /// <param name="id">TODO item ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TODO item details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TodoItemDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting TODO item {TodoItemId} for tenant {TenantId}", id, TenantId);

        var item = await _todoItemService.GetByIdAsync(id, cancellationToken);

        if (item == null)
        {
            return NotFoundResponse($"TODO item with ID {id} not found");
        }

        return ApiResponse(item);
    }

    /// <summary>
    /// Gets TODO items by task type
    /// </summary>
    /// <param name="taskType">Task type (Inventory, Product, Equipment, Other)</param>
    /// <param name="includeCompleted">Include completed items (default: false)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of TODO items matching the task type</returns>
    [HttpGet("by-type/{taskType}")]
    [ProducesResponseType(typeof(List<TodoItemDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetByType(
        TaskType taskType,
        [FromQuery] bool includeCompleted = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing TODO items of type {TaskType} for tenant {TenantId}", taskType, TenantId);

        var items = await _todoItemService.GetByTypeAsync(taskType, includeCompleted, cancellationToken);
        return ApiResponse(items);
    }

    /// <summary>
    /// Creates a new TODO item
    /// </summary>
    /// <param name="request">TODO item creation data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created TODO item</returns>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(TodoItemDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTodoItemRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "Reason", new[] { "Reason is required" } }
            });
        }

        _logger.LogInformation("Creating TODO item of type {TaskType} for tenant {TenantId}", request.TaskType, TenantId);

        var item = await _todoItemService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = item.Id },
            item
        );
    }

    /// <summary>
    /// Updates an existing TODO item
    /// </summary>
    /// <param name="id">TODO item ID</param>
    /// <param name="request">TODO item update data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated TODO item</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(TodoItemDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTodoItemRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating TODO item {TodoItemId} for tenant {TenantId}", id, TenantId);

        var item = await _todoItemService.UpdateAsync(id, request, cancellationToken);
        return ApiResponse(item);
    }

    /// <summary>
    /// Marks a TODO item as completed
    /// </summary>
    /// <param name="id">TODO item ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated TODO item</returns>
    [HttpPut("{id}/complete")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(TodoItemDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> MarkCompleted(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Marking TODO item {TodoItemId} as completed for tenant {TenantId}", id, TenantId);

        // Get the username from the current user claims
        var username = User.Identity?.Name;

        var item = await _todoItemService.MarkCompletedAsync(id, username, cancellationToken);
        return ApiResponse(item);
    }

    /// <summary>
    /// Deletes a TODO item
    /// </summary>
    /// <param name="id">TODO item ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting TODO item {TodoItemId} for tenant {TenantId}", id, TenantId);

        await _todoItemService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
