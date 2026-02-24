using Famick.HomeManagement.Core.DTOs.Chores;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing chores, scheduling, and execution tracking
/// </summary>
[ApiController]
[Route("api/v1/chores")]
[Authorize]
public class ChoresController : ApiControllerBase
{
    private readonly IChoreService _choreService;
    private readonly IValidator<CreateChoreRequest> _createValidator;
    private readonly IValidator<UpdateChoreRequest> _updateValidator;
    private readonly IValidator<ExecuteChoreRequest> _executeValidator;
    private readonly IValidator<SkipChoreRequest> _skipValidator;

    public ChoresController(
        IChoreService choreService,
        IValidator<CreateChoreRequest> createValidator,
        IValidator<UpdateChoreRequest> updateValidator,
        IValidator<ExecuteChoreRequest> executeValidator,
        IValidator<SkipChoreRequest> skipValidator,
        ITenantProvider tenantProvider,
        ILogger<ChoresController> logger)
        : base(tenantProvider, logger)
    {
        _choreService = choreService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _executeValidator = executeValidator;
        _skipValidator = skipValidator;
    }

    #region Chore CRUD

    /// <summary>
    /// Lists all chores with optional filtering
    /// </summary>
    /// <param name="filter">Optional filter criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of chores</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<ChoreSummaryDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> List(
        [FromQuery] ChoreFilterRequest? filter,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing chores for tenant {TenantId}", TenantId);

        var chores = await _choreService.ListAsync(filter, cancellationToken);
        return ApiResponse(chores);
    }

    /// <summary>
    /// Gets a specific chore by ID
    /// </summary>
    /// <param name="id">Chore ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Chore details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ChoreDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting chore {ChoreId} for tenant {TenantId}", id, TenantId);

        var chore = await _choreService.GetByIdAsync(id, cancellationToken);

        if (chore == null)
        {
            return NotFoundResponse($"Chore with ID {id} not found");
        }

        return ApiResponse(chore);
    }

    /// <summary>
    /// Creates a new chore
    /// </summary>
    /// <param name="request">Chore creation data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created chore</returns>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ChoreDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Create(
        [FromBody] CreateChoreRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Creating chore '{Name}' for tenant {TenantId}", request.Name, TenantId);

        var chore = await _choreService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = chore.Id },
            chore
        );
    }

    /// <summary>
    /// Updates an existing chore
    /// </summary>
    /// <param name="id">Chore ID</param>
    /// <param name="request">Chore update data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated chore</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ChoreDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateChoreRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Updating chore {ChoreId} for tenant {TenantId}", id, TenantId);

        var chore = await _choreService.UpdateAsync(id, request, cancellationToken);
        return ApiResponse(chore);
    }

    /// <summary>
    /// Deletes a chore (soft delete)
    /// </summary>
    /// <param name="id">Chore ID</param>
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
        _logger.LogInformation("Deleting chore {ChoreId} for tenant {TenantId}", id, TenantId);

        await _choreService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Execution & Logs

    /// <summary>
    /// Marks a chore as completed (creates execution log)
    /// </summary>
    /// <param name="id">Chore ID</param>
    /// <param name="request">Execution details (notes, completion date)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created chore log entry</returns>
    [HttpPost("{id}/execute")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ChoreLogDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Execute(
        Guid id,
        [FromBody] ExecuteChoreRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _executeValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Executing chore {ChoreId} for tenant {TenantId}", id, TenantId);

        var log = await _choreService.ExecuteChoreAsync(id, request, cancellationToken);

        return CreatedAtAction(
            nameof(GetExecutionHistory),
            new { id },
            log
        );
    }

    /// <summary>
    /// Skips a scheduled chore occurrence
    /// </summary>
    /// <param name="id">Chore ID</param>
    /// <param name="request">Skip reason</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("{id}/skip")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Skip(
        Guid id,
        [FromBody] SkipChoreRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _skipValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Skipping chore {ChoreId} for tenant {TenantId}", id, TenantId);

        await _choreService.SkipChoreAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Undoes a chore execution (removes log entry)
    /// </summary>
    /// <param name="id">Chore ID</param>
    /// <param name="logId">Chore log ID to undo</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}/logs/{logId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UndoExecution(
        Guid id,
        Guid logId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Undoing execution {LogId} for chore {ChoreId} for tenant {TenantId}",
            logId, id, TenantId);

        await _choreService.UndoExecutionAsync(logId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Gets execution history for a chore
    /// </summary>
    /// <param name="id">Chore ID</param>
    /// <param name="limit">Maximum number of entries to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of chore log entries</returns>
    [HttpGet("{id}/logs")]
    [ProducesResponseType(typeof(List<ChoreLogDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetExecutionHistory(
        Guid id,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting execution history for chore {ChoreId} for tenant {TenantId}", id, TenantId);

        var logs = await _choreService.GetExecutionHistoryAsync(id, limit, cancellationToken);
        return ApiResponse(logs);
    }

    #endregion

    #region Scheduling & Queries

    /// <summary>
    /// Calculates the next execution date for a chore based on its schedule
    /// </summary>
    /// <param name="id">Chore ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Next execution date (null if not scheduled)</returns>
    [HttpGet("{id}/next-execution")]
    [ProducesResponseType(typeof(DateTime?), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetNextExecutionDate(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Calculating next execution date for chore {ChoreId} for tenant {TenantId}",
            id, TenantId);

        var nextDate = await _choreService.CalculateNextExecutionDateAsync(id, cancellationToken);
        return ApiResponse(new { nextExecutionDate = nextDate });
    }

    /// <summary>
    /// Gets all overdue chores
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of overdue chores</returns>
    [HttpGet("overdue")]
    [ProducesResponseType(typeof(List<ChoreSummaryDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetOverdue(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting overdue chores for tenant {TenantId}", TenantId);

        var chores = await _choreService.GetOverdueChoresAsync(cancellationToken);
        return ApiResponse(chores);
    }

    /// <summary>
    /// Gets chores due soon (within specified days)
    /// </summary>
    /// <param name="daysAhead">Number of days to look ahead (default: 7)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of chores due soon</returns>
    [HttpGet("due-soon")]
    [ProducesResponseType(typeof(List<ChoreSummaryDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetDueSoon(
        [FromQuery] int daysAhead = 7,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting chores due within {DaysAhead} days for tenant {TenantId}",
            daysAhead, TenantId);

        var chores = await _choreService.GetChoresDueSoonAsync(daysAhead, cancellationToken);
        return ApiResponse(chores);
    }

    /// <summary>
    /// Assigns the next execution of a chore to a user
    /// </summary>
    /// <param name="id">Chore ID</param>
    /// <param name="userId">User ID to assign to (null for round-robin assignment)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("{id}/assign")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> AssignNextExecution(
        Guid id,
        [FromQuery] Guid? userId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Assigning chore {ChoreId} to user {UserId} for tenant {TenantId}",
            id, userId, TenantId);

        await _choreService.AssignNextExecutionAsync(id, userId, cancellationToken);
        return NoContent();
    }

    #endregion
}
