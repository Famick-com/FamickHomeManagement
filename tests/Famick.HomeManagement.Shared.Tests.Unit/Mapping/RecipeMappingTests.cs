using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class RecipeMappingTests
{
    private readonly IMapper _mapper;

    public RecipeMappingTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<RecipeMappingProfile>();
        }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        // Validation skipped: profiles are tested in isolation
        _mapper = config.CreateMapper();
    }

    #region CreateRecipeRequest -> Recipe

    [Fact]
    public void CreateRecipeRequest_To_Recipe_MapsEditableFields()
    {
        var contactId = Guid.NewGuid();
        var request = new CreateRecipeRequest
        {
            Name = "Chocolate Chip Cookies",
            Source = "https://recipes.example.com/cookies",
            Servings = 24,
            Notes = "Double the chocolate chips for extra richness",
            Attribution = "Grandma's recipe",
            IsMeal = false,
            CreatedByContactId = contactId
        };

        var entity = _mapper.Map<Recipe>(request);

        entity.Name.Should().Be("Chocolate Chip Cookies");
        entity.Source.Should().Be("https://recipes.example.com/cookies");
        entity.Servings.Should().Be(24);
        entity.Notes.Should().Be("Double the chocolate chips for extra richness");
        entity.Attribution.Should().Be("Grandma's recipe");
        entity.IsMeal.Should().BeFalse();
        entity.CreatedByContactId.Should().Be(contactId);
    }

    [Fact]
    public void CreateRecipeRequest_To_Recipe_IgnoresSystemFields()
    {
        var request = new CreateRecipeRequest
        {
            Name = "Test Recipe"
        };

        var entity = _mapper.Map<Recipe>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.CreatedByContact.Should().BeNull();
        entity.Steps.Should().BeEmpty();
        entity.Images.Should().BeEmpty();
        entity.NestedRecipes.Should().BeEmpty();
        entity.ParentRecipes.Should().BeEmpty();
        entity.ShareTokens.Should().BeEmpty();
    }

    #endregion

    #region UpdateRecipeRequest -> Recipe

    [Fact]
    public void UpdateRecipeRequest_To_Recipe_MapsEditableFields()
    {
        var contactId = Guid.NewGuid();
        var request = new UpdateRecipeRequest
        {
            Name = "Updated Cookies",
            Source = "Family cookbook",
            Servings = 12,
            Notes = "Use butter, not margarine",
            Attribution = "Mom's kitchen",
            IsMeal = true,
            CreatedByContactId = contactId
        };

        var entity = _mapper.Map<Recipe>(request);

        entity.Name.Should().Be("Updated Cookies");
        entity.Source.Should().Be("Family cookbook");
        entity.Servings.Should().Be(12);
        entity.Notes.Should().Be("Use butter, not margarine");
        entity.Attribution.Should().Be("Mom's kitchen");
        entity.IsMeal.Should().BeTrue();
        entity.CreatedByContactId.Should().Be(contactId);
    }

    [Fact]
    public void UpdateRecipeRequest_To_Recipe_IgnoresSystemFields()
    {
        var request = new UpdateRecipeRequest
        {
            Name = "Test"
        };

        var entity = _mapper.Map<Recipe>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.CreatedByContact.Should().BeNull();
        entity.Steps.Should().BeEmpty();
        entity.Images.Should().BeEmpty();
        entity.NestedRecipes.Should().BeEmpty();
        entity.ParentRecipes.Should().BeEmpty();
        entity.ShareTokens.Should().BeEmpty();
    }

    #endregion

    #region CreateRecipeStepRequest -> RecipeStep

    [Fact]
    public void CreateRecipeStepRequest_To_RecipeStep_MapsEditableFields()
    {
        var request = new CreateRecipeStepRequest
        {
            Title = "Prepare the dough",
            Description = "Mix dry and wet ingredients",
            Instructions = "Combine flour, sugar, and baking soda in a bowl. In a separate bowl, mix eggs, butter, and vanilla.",
            VideoUrl = "https://youtube.com/watch?v=abc123&t=120"
        };

        var entity = _mapper.Map<RecipeStep>(request);

        entity.Title.Should().Be("Prepare the dough");
        entity.Description.Should().Be("Mix dry and wet ingredients");
        entity.Instructions.Should().Be("Combine flour, sugar, and baking soda in a bowl. In a separate bowl, mix eggs, butter, and vanilla.");
        entity.VideoUrl.Should().Be("https://youtube.com/watch?v=abc123&t=120");
    }

    [Fact]
    public void CreateRecipeStepRequest_To_RecipeStep_IgnoresSystemFields()
    {
        var request = new CreateRecipeStepRequest
        {
            Instructions = "Test"
        };

        var entity = _mapper.Map<RecipeStep>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.RecipeId.Should().Be(Guid.Empty);
        entity.StepOrder.Should().Be(0);
        entity.ImageFileName.Should().BeNull();
        entity.ImageOriginalFileName.Should().BeNull();
        entity.ImageContentType.Should().BeNull();
        entity.ImageFileSize.Should().BeNull();
        entity.ImageExternalUrl.Should().BeNull();
        entity.Ingredients.Should().BeEmpty();
    }

    #endregion

    #region UpdateRecipeStepRequest -> RecipeStep

    [Fact]
    public void UpdateRecipeStepRequest_To_RecipeStep_MapsEditableFields()
    {
        var request = new UpdateRecipeStepRequest
        {
            Title = "Updated step title",
            Description = "Updated description",
            Instructions = "New instructions for the step",
            VideoUrl = "https://youtube.com/watch?v=xyz789"
        };

        var entity = _mapper.Map<RecipeStep>(request);

        entity.Title.Should().Be("Updated step title");
        entity.Description.Should().Be("Updated description");
        entity.Instructions.Should().Be("New instructions for the step");
        entity.VideoUrl.Should().Be("https://youtube.com/watch?v=xyz789");
    }

    [Fact]
    public void UpdateRecipeStepRequest_To_RecipeStep_IgnoresSystemFields()
    {
        var request = new UpdateRecipeStepRequest
        {
            Instructions = "Test"
        };

        var entity = _mapper.Map<RecipeStep>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.RecipeId.Should().Be(Guid.Empty);
        entity.StepOrder.Should().Be(0);
        entity.ImageFileName.Should().BeNull();
        entity.ImageOriginalFileName.Should().BeNull();
        entity.ImageContentType.Should().BeNull();
        entity.ImageFileSize.Should().BeNull();
        entity.ImageExternalUrl.Should().BeNull();
        entity.Ingredients.Should().BeEmpty();
    }

    #endregion

    #region CreateRecipeIngredientRequest -> RecipePosition

    [Fact]
    public void CreateRecipeIngredientRequest_To_RecipePosition_MapsEditableFields()
    {
        var productId = Guid.NewGuid();
        var quantityUnitId = Guid.NewGuid();

        var request = new CreateRecipeIngredientRequest
        {
            ProductId = productId,
            Amount = 2.5m,
            AmountInGrams = 300m,
            QuantityUnitId = quantityUnitId,
            Note = "finely chopped",
            IngredientGroup = "Dry Ingredients",
            OnlyCheckSingleUnitInStock = true,
            NotCheckStockFulfillment = false,
            SortOrder = 3
        };

        var entity = _mapper.Map<RecipePosition>(request);

        entity.ProductId.Should().Be(productId);
        entity.Amount.Should().Be(2.5m);
        entity.AmountInGrams.Should().Be(300m);
        entity.QuantityUnitId.Should().Be(quantityUnitId);
        entity.Note.Should().Be("finely chopped");
        entity.IngredientGroup.Should().Be("Dry Ingredients");
        entity.OnlyCheckSingleUnitInStock.Should().BeTrue();
        entity.NotCheckStockFulfillment.Should().BeFalse();
        entity.SortOrder.Should().Be(3);
    }

    [Fact]
    public void CreateRecipeIngredientRequest_To_RecipePosition_IgnoresSystemFields()
    {
        var request = new CreateRecipeIngredientRequest
        {
            ProductId = Guid.NewGuid(),
            Amount = 1m
        };

        var entity = _mapper.Map<RecipePosition>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.RecipeStepId.Should().Be(Guid.Empty);
    }

    #endregion

    #region UpdateRecipeIngredientRequest -> RecipePosition

    [Fact]
    public void UpdateRecipeIngredientRequest_To_RecipePosition_MapsEditableFields()
    {
        var quantityUnitId = Guid.NewGuid();

        var request = new UpdateRecipeIngredientRequest
        {
            Amount = 5.0m,
            AmountInGrams = 500m,
            QuantityUnitId = quantityUnitId,
            Note = "room temperature",
            IngredientGroup = "Wet Ingredients",
            OnlyCheckSingleUnitInStock = false,
            NotCheckStockFulfillment = true,
            SortOrder = 1
        };

        var entity = _mapper.Map<RecipePosition>(request);

        entity.Amount.Should().Be(5.0m);
        entity.AmountInGrams.Should().Be(500m);
        entity.QuantityUnitId.Should().Be(quantityUnitId);
        entity.Note.Should().Be("room temperature");
        entity.IngredientGroup.Should().Be("Wet Ingredients");
        entity.OnlyCheckSingleUnitInStock.Should().BeFalse();
        entity.NotCheckStockFulfillment.Should().BeTrue();
        entity.SortOrder.Should().Be(1);
    }

    [Fact]
    public void UpdateRecipeIngredientRequest_To_RecipePosition_IgnoresSystemFields()
    {
        var request = new UpdateRecipeIngredientRequest
        {
            Amount = 1m
        };

        var entity = _mapper.Map<RecipePosition>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.RecipeStepId.Should().Be(Guid.Empty);
        entity.ProductId.Should().Be(Guid.Empty);
    }

    #endregion
}
