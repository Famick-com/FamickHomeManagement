using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Core.Interfaces;
using System.Security.Claims;
using Famick.HomeManagement.Core.Validators.Common;
using Famick.HomeManagement.Web.Shared.Authorization;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing recipes, steps, ingredients, images, nesting, and sharing
/// </summary>
[ApiController]
[Route("api/v1/recipes")]
[Authorize]
public class RecipesController : ApiControllerBase
{
    private readonly IRecipeService _recipeService;
    private readonly IRecipeImportJobService _importJobs;
    private readonly IStockService _stockService;
    private readonly IFileStorageService _fileStorage;
    private readonly IFileAccessTokenService _tokenService;
    private readonly IValidator<CreateRecipeRequest> _createValidator;
    private readonly IValidator<UpdateRecipeRequest> _updateValidator;
    private readonly IValidator<CreateRecipeStepRequest> _createStepValidator;
    private readonly IValidator<UpdateRecipeStepRequest> _updateStepValidator;
    private readonly IValidator<CreateRecipeIngredientRequest> _createIngredientValidator;
    private readonly IValidator<UpdateRecipeIngredientRequest> _updateIngredientValidator;
    private readonly IValidator<AddToShoppingListRequest> _addToShoppingListValidator;
    private readonly IValidator<ReorderStepsRequest> _reorderStepsValidator;
    private readonly IValidator<ConsumeRecipeRequest> _consumeValidator;
    private readonly IValidator<SetRecipeRatingRequest> _ratingValidator;
    private readonly IValidator<RecipeFilterRequest> _filterValidator;
    private readonly IValidator<ImportRecipeRequest> _importValidator;

    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp" };
    private const long MaxImageSize = 10 * 1024 * 1024; // 10MB

    public RecipesController(
        IRecipeService recipeService,
        IRecipeImportJobService importJobs,
        IStockService stockService,
        IFileStorageService fileStorage,
        IFileAccessTokenService tokenService,
        IValidator<CreateRecipeRequest> createValidator,
        IValidator<UpdateRecipeRequest> updateValidator,
        IValidator<CreateRecipeStepRequest> createStepValidator,
        IValidator<UpdateRecipeStepRequest> updateStepValidator,
        IValidator<CreateRecipeIngredientRequest> createIngredientValidator,
        IValidator<UpdateRecipeIngredientRequest> updateIngredientValidator,
        IValidator<AddToShoppingListRequest> addToShoppingListValidator,
        IValidator<ReorderStepsRequest> reorderStepsValidator,
        IValidator<ConsumeRecipeRequest> consumeValidator,
        IValidator<SetRecipeRatingRequest> ratingValidator,
        IValidator<RecipeFilterRequest> filterValidator,
        IValidator<ImportRecipeRequest> importValidator,
        ITenantProvider tenantProvider,
        ILogger<RecipesController> logger)
        : base(tenantProvider, logger)
    {
        _recipeService = recipeService;
        _importJobs = importJobs;
        _stockService = stockService;
        _fileStorage = fileStorage;
        _tokenService = tokenService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _createStepValidator = createStepValidator;
        _updateStepValidator = updateStepValidator;
        _createIngredientValidator = createIngredientValidator;
        _updateIngredientValidator = updateIngredientValidator;
        _addToShoppingListValidator = addToShoppingListValidator;
        _reorderStepsValidator = reorderStepsValidator;
        _consumeValidator = consumeValidator;
        _ratingValidator = ratingValidator;
        _filterValidator = filterValidator;
        _importValidator = importValidator;
    }

    #region Recipe CRUD

    /// <summary>
    /// Lists all recipes
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<RecipeSummaryDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> List(
        [FromQuery] RecipeFilterRequest? filter,
        CancellationToken cancellationToken)
    {
        if (filter != null)
        {
            var validationResult = await _filterValidator.ValidateAsync(filter, cancellationToken);
            if (!validationResult.IsValid)
            {
                return ValidationErrorResponse(
                    validationResult.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
                );
            }
        }

        _logger.LogInformation("Listing recipes for tenant {TenantId}", TenantId);

        var recipes = await _recipeService.ListAsync(filter, GetCurrentUserId(), cancellationToken);
        return ApiResponse(recipes);
    }

    /// <summary>
    /// Gets a specific recipe by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RecipeDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting recipe {RecipeId} for tenant {TenantId}", id, TenantId);

        var recipe = await _recipeService.GetByIdAsync(id, GetCurrentUserId(), cancellationToken);

        if (recipe == null)
        {
            return NotFoundResponse($"Recipe with ID {id} not found");
        }

        return ApiResponse(recipe);
    }

    /// <summary>
    /// Creates a new recipe
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(RecipeDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRecipeRequest request,
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

        _logger.LogInformation("Creating recipe '{Name}' for tenant {TenantId}", request.Name, TenantId);

        var recipe = await _recipeService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = recipe.Id },
            recipe
        );
    }

    /// <summary>
    /// Starts importing a recipe from a web page's schema.org Recipe data, with its steps, ingredients,
    /// photo and credit to the source. Each ingredient line is linked to a product where one matches
    /// exactly, and kept as text otherwise. The import runs in the background, as some recipe sites are
    /// slow: check on it at the job's address, and cancel it with a DELETE there.
    /// </summary>
    /// <response code="202">Started; the body is the running job, and Location its address.</response>
    [HttpPost("import")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(RecipeImportJobDto), 202)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Import(
        [FromBody] ImportRecipeRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _importValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        var job = await _importJobs.StartAsync(request.Url, request.AllowDuplicate, GetCurrentUserId(), cancellationToken);
        return AcceptedAtAction(nameof(GetImport), new { jobId = job.JobId }, job);
    }

    /// <summary>
    /// A recipe import as it stands.
    /// </summary>
    /// <response code="200">It finished: the body's result is the recipe and how each line was matched, or it was cancelled.</response>
    /// <response code="202">Still running; check again shortly.</response>
    /// <response code="409">The household already has a recipe from this page; the body's existingRecipeId names it.</response>
    /// <response code="422">The page couldn't be fetched or has no recipe; the body's code says which.</response>
    [HttpGet("import/{jobId:guid}")]
    [ProducesResponseType(typeof(RecipeImportJobDto), 200)]
    [ProducesResponseType(typeof(RecipeImportJobDto), 202)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> GetImport(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _importJobs.GetAsync(jobId, GetCurrentUserId(), cancellationToken);
        if (job == null)
            return NotFound();

        return job.Status switch
        {
            RecipeImportJobStatus.Running => StatusCode(StatusCodes.Status202Accepted, job),
            RecipeImportJobStatus.Failed when job.ErrorCode == RecipeAlreadyImportedException.Code => Conflict(new
            {
                code = job.ErrorCode,
                error_message = job.ErrorMessage,
                existingRecipeId = job.ExistingRecipeId
            }),
            RecipeImportJobStatus.Failed => UnprocessableEntity(new { code = job.ErrorCode, error_message = job.ErrorMessage }),
            _ => Ok(job)
        };
    }

    /// <summary>
    /// Cancels a recipe import. Nothing is saved for an import cancelled before it saved its recipe.
    /// </summary>
    /// <response code="200">Cancelled, or it had already failed; the body is the job.</response>
    /// <response code="409">It had already saved its recipe; the body's recipeId names it.</response>
    [HttpDelete("import/{jobId:guid}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(RecipeImportJobDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CancelImport(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _importJobs.CancelAsync(jobId, cancellationToken);
        if (job == null)
            return NotFound();

        if (job.Status == RecipeImportJobStatus.Succeeded)
        {
            var finished = await _importJobs.GetAsync(jobId, GetCurrentUserId(), cancellationToken);
            return Conflict(new
            {
                code = RecipeImportException.AlreadyFinished,
                error_message = "The recipe had already been imported",
                recipeId = finished?.Result?.Recipe.Id
            });
        }

        return Ok(job);
    }

    /// <summary>
    /// Updates an existing recipe
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(RecipeDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateRecipeRequest request,
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

        _logger.LogInformation("Updating recipe {RecipeId} for tenant {TenantId}", id, TenantId);

        var recipe = await _recipeService.UpdateAsync(id, request, GetCurrentUserId(), cancellationToken);
        return ApiResponse(recipe);
    }

    /// <summary>
    /// Deletes a recipe
    /// </summary>
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
        _logger.LogInformation("Deleting recipe {RecipeId} for tenant {TenantId}", id, TenantId);

        await _recipeService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Steps

    /// <summary>
    /// Adds a step to a recipe
    /// </summary>
    [HttpPost("{id}/steps")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(RecipeStepDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> AddStep(
        Guid id,
        [FromBody] CreateRecipeStepRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _createStepValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Adding step to recipe {RecipeId} for tenant {TenantId}", id, TenantId);

        var step = await _recipeService.AddStepAsync(id, request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, step);
    }

    /// <summary>
    /// Updates a recipe step
    /// </summary>
    [HttpPut("{id}/steps/{stepId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(RecipeStepDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UpdateStep(
        Guid id,
        Guid stepId,
        [FromBody] UpdateRecipeStepRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _updateStepValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Updating step {StepId} in recipe {RecipeId} for tenant {TenantId}", stepId, id, TenantId);

        var step = await _recipeService.UpdateStepAsync(id, stepId, request, cancellationToken);
        return ApiResponse(step);
    }

    /// <summary>
    /// Deletes a recipe step
    /// </summary>
    [HttpDelete("{id}/steps/{stepId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DeleteStep(
        Guid id,
        Guid stepId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting step {StepId} from recipe {RecipeId} for tenant {TenantId}", stepId, id, TenantId);

        await _recipeService.DeleteStepAsync(id, stepId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Reorders steps within a recipe
    /// </summary>
    [HttpPut("{id}/steps/reorder")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ReorderSteps(
        Guid id,
        [FromBody] ReorderStepsRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _reorderStepsValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Reordering steps in recipe {RecipeId} for tenant {TenantId}", id, TenantId);

        await _recipeService.ReorderStepsAsync(id, request, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Step Ingredients

    /// <summary>
    /// Adds an ingredient to a recipe step
    /// </summary>
    [HttpPost("{id}/steps/{stepId}/ingredients")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(RecipeIngredientDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> AddIngredient(
        Guid id,
        Guid stepId,
        [FromBody] CreateRecipeIngredientRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _createIngredientValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Adding ingredient to step {StepId} in recipe {RecipeId} for tenant {TenantId}", stepId, id, TenantId);

        var ingredient = await _recipeService.AddIngredientAsync(id, stepId, request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, ingredient);
    }

    /// <summary>
    /// Updates an ingredient in a recipe step
    /// </summary>
    [HttpPut("{id}/steps/{stepId}/ingredients/{ingredientId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(RecipeIngredientDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UpdateIngredient(
        Guid id,
        Guid stepId,
        Guid ingredientId,
        [FromBody] UpdateRecipeIngredientRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _updateIngredientValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Updating ingredient {IngredientId} in step {StepId} recipe {RecipeId} for tenant {TenantId}",
            ingredientId, stepId, id, TenantId);

        var ingredient = await _recipeService.UpdateIngredientAsync(ingredientId, request, cancellationToken);
        return ApiResponse(ingredient);
    }

    /// <summary>
    /// Removes an ingredient from a recipe step
    /// </summary>
    [HttpDelete("{id}/steps/{stepId}/ingredients/{ingredientId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DeleteIngredient(
        Guid id,
        Guid stepId,
        Guid ingredientId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting ingredient {IngredientId} from step {StepId} recipe {RecipeId} for tenant {TenantId}",
            ingredientId, stepId, id, TenantId);

        await _recipeService.DeleteIngredientAsync(ingredientId, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Images

    /// <summary>
    /// Uploads an image to a recipe
    /// </summary>
    [HttpPost("{id}/images")]
    [Authorize(Policy = "RequireEditor")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(RecipeImageDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UploadImage(
        Guid id,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error_message = "An image file is required" });
        }

        if (!AllowedImageTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            return BadRequest(new { error_message = $"File '{file.FileName}' is not a supported image type. Allowed: jpg, png, webp" });
        }

        if (file.Length > MaxImageSize)
        {
            return BadRequest(new { error_message = $"File '{file.FileName}' exceeds the maximum size of 10MB" });
        }

        _logger.LogInformation("Uploading image to recipe {RecipeId} for tenant {TenantId}", id, TenantId);

        await using var stream = file.OpenReadStream();
        var image = await _recipeService.AddImageAsync(id, stream, file.FileName, file.ContentType, file.Length, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, image);
    }

    /// <summary>
    /// Sets an image as the primary image for a recipe
    /// </summary>
    [HttpPut("{id}/images/{imageId}/primary")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SetPrimaryImage(
        Guid id,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Setting image {ImageId} as primary for recipe {RecipeId} for tenant {TenantId}",
            imageId, id, TenantId);

        await _recipeService.SetPrimaryImageAsync(id, imageId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes a recipe image
    /// </summary>
    [HttpDelete("{id}/images/{imageId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DeleteImage(
        Guid id,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting image {ImageId} from recipe {RecipeId} for tenant {TenantId}",
            imageId, id, TenantId);

        await _recipeService.DeleteImageAsync(imageId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Downloads a recipe image (secure file access with tenant validation)
    /// </summary>
    [HttpGet("{recipeId}/images/{imageId}/download")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DownloadImage(
        Guid recipeId,
        Guid imageId,
        [FromQuery] string? token,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Downloading image {ImageId} for recipe {RecipeId}", imageId, recipeId);

        var expectedTenantId = ValidateFileAccess(_tokenService, token, "recipe-image", imageId);
        if (!expectedTenantId.HasValue)
        {
            return Unauthorized();
        }

        var image = await _recipeService.GetImageByIdAsync(recipeId, imageId, cancellationToken);
        if (image == null)
        {
            return NotFoundResponse("Image not found");
        }

        if (!ValidateTenantAccess(image.TenantId, expectedTenantId.Value))
        {
            return NotFoundResponse("Image not found");
        }

        var stream = await _fileStorage.GetRecipeImageStreamAsync(recipeId, image.FileName, cancellationToken);
        if (stream == null)
        {
            _logger.LogWarning("Image file not found: recipe {RecipeId}, file {FileName}", recipeId, image.FileName);
            return NotFoundResponse("Image file not found");
        }

        return StoredFile(stream, image.ContentType, image.FileName);
    }

    #endregion

    #region Step Images

    /// <summary>
    /// Uploads or sets an image for a recipe step (one image per step)
    /// </summary>
    [HttpPost("{id}/steps/{stepId}/image")]
    [Authorize(Policy = "RequireEditor")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(RecipeStepDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UploadStepImage(
        Guid id,
        Guid stepId,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error_message = "An image file is required" });
        }

        if (!AllowedImageTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            return BadRequest(new { error_message = $"File '{file.FileName}' is not a supported image type. Allowed: jpg, png, webp" });
        }

        if (file.Length > MaxImageSize)
        {
            return BadRequest(new { error_message = $"File '{file.FileName}' exceeds the maximum size of 10MB" });
        }

        _logger.LogInformation("Uploading step image for recipe {RecipeId} step {StepId} for tenant {TenantId}", id, stepId, TenantId);

        await using var stream = file.OpenReadStream();
        var step = await _recipeService.UploadStepImageAsync(id, stepId, stream, file.FileName, file.ContentType, file.Length, cancellationToken);

        return ApiResponse(step);
    }

    /// <summary>
    /// Removes a step image
    /// </summary>
    [HttpDelete("{id}/steps/{stepId}/image")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DeleteStepImage(
        Guid id,
        Guid stepId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting step image for recipe {RecipeId} step {StepId} for tenant {TenantId}", id, stepId, TenantId);

        await _recipeService.DeleteStepImageAsync(id, stepId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Downloads a recipe step image (secure file access with tenant validation)
    /// </summary>
    [HttpGet("{recipeId}/steps/{stepId}/image/download")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DownloadStepImage(
        Guid recipeId,
        Guid stepId,
        [FromQuery] string? token,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Downloading step image for recipe {RecipeId} step {StepId}", recipeId, stepId);

        var expectedTenantId = ValidateFileAccess(_tokenService, token, "recipe-step-image", stepId);
        if (!expectedTenantId.HasValue)
        {
            return Unauthorized();
        }

        // We need to get the step to find the file - using the service to get full recipe
        var recipe = await _recipeService.GetByIdAsync(recipeId, null, cancellationToken);
        if (recipe == null)
        {
            return NotFoundResponse("Recipe not found");
        }

        var step = recipe.Steps.FirstOrDefault(s => s.Id == stepId);
        if (step == null || string.IsNullOrEmpty(step.ImageFileName))
        {
            return NotFoundResponse("Step image not found");
        }

        var stream = await _fileStorage.GetRecipeStepImageStreamAsync(recipeId, stepId, step.ImageFileName, cancellationToken);
        if (stream == null)
        {
            return NotFoundResponse("Step image file not found");
        }

        return StoredFile(stream, step.ImageContentType ?? "image/jpeg", step.ImageFileName);
    }

    #endregion

    #region Nesting

    /// <summary>
    /// Adds a nested recipe (sub-recipe) to a parent recipe
    /// </summary>
    [HttpPost("{id}/nested/{childRecipeId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(422)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> AddNestedRecipe(
        Guid id,
        Guid childRecipeId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding nested recipe {ChildRecipeId} to recipe {RecipeId} for tenant {TenantId}",
            childRecipeId, id, TenantId);

        await _recipeService.AddNestedRecipeAsync(id, childRecipeId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Removes a nested recipe from a parent recipe
    /// </summary>
    [HttpDelete("{id}/nested/{childRecipeId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> RemoveNestedRecipe(
        Guid id,
        Guid childRecipeId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Removing nested recipe {ChildRecipeId} from recipe {RecipeId} for tenant {TenantId}",
            childRecipeId, id, TenantId);

        await _recipeService.RemoveNestedRecipeAsync(id, childRecipeId, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Sharing

    /// <summary>
    /// Generates a share token for a recipe (90-day expiry)
    /// </summary>
    [HttpPost("{id}/share")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(RecipeShareDto), 201)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GenerateShareToken(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating share token for recipe {RecipeId} for tenant {TenantId}", id, TenantId);

        var share = await _recipeService.GenerateShareTokenAsync(id, cancellationToken);
        return CreatedAtAction(nameof(GetByShareToken), new { token = share.Token }, share);
    }

    /// <summary>
    /// Revokes the active share token for a recipe
    /// </summary>
    [HttpDelete("{id}/share")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> RevokeShareToken(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Revoking share token for recipe {RecipeId} for tenant {TenantId}", id, TenantId);

        await _recipeService.RevokeShareTokenAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Gets a recipe by share token (public, no authentication required)
    /// </summary>
    [HttpGet("shared/{token}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RecipeDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetByShareToken(
        string token,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Accessing shared recipe with token {Token}", token);

        var recipe = await _recipeService.GetByShareTokenAsync(token, cancellationToken);

        if (recipe == null)
        {
            return NotFoundResponse("Recipe link expired or not found");
        }

        return ApiResponse(recipe);
    }

    #endregion

    #region Business Logic

    /// <summary>
    /// Checks stock fulfillment for a recipe
    /// </summary>
    [HttpGet("{id}/fulfillment")]
    [ProducesResponseType(typeof(RecipeFulfillmentDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CheckFulfillment(
        Guid id,
        [FromQuery] int? servings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking stock fulfillment for recipe {RecipeId} for tenant {TenantId}", id, TenantId);

        servings = servings is null ? null : Math.Clamp(servings.Value, 1, QueryLimits.MaxServings);
        var fulfillment = await _recipeService.CheckFulfillmentAsync(id, servings, cancellationToken);
        return ApiResponse(fulfillment);
    }

    /// <summary>
    /// Gets aggregated ingredients (flattened from all steps and nested recipes)
    /// </summary>
    [HttpGet("{id}/ingredients")]
    [ProducesResponseType(typeof(List<IngredientRequirementDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetAggregatedIngredients(
        Guid id,
        [FromQuery] int? servings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting aggregated ingredients for recipe {RecipeId} for tenant {TenantId}", id, TenantId);

        servings = servings is null ? null : Math.Clamp(servings.Value, 1, QueryLimits.MaxServings);
        var ingredients = await _recipeService.GetAggregatedIngredientsAsync(id, servings, cancellationToken);
        return ApiResponse(ingredients);
    }

    /// <summary>
    /// Adds missing ingredients to a shopping list
    /// </summary>
    [HttpPost("{id}/add-to-shopping-list")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> AddToShoppingList(
        Guid id,
        [FromBody] AddToShoppingListRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _addToShoppingListValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Adding recipe {RecipeId} ingredients to shopping list {ShoppingListId} for tenant {TenantId}",
            id, request.ShoppingListId, TenantId);

        await _recipeService.AddToShoppingListAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Takes what a recipe used out of stock after it was made. Lines with too little stock consume what
    /// there is and come back with the amount they were short.
    /// </summary>
    [HttpPost("{id}/consume")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ConsumeRecipeResultDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(422)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Consume(
        Guid id,
        [FromBody] ConsumeRecipeRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _consumeValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Consuming {LineCount} ingredients of recipe {RecipeId} at {Servings} servings for tenant {TenantId}",
            request.Lines.Count, id, request.Servings, TenantId);

        var result = await _stockService.ConsumeForRecipeAsync(id, request, cancellationToken);
        return ApiResponse(result);
    }

    /// <summary>
    /// Gets the complete recipe hierarchy (nested recipe tree)
    /// </summary>
    [HttpGet("{id}/hierarchy")]
    [ProducesResponseType(typeof(List<Guid>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetHierarchy(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting recipe hierarchy for recipe {RecipeId} for tenant {TenantId}", id, TenantId);

        var hierarchy = await _recipeService.GetHierarchyAsync(id, cancellationToken);
        return ApiResponse(hierarchy);
    }

    #endregion

    #region Ratings

    /// <summary>
    /// Sets or replaces the caller's rating of a recipe, 1 to 5 stars in half stars. Any member may rate,
    /// Viewers included: it is their own opinion.
    /// </summary>
    [HttpPut("{id}/rating")]
    [NoRolePolicy("A rating is the caller's own, so a Viewer may rate too.")]
    [ProducesResponseType(typeof(RecipeRatingDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SetRating(
        Guid id,
        [FromBody] SetRecipeRatingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return UnauthorizedResponse();
        }

        var validationResult = await _ratingValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Rating recipe {RecipeId} for tenant {TenantId}", id, TenantId);

        var ratings = await _recipeService.SetRatingAsync(id, userId.Value, request.Rating, cancellationToken);
        return ApiResponse(ratings);
    }

    /// <summary>
    /// Clears the caller's rating of a recipe.
    /// </summary>
    [HttpDelete("{id}/rating")]
    [NoRolePolicy("A rating is the caller's own, so a Viewer may rate too.")]
    [ProducesResponseType(typeof(RecipeRatingDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ClearRating(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return UnauthorizedResponse();
        }

        _logger.LogInformation("Clearing a rating of recipe {RecipeId} for tenant {TenantId}", id, TenantId);

        var ratings = await _recipeService.ClearRatingAsync(id, userId.Value, cancellationToken);
        return ApiResponse(ratings);
    }

    #endregion

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
