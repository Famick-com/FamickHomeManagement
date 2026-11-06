using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Starts, reports and cancels recipe imports run by <see cref="IRecipeImportJobRunner"/>. A job is the
/// household's: the tenant query filter keeps another household from seeing or cancelling it.
/// </summary>
public class RecipeImportJobService : IRecipeImportJobService
{
    /// <summary>
    /// A job still running after this was stopped part way, as by a deploy, and is reported as
    /// interrupted. Longer than <see cref="RecipeImportJobRunner.RunLimit"/>, so a live job never is.
    /// </summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(2);

    /// <summary>How long a finished job is kept for a client to read before it's cleared.</summary>
    public static readonly TimeSpan KeepFor = TimeSpan.FromDays(1);

    private readonly HomeManagementDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly IRecipeImportJobRunner _runner;
    private readonly IRecipeService _recipeService;
    private readonly ILogger<RecipeImportJobService> _logger;

    public RecipeImportJobService(
        HomeManagementDbContext context,
        ITenantProvider tenantProvider,
        IRecipeImportJobRunner runner,
        IRecipeService recipeService,
        ILogger<RecipeImportJobService> logger)
    {
        _context = context;
        _tenantProvider = tenantProvider;
        _runner = runner;
        _recipeService = recipeService;
        _logger = logger;
    }

    public async Task<RecipeImportJobDto> StartAsync(string url, bool allowDuplicate, Guid? userId, CancellationToken ct = default)
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new InvalidOperationException("A recipe import needs a household");

        // Old jobs go as new ones come, so the table holds about a day of each household's.
        var cutoff = DateTime.UtcNow - KeepFor;
        _context.RecipeImportJobs.RemoveRange(
            await _context.RecipeImportJobs.Where(j => j.CreatedAt < cutoff).ToListAsync(ct));

        var job = new RecipeImportJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Url = url.Trim(),
            AllowDuplicate = allowDuplicate,
            Status = RecipeImportJobStatus.Running,
            Version = Guid.NewGuid()
        };
        _context.RecipeImportJobs.Add(job);
        await _context.SaveChangesAsync(ct);

        _runner.Start(job.Id, tenantId, userId);
        _logger.LogInformation("Started recipe import job {JobId}", job.Id);

        return ToDto(job);
    }

    public async Task<RecipeImportJobDto?> GetAsync(Guid jobId, Guid? userId, CancellationToken ct = default)
    {
        var job = await _context.RecipeImportJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job == null)
            return null;

        var dto = ToDto(job);
        if (job.Status == RecipeImportJobStatus.Succeeded && job.RecipeId is { } recipeId
            && await _recipeService.GetByIdAsync(recipeId, userId, ct) is { } recipe)
        {
            dto.Result = new RecipeImportResultDto
            {
                Recipe = recipe,
                Ingredients = job.IngredientsJson is { } json
                    ? JsonSerializer.Deserialize<List<ImportedIngredientDto>>(json) ?? []
                    : []
            };
        }

        return dto;
    }

    public async Task<RecipeImportJobDto?> CancelAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _context.RecipeImportJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job == null)
            return null;

        if (job.Status == RecipeImportJobStatus.Running)
        {
            job.Status = RecipeImportJobStatus.Cancelled;
            job.Version = Guid.NewGuid();
            job.CompletedAt = DateTime.UtcNow;
            try
            {
                await _context.SaveChangesAsync(ct);
                _runner.Cancel(jobId);
                _logger.LogInformation("Cancelled recipe import job {JobId}", jobId);
            }
            catch (DbUpdateConcurrencyException)
            {
                // It finished as the cancel came in; the import's outcome stands.
                _context.ChangeTracker.Clear();
                job = await _context.RecipeImportJobs.AsNoTracking().FirstAsync(j => j.Id == jobId, ct);
            }
        }

        return ToDto(job);
    }

    private static RecipeImportJobDto ToDto(RecipeImportJob job)
    {
        var dto = new RecipeImportJobDto
        {
            JobId = job.Id,
            Status = job.Status,
            ErrorCode = job.ErrorCode,
            ErrorMessage = job.ErrorMessage,
            ExistingRecipeId = job.ExistingRecipeId
        };

        // Still running long after any import could: the instance running it stopped.
        if (job.Status == RecipeImportJobStatus.Running && job.CreatedAt < DateTime.UtcNow - StaleAfter)
        {
            dto.Status = RecipeImportJobStatus.Failed;
            dto.ErrorCode = RecipeImportException.ImportInterrupted;
            dto.ErrorMessage = "The import stopped before it finished";
        }

        return dto;
    }
}
