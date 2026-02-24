using Famick.HomeManagement.Core.DTOs.Chores;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing chores, scheduling, and execution tracking
/// </summary>
public interface IChoreService
{
    // Chore management
    Task<ChoreDto> CreateAsync(
        CreateChoreRequest request,
        CancellationToken cancellationToken = default);

    Task<ChoreDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<ChoreSummaryDto>> ListAsync(
        ChoreFilterRequest? filter = null,
        CancellationToken cancellationToken = default);

    Task<ChoreDto> UpdateAsync(
        Guid id,
        UpdateChoreRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    // Execution
    Task<ChoreLogDto> ExecuteChoreAsync(
        Guid choreId,
        ExecuteChoreRequest request,
        CancellationToken cancellationToken = default);

    Task UndoExecutionAsync(
        Guid logId,
        CancellationToken cancellationToken = default);

    Task SkipChoreAsync(
        Guid choreId,
        SkipChoreRequest request,
        CancellationToken cancellationToken = default);

    // Scheduling & querying
    Task<DateTime?> CalculateNextExecutionDateAsync(
        Guid choreId,
        CancellationToken cancellationToken = default);

    Task<List<ChoreSummaryDto>> GetOverdueChoresAsync(
        CancellationToken cancellationToken = default);

    Task<List<ChoreSummaryDto>> GetChoresDueSoonAsync(
        int daysAhead = 7,
        CancellationToken cancellationToken = default);

    // Assignment
    Task AssignNextExecutionAsync(
        Guid choreId,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    // History
    Task<List<ChoreLogDto>> GetExecutionHistoryAsync(
        Guid choreId,
        int? limit = null,
        CancellationToken cancellationToken = default);
}
