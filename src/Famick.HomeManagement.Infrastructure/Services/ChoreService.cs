using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Chores;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class ChoreService : IChoreService
{
    private readonly HomeManagementDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<ChoreService> _logger;

    public ChoreService(
        HomeManagementDbContext context,
        IMapper mapper,
        ILogger<ChoreService> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    // Chore management
    public async Task<ChoreDto> CreateAsync(
        CreateChoreRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating chore: {Name}", request.Name);

        // Verify product exists if product consumption is enabled
        if (request.ConsumeProductOnExecution && request.ProductId.HasValue)
        {
            var product = await _context.Products.FindAsync(new object[] { request.ProductId.Value }, cancellationToken);
            if (product == null)
            {
                throw new EntityNotFoundException(nameof(Product), request.ProductId.Value);
            }
        }

        var chore = _mapper.Map<Chore>(request);
        chore.Id = Guid.NewGuid();

        // Set initial assignment based on assignment type
        SetInitialAssignment(chore, request.AssignmentType, request.AssignmentConfig);

        _context.Chores.Add(chore);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created chore: {Id} - {Name}", chore.Id, chore.Name);

        // Calculate next execution date for the DTO
        var nextExecutionDate = await CalculateNextExecutionDateInternalAsync(chore, null, cancellationToken);
        var choreDto = _mapper.Map<ChoreDto>(chore);
        choreDto.NextExecutionDate = nextExecutionDate;

        return choreDto;
    }

    public async Task<ChoreDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var chore = await _context.Chores
            .Include(c => c.Product)
            .Include(c => c.NextExecutionAssignedToUser)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (chore == null) return null;

        var lastExecution = await GetLastExecutionTimeAsync(id, cancellationToken);
        var nextExecutionDate = await CalculateNextExecutionDateInternalAsync(chore, lastExecution, cancellationToken);

        var choreDto = _mapper.Map<ChoreDto>(chore);
        choreDto.NextExecutionDate = nextExecutionDate;

        return choreDto;
    }

    public async Task<List<ChoreSummaryDto>> ListAsync(
        ChoreFilterRequest? filter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Chores
            .Include(c => c.NextExecutionAssignedToUser)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(filter?.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(searchTerm) ||
                (c.Description != null && c.Description.ToLower().Contains(searchTerm)));
        }

        if (!string.IsNullOrWhiteSpace(filter?.PeriodType))
        {
            query = query.Where(c => c.PeriodType == filter.PeriodType);
        }

        if (filter?.AssignedToUserId.HasValue == true)
        {
            query = query.Where(c => c.NextExecutionAssignedToUserId == filter.AssignedToUserId.Value);
        }

        var chores = await query.ToListAsync(cancellationToken);

        // Calculate next execution dates and overdue status
        var summaries = new List<ChoreSummaryDto>();
        foreach (var chore in chores)
        {
            var lastExecution = await GetLastExecutionTimeAsync(chore.Id, cancellationToken);
            var nextExecutionDate = await CalculateNextExecutionDateInternalAsync(chore, lastExecution, cancellationToken);

            var summary = _mapper.Map<ChoreSummaryDto>(chore);
            summary.NextExecutionDate = nextExecutionDate;
            summary.IsOverdue = nextExecutionDate.HasValue && nextExecutionDate.Value < DateTime.UtcNow;

            // Apply overdue filter if specified
            if (filter?.IsOverdue.HasValue == true && filter.IsOverdue.Value != summary.IsOverdue)
            {
                continue;
            }

            summaries.Add(summary);
        }

        // Apply sorting
        summaries = (filter?.SortBy?.ToLower()) switch
        {
            "name" => filter.Descending
                ? summaries.OrderByDescending(c => c.Name).ToList()
                : summaries.OrderBy(c => c.Name).ToList(),
            "nextexecutiondate" => filter.Descending
                ? summaries.OrderByDescending(c => c.NextExecutionDate).ToList()
                : summaries.OrderBy(c => c.NextExecutionDate).ToList(),
            _ => summaries.OrderBy(c => c.NextExecutionDate).ToList() // Default sort
        };

        return summaries;
    }

    public async Task<ChoreDto> UpdateAsync(
        Guid id,
        UpdateChoreRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating chore: {Id}", id);

        var chore = await _context.Chores.FindAsync(new object[] { id }, cancellationToken);
        if (chore == null)
        {
            throw new EntityNotFoundException(nameof(Chore), id);
        }

        // Verify product exists if product consumption is enabled
        if (request.ConsumeProductOnExecution && request.ProductId.HasValue)
        {
            var product = await _context.Products.FindAsync(new object[] { request.ProductId.Value }, cancellationToken);
            if (product == null)
            {
                throw new EntityNotFoundException(nameof(Product), request.ProductId.Value);
            }
        }

        _mapper.Map(request, chore);

        // Update assignment based on assignment type
        SetInitialAssignment(chore, request.AssignmentType, request.AssignmentConfig);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated chore: {Id} - {Name}", id, request.Name);

        // Reload and calculate next execution date
        chore = await _context.Chores
            .Include(c => c.Product)
            .Include(c => c.NextExecutionAssignedToUser)
            .FirstAsync(c => c.Id == id, cancellationToken);

        var lastExecution = await GetLastExecutionTimeAsync(id, cancellationToken);
        var nextExecutionDate = await CalculateNextExecutionDateInternalAsync(chore, lastExecution, cancellationToken);

        var choreDto = _mapper.Map<ChoreDto>(chore);
        choreDto.NextExecutionDate = nextExecutionDate;

        return choreDto;
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting chore: {Id}", id);

        var chore = await _context.Chores.FindAsync(new object[] { id }, cancellationToken);
        if (chore == null)
        {
            throw new EntityNotFoundException(nameof(Chore), id);
        }

        _context.Chores.Remove(chore);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted chore: {Id}", id);
    }

    // Execution
    public async Task<ChoreLogDto> ExecuteChoreAsync(
        Guid choreId,
        ExecuteChoreRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing chore: {ChoreId}", choreId);

        var chore = await _context.Chores
            .Include(c => c.Product)
            .FirstOrDefaultAsync(c => c.Id == choreId, cancellationToken);

        if (chore == null)
        {
            throw new EntityNotFoundException(nameof(Chore), choreId);
        }

        var trackedTime = request.TrackedTime ?? DateTime.UtcNow;

        // Create log entry
        var log = new ChoreLog
        {
            Id = Guid.NewGuid(),
            TenantId = chore.TenantId,
            ChoreId = choreId,
            TrackedTime = trackedTime,
            DoneByUserId = request.DoneByUserId,
            Undone = false,
            Skipped = false
        };

        _context.ChoresLog.Add(log);

        // TODO: Future enhancement - consume product if configured (requires StockService)
        // if (chore.ConsumeProductOnExecution && chore.ProductId.HasValue && chore.ProductAmount.HasValue)
        // {
        //     await _stockService.ConsumeAsync(chore.ProductId.Value, chore.ProductAmount.Value, cancellationToken);
        // }

        // Update next execution assignment if using round-robin
        if (chore.AssignmentType == "round-robin")
        {
            await AssignNextExecutionAsync(choreId, null, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Executed chore: {ChoreId}, Log: {LogId}", choreId, log.Id);

        // Reload with navigation properties
        log = await _context.ChoresLog
            .Include(l => l.Chore)
            .Include(l => l.DoneByUser)
            .FirstAsync(l => l.Id == log.Id, cancellationToken);

        return _mapper.Map<ChoreLogDto>(log);
    }

    public async Task UndoExecutionAsync(
        Guid logId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Undoing chore execution: {LogId}", logId);

        var log = await _context.ChoresLog.FindAsync(new object[] { logId }, cancellationToken);
        if (log == null)
        {
            throw new EntityNotFoundException(nameof(ChoreLog), logId);
        }

        log.Undone = true;
        log.UndoneTimestamp = DateTime.UtcNow;

        // TODO: Future enhancement - reverse product consumption if applicable

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Undone chore execution: {LogId}", logId);
    }

    public async Task SkipChoreAsync(
        Guid choreId,
        SkipChoreRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Skipping chore: {ChoreId}", choreId);

        var chore = await _context.Chores.FindAsync(new object[] { choreId }, cancellationToken);
        if (chore == null)
        {
            throw new EntityNotFoundException(nameof(Chore), choreId);
        }

        var log = new ChoreLog
        {
            Id = Guid.NewGuid(),
            TenantId = chore.TenantId,
            ChoreId = choreId,
            Skipped = true,
            ScheduledExecutionTime = request.ScheduledExecutionTime
        };

        _context.ChoresLog.Add(log);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Skipped chore: {ChoreId}", choreId);
    }

    // Scheduling & querying
    public async Task<DateTime?> CalculateNextExecutionDateAsync(
        Guid choreId,
        CancellationToken cancellationToken = default)
    {
        var chore = await _context.Chores.FindAsync(new object[] { choreId }, cancellationToken);
        if (chore == null)
        {
            throw new EntityNotFoundException(nameof(Chore), choreId);
        }

        var lastExecution = await GetLastExecutionTimeAsync(choreId, cancellationToken);
        return await CalculateNextExecutionDateInternalAsync(chore, lastExecution, cancellationToken);
    }

    public async Task<List<ChoreSummaryDto>> GetOverdueChoresAsync(
        CancellationToken cancellationToken = default)
    {
        var allChores = await ListAsync(null, cancellationToken);
        return allChores.Where(c => c.IsOverdue).ToList();
    }

    public async Task<List<ChoreSummaryDto>> GetChoresDueSoonAsync(
        int daysAhead = 7,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(daysAhead);
        var allChores = await ListAsync(null, cancellationToken);

        return allChores
            .Where(c => c.NextExecutionDate.HasValue &&
                       c.NextExecutionDate.Value <= cutoffDate &&
                       c.NextExecutionDate.Value >= DateTime.UtcNow)
            .ToList();
    }

    // Assignment
    public async Task AssignNextExecutionAsync(
        Guid choreId,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var chore = await _context.Chores.FindAsync(new object[] { choreId }, cancellationToken);
        if (chore == null)
        {
            throw new EntityNotFoundException(nameof(Chore), choreId);
        }

        // If userId provided, use it directly
        if (userId.HasValue)
        {
            chore.NextExecutionAssignedToUserId = userId.Value;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        // If no assignment type, clear assignment
        if (string.IsNullOrEmpty(chore.AssignmentType))
        {
            chore.NextExecutionAssignedToUserId = null;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        // Round-robin assignment
        if (chore.AssignmentType == "round-robin")
        {
            var userIds = ParseAssignmentConfig(chore.AssignmentConfig);
            if (userIds.Count == 0)
            {
                chore.NextExecutionAssignedToUserId = null;
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            // Get last assigned user from most recent log entry
            var lastLog = await _context.ChoresLog
                .Where(cl => cl.ChoreId == choreId && !cl.Undone && !cl.Skipped)
                .OrderByDescending(cl => cl.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var lastUserId = lastLog?.DoneByUserId;

            // Find next user in round-robin sequence
            int nextIndex;
            if (lastUserId.HasValue && userIds.Contains(lastUserId.Value))
            {
                var currentIndex = userIds.IndexOf(lastUserId.Value);
                nextIndex = (currentIndex + 1) % userIds.Count;
            }
            else
            {
                nextIndex = 0; // Start from beginning
            }

            chore.NextExecutionAssignedToUserId = userIds[nextIndex];
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    // History
    public async Task<List<ChoreLogDto>> GetExecutionHistoryAsync(
        Guid choreId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ChoresLog
            .Include(l => l.Chore)
            .Include(l => l.DoneByUser)
            .Where(l => l.ChoreId == choreId)
            .OrderByDescending(l => l.CreatedAt)
            .AsQueryable();

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        var logs = await query.ToListAsync(cancellationToken);

        return _mapper.Map<List<ChoreLogDto>>(logs);
    }

    // Private helper methods
    private async Task<DateTime?> GetLastExecutionTimeAsync(
        Guid choreId,
        CancellationToken cancellationToken)
    {
        var lastLog = await _context.ChoresLog
            .Where(cl => cl.ChoreId == choreId && !cl.Undone && !cl.Skipped && cl.TrackedTime.HasValue)
            .OrderByDescending(cl => cl.TrackedTime)
            .FirstOrDefaultAsync(cancellationToken);

        return lastLog?.TrackedTime;
    }

    private async Task<DateTime?> CalculateNextExecutionDateInternalAsync(
        Chore chore,
        DateTime? lastExecution,
        CancellationToken cancellationToken)
    {
        // If manually scheduled, no automatic next execution date
        if (chore.PeriodType == "manually")
        {
            return null;
        }

        DateTime? nextDate;

        // If no execution history, calculate first execution based on period type
        if (lastExecution == null)
        {
            nextDate = chore.PeriodType switch
            {
                "daily" => DateTime.UtcNow.Date, // Today
                "weekly" when chore.PeriodDays.HasValue =>
                    GetNextWeekday(DateTime.UtcNow, (DayOfWeek)chore.PeriodDays.Value),
                "monthly" when chore.PeriodDays.HasValue =>
                    GetNextMonthlyDateFromNow(chore.PeriodDays.Value),
                "dynamic-regular" => DateTime.UtcNow.Date, // Today for first execution
                _ => DateTime.UtcNow
            };
        }
        else
        {
            var baseDate = lastExecution.Value;

            nextDate = chore.PeriodType switch
            {
                "daily" => baseDate.AddDays(1),
                "weekly" when chore.PeriodDays.HasValue =>
                    GetNextWeekday(baseDate.AddDays(1), (DayOfWeek)chore.PeriodDays.Value),
                "monthly" when chore.PeriodDays.HasValue =>
                    GetNextMonthlyDate(baseDate, chore.PeriodDays.Value),
                "dynamic-regular" when chore.PeriodDays.HasValue =>
                    baseDate.AddDays(chore.PeriodDays.Value),
                _ => (DateTime?)null
            };
        }

        // Apply rollover if needed
        if (nextDate.HasValue && chore.Rollover && nextDate.Value < DateTime.UtcNow)
        {
            if (chore.TrackDateOnly)
            {
                // Set to today's date
                nextDate = DateTime.UtcNow.Date;
            }
            else
            {
                // Set to current time
                nextDate = DateTime.UtcNow;
            }
        }

        // Apply TrackDateOnly - strip time portion
        if (nextDate.HasValue && chore.TrackDateOnly)
        {
            nextDate = nextDate.Value.Date;
        }

        await Task.CompletedTask; // Async method signature for consistency
        return nextDate;
    }

    private DateTime GetNextWeekday(DateTime from, DayOfWeek targetDay)
    {
        // Calculate days until target weekday
        int daysUntil = ((int)targetDay - (int)from.DayOfWeek + 7) % 7;
        if (daysUntil == 0)
        {
            // If today is the target day, use today (or next week if we want to skip today)
            daysUntil = 0;
        }
        return from.Date.AddDays(daysUntil);
    }

    private DateTime GetNextMonthlyDateFromNow(int dayOfMonth)
    {
        var now = DateTime.UtcNow;
        var daysInCurrentMonth = DateTime.DaysInMonth(now.Year, now.Month);
        var targetDay = Math.Min(dayOfMonth, daysInCurrentMonth);

        // If the target day hasn't passed this month, use this month
        if (now.Day <= targetDay)
        {
            return new DateTime(now.Year, now.Month, targetDay, 0, 0, 0, DateTimeKind.Utc);
        }

        // Otherwise, use next month
        var nextMonth = now.AddMonths(1);
        var daysInNextMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
        targetDay = Math.Min(dayOfMonth, daysInNextMonth);
        return new DateTime(nextMonth.Year, nextMonth.Month, targetDay, 0, 0, 0, DateTimeKind.Utc);
    }

    private DateTime GetNextMonthlyDate(DateTime baseDate, int dayOfMonth)
    {
        // Handle edge cases (e.g., Jan 31 -> Feb 28)
        var nextMonth = baseDate.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
        var targetDay = Math.Min(dayOfMonth, daysInMonth);

        return new DateTime(nextMonth.Year, nextMonth.Month, targetDay,
            baseDate.Hour, baseDate.Minute, baseDate.Second, DateTimeKind.Utc);
    }

    private List<Guid> ParseAssignmentConfig(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
            return new List<Guid>();

        // Try to parse as JSON array first (from UI), then fall back to CSV
        if (config.StartsWith("["))
        {
            try
            {
                var guids = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(config);
                return guids ?? new List<Guid>();
            }
            catch
            {
                // Fall through to CSV parsing
            }
        }

        // CSV format: "guid1,guid2,guid3"
        return config.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s.Trim(), out var guid) ? guid : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();
    }

    private void SetInitialAssignment(Chore chore, string? assignmentType, string? assignmentConfig)
    {
        if (string.IsNullOrEmpty(assignmentType))
        {
            chore.NextExecutionAssignedToUserId = null;
            return;
        }

        if (assignmentType == "specific-user" && !string.IsNullOrWhiteSpace(assignmentConfig))
        {
            // For specific-user, the config is the user ID
            if (Guid.TryParse(assignmentConfig.Trim('"'), out var userId))
            {
                chore.NextExecutionAssignedToUserId = userId;
            }
        }
        else if (assignmentType == "round-robin")
        {
            // For round-robin, assign to the first user in the list initially
            var userIds = ParseAssignmentConfig(assignmentConfig);
            chore.NextExecutionAssignedToUserId = userIds.FirstOrDefault();
            if (chore.NextExecutionAssignedToUserId == Guid.Empty)
            {
                chore.NextExecutionAssignedToUserId = null;
            }
        }
    }
}
