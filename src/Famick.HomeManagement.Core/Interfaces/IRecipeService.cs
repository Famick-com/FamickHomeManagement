using Famick.HomeManagement.Core.DTOs.Recipes;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing recipes, steps, ingredients, images, nesting, and sharing
/// </summary>
public interface IRecipeService
{
    #region Recipe CRUD

    /// <summary>
    /// Creates a new recipe.
    /// </summary>
    Task<RecipeDto> CreateAsync(
        CreateRecipeRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a recipe by ID with full details.
    /// </summary>
    Task<RecipeDto?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default);

    /// <summary>
    /// Lists recipes with optional filtering and sorting.
    /// </summary>
    Task<List<RecipeSummaryDto>> ListAsync(
        RecipeFilterRequest? filter = null,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an existing recipe.
    /// </summary>
    Task<RecipeDto> UpdateAsync(
        Guid id,
        UpdateRecipeRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a recipe and all associated data (steps, ingredients, images, share tokens).
    /// </summary>
    Task DeleteAsync(
        Guid id,
        CancellationToken ct = default);

    #endregion

    #region Steps

    /// <summary>
    /// Adds a step to a recipe. StepOrder is auto-assigned.
    /// </summary>
    Task<RecipeStepDto> AddStepAsync(
        Guid recipeId,
        CreateRecipeStepRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an existing recipe step.
    /// </summary>
    Task<RecipeStepDto> UpdateStepAsync(
        Guid recipeId,
        Guid stepId,
        UpdateRecipeStepRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a recipe step and reorders remaining steps.
    /// </summary>
    Task DeleteStepAsync(
        Guid recipeId,
        Guid stepId,
        CancellationToken ct = default);

    /// <summary>
    /// Reorders steps within a recipe.
    /// </summary>
    Task ReorderStepsAsync(
        Guid recipeId,
        ReorderStepsRequest request,
        CancellationToken ct = default);

    #endregion

    #region Ingredients

    /// <summary>
    /// Adds an ingredient to a recipe step.
    /// </summary>
    Task<RecipeIngredientDto> AddIngredientAsync(
        Guid recipeId,
        Guid stepId,
        CreateRecipeIngredientRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an ingredient.
    /// </summary>
    Task<RecipeIngredientDto> UpdateIngredientAsync(
        Guid ingredientId,
        UpdateRecipeIngredientRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes an ingredient.
    /// </summary>
    Task DeleteIngredientAsync(
        Guid ingredientId,
        CancellationToken ct = default);

    #endregion

    #region Images

    /// <summary>
    /// Adds an image to a recipe.
    /// </summary>
    Task<RecipeImageDto> AddImageAsync(
        Guid recipeId,
        Stream stream,
        string fileName,
        string contentType,
        long fileSize,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a recipe image.
    /// </summary>
    Task DeleteImageAsync(
        Guid imageId,
        CancellationToken ct = default);

    /// <summary>
    /// Sets an image as the primary image for a recipe.
    /// </summary>
    Task SetPrimaryImageAsync(
        Guid recipeId,
        Guid imageId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a recipe image by ID (uses IgnoreQueryFilters - caller must validate tenant access).
    /// </summary>
    Task<RecipeImageDto?> GetImageByIdAsync(
        Guid recipeId,
        Guid imageId,
        CancellationToken ct = default);

    #endregion

    #region Step Images

    /// <summary>
    /// Uploads an image for a recipe step (one image per step).
    /// </summary>
    Task<RecipeStepDto> UploadStepImageAsync(
        Guid recipeId,
        Guid stepId,
        Stream stream,
        string fileName,
        string contentType,
        long fileSize,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a step image.
    /// </summary>
    Task DeleteStepImageAsync(
        Guid recipeId,
        Guid stepId,
        CancellationToken ct = default);

    #endregion

    #region Nesting

    /// <summary>
    /// Adds a nested recipe (sub-recipe) to a parent recipe.
    /// </summary>
    Task AddNestedRecipeAsync(
        Guid parentRecipeId,
        Guid childRecipeId,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a nested recipe from a parent recipe.
    /// </summary>
    Task RemoveNestedRecipeAsync(
        Guid parentRecipeId,
        Guid childRecipeId,
        CancellationToken ct = default);

    #endregion

    #region Sharing

    /// <summary>
    /// Generates a share token for a recipe (90-day expiry). Revokes any existing active token.
    /// </summary>
    Task<RecipeShareDto> GenerateShareTokenAsync(
        Guid recipeId,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes the active share token for a recipe.
    /// </summary>
    Task RevokeShareTokenAsync(
        Guid recipeId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a recipe by share token (public, no auth required).
    /// Returns null if token is expired, revoked, or not found.
    /// </summary>
    Task<RecipeDto?> GetByShareTokenAsync(
        string token,
        CancellationToken ct = default);

    #endregion

    #region Business Logic

    /// <summary>
    /// Checks stock fulfillment for a recipe, optionally scaled to different servings.
    /// </summary>
    Task<RecipeFulfillmentDto> CheckFulfillmentAsync(
        Guid recipeId,
        int? servings = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets aggregated ingredients from all steps and nested recipes, optionally scaled.
    /// </summary>
    Task<List<IngredientRequirementDto>> GetAggregatedIngredientsAsync(
        Guid recipeId,
        int? servings = null,
        CancellationToken ct = default);

    /// <summary>
    /// Adds missing ingredients to a shopping list based on current stock.
    /// </summary>
    Task AddToShoppingListAsync(
        Guid recipeId,
        AddToShoppingListRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the complete recipe hierarchy (all nested recipe IDs).
    /// </summary>
    Task<List<Guid>> GetHierarchyAsync(
        Guid recipeId,
        CancellationToken ct = default);

    #endregion
}
