using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Validators.MealPlanner;
using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Famick.HomeManagement.Shared.Tests.Unit.Validators;

public class CreateMealTypeRequestValidatorTests
{
    private readonly CreateMealTypeRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var request = new CreateMealTypeRequest { Name = "Brunch", SortOrder = 5, Color = "#FF5722" };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyName_FailsValidation()
    {
        var request = new CreateMealTypeRequest { Name = "", SortOrder = 0 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void NameTooLong_FailsValidation()
    {
        var request = new CreateMealTypeRequest { Name = new string('A', 101), SortOrder = 0 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void ColorTooLong_FailsValidation()
    {
        var request = new CreateMealTypeRequest { Name = "Test", SortOrder = 0, Color = new string('A', 51) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Color);
    }

    [Fact]
    public void NullColor_PassesValidation()
    {
        var request = new CreateMealTypeRequest { Name = "Test", SortOrder = 0, Color = null };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Color);
    }
}

public class UpdateMealTypeRequestValidatorTests
{
    private readonly UpdateMealTypeRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var request = new UpdateMealTypeRequest { Name = "Brunch", SortOrder = 5 };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyName_FailsValidation()
    {
        var request = new UpdateMealTypeRequest { Name = "", SortOrder = 0 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}

public class CreateMealRequestValidatorTests
{
    private readonly CreateMealRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var request = new CreateMealRequest
        {
            Name = "Pasta Night",
            Items = new List<CreateMealItemRequest>
            {
                new() { ItemType = MealItemType.Freetext, FreetextDescription = "Spaghetti", SortOrder = 0 }
            }
        };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyName_FailsValidation()
    {
        var request = new CreateMealRequest
        {
            Name = "",
            Items = new List<CreateMealItemRequest>
            {
                new() { ItemType = MealItemType.Freetext, FreetextDescription = "Item", SortOrder = 0 }
            }
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void NameTooLong_FailsValidation()
    {
        var request = new CreateMealRequest
        {
            Name = new string('A', 201),
            Items = new List<CreateMealItemRequest>
            {
                new() { ItemType = MealItemType.Freetext, FreetextDescription = "Item", SortOrder = 0 }
            }
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void EmptyItems_FailsValidation()
    {
        var request = new CreateMealRequest
        {
            Name = "Test",
            Items = new List<CreateMealItemRequest>()
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Fact]
    public void NotesTooLong_FailsValidation()
    {
        var request = new CreateMealRequest
        {
            Name = "Test",
            Notes = new string('A', 2001),
            Items = new List<CreateMealItemRequest>
            {
                new() { ItemType = MealItemType.Freetext, FreetextDescription = "Item", SortOrder = 0 }
            }
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }
}

public class CreateMealItemRequestValidatorTests
{
    private readonly CreateMealItemRequestValidator _validator = new();

    [Fact]
    public void RecipeItem_WithRecipeId_PassesValidation()
    {
        var request = new CreateMealItemRequest
        {
            ItemType = MealItemType.Recipe,
            RecipeId = Guid.NewGuid(),
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RecipeItem_WithoutRecipeId_FailsValidation()
    {
        var request = new CreateMealItemRequest
        {
            ItemType = MealItemType.Recipe,
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.RecipeId);
    }

    [Fact]
    public void ProductItem_WithProductId_PassesValidation()
    {
        var request = new CreateMealItemRequest
        {
            ItemType = MealItemType.Product,
            ProductId = Guid.NewGuid(),
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ProductItem_WithoutProductId_FailsValidation()
    {
        var request = new CreateMealItemRequest
        {
            ItemType = MealItemType.Product,
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ProductId);
    }

    [Fact]
    public void ProductItem_WithZeroQuantity_FailsValidation()
    {
        var request = new CreateMealItemRequest
        {
            ItemType = MealItemType.Product,
            ProductId = Guid.NewGuid(),
            ProductQuantity = 0,
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ProductQuantity);
    }

    [Fact]
    public void FreetextItem_WithDescription_PassesValidation()
    {
        var request = new CreateMealItemRequest
        {
            ItemType = MealItemType.Freetext,
            FreetextDescription = "Side salad",
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FreetextItem_WithoutDescription_FailsValidation()
    {
        var request = new CreateMealItemRequest
        {
            ItemType = MealItemType.Freetext,
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.FreetextDescription);
    }

    [Fact]
    public void FreetextItem_DescriptionTooLong_FailsValidation()
    {
        var request = new CreateMealItemRequest
        {
            ItemType = MealItemType.Freetext,
            FreetextDescription = new string('A', 501),
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.FreetextDescription);
    }
}

public class CreateMealPlanEntryRequestValidatorTests
{
    private readonly CreateMealPlanEntryRequestValidator _validator = new();

    [Fact]
    public void MealEntry_PassesValidation()
    {
        var request = new CreateMealPlanEntryRequest
        {
            MealId = Guid.NewGuid(),
            MealTypeId = Guid.NewGuid(),
            DayOfWeek = 0,
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void InlineNoteEntry_PassesValidation()
    {
        var request = new CreateMealPlanEntryRequest
        {
            InlineNote = "Leftovers",
            MealTypeId = Guid.NewGuid(),
            DayOfWeek = 3,
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void BothMealIdAndInlineNote_FailsValidation()
    {
        var request = new CreateMealPlanEntryRequest
        {
            MealId = Guid.NewGuid(),
            InlineNote = "Note",
            MealTypeId = Guid.NewGuid(),
            DayOfWeek = 0,
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void NeitherMealIdNorInlineNote_FailsValidation()
    {
        var request = new CreateMealPlanEntryRequest
        {
            MealTypeId = Guid.NewGuid(),
            DayOfWeek = 0,
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void InlineNoteTooLong_FailsValidation()
    {
        var request = new CreateMealPlanEntryRequest
        {
            InlineNote = new string('A', 201),
            MealTypeId = Guid.NewGuid(),
            DayOfWeek = 0,
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.InlineNote);
    }

    [Fact]
    public void EmptyMealTypeId_FailsValidation()
    {
        var request = new CreateMealPlanEntryRequest
        {
            MealId = Guid.NewGuid(),
            MealTypeId = Guid.Empty,
            DayOfWeek = 0,
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.MealTypeId);
    }

    [Fact]
    public void DayOfWeekOutOfRange_FailsValidation()
    {
        var request = new CreateMealPlanEntryRequest
        {
            MealId = Guid.NewGuid(),
            MealTypeId = Guid.NewGuid(),
            DayOfWeek = 7,
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DayOfWeek);
    }

    [Fact]
    public void NegativeDayOfWeek_FailsValidation()
    {
        var request = new CreateMealPlanEntryRequest
        {
            MealId = Guid.NewGuid(),
            MealTypeId = Guid.NewGuid(),
            DayOfWeek = -1,
            SortOrder = 0
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DayOfWeek);
    }

    [Fact]
    public void BatchSourceAndBatchSourceEntryId_FailsValidation()
    {
        var request = new CreateMealPlanEntryRequest
        {
            MealId = Guid.NewGuid(),
            MealTypeId = Guid.NewGuid(),
            DayOfWeek = 0,
            SortOrder = 0,
            IsBatchSource = true,
            BatchSourceEntryId = Guid.NewGuid()
        };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void BatchFieldsWithoutMeal_FailsValidation()
    {
        var request = new CreateMealPlanEntryRequest
        {
            InlineNote = "Note",
            MealTypeId = Guid.NewGuid(),
            DayOfWeek = 0,
            SortOrder = 0,
            IsBatchSource = true
        };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeFalse();
    }
}

public class GenerateShoppingListRequestValidatorTests
{
    private readonly GenerateShoppingListRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var request = new GenerateShoppingListRequest { ShoppingListId = Guid.NewGuid() };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyShoppingListId_FailsValidation()
    {
        var request = new GenerateShoppingListRequest { ShoppingListId = Guid.Empty };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ShoppingListId);
    }
}

public class UpdateDietaryProfileRequestValidatorTests
{
    private readonly UpdateDietaryProfileRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var request = new UpdateDietaryProfileRequest
        {
            DietaryNotes = "No spicy food",
            Allergens = new List<UpdateContactAllergenRequest>
            {
                new() { AllergenType = AllergenType.Peanuts, Severity = AllergenSeverity.Allergy }
            },
            DietaryPreferences = new List<DietaryPreference> { DietaryPreference.Vegetarian }
        };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DietaryNotesTooLong_FailsValidation()
    {
        var request = new UpdateDietaryProfileRequest
        {
            DietaryNotes = new string('A', 501),
            Allergens = new List<UpdateContactAllergenRequest>(),
            DietaryPreferences = new List<DietaryPreference>()
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DietaryNotes);
    }

    [Fact]
    public void InvalidAllergenType_FailsValidation()
    {
        var request = new UpdateDietaryProfileRequest
        {
            Allergens = new List<UpdateContactAllergenRequest>
            {
                new() { AllergenType = (AllergenType)999, Severity = AllergenSeverity.Allergy }
            },
            DietaryPreferences = new List<DietaryPreference>()
        };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void InvalidDietaryPreference_FailsValidation()
    {
        var request = new UpdateDietaryProfileRequest
        {
            Allergens = new List<UpdateContactAllergenRequest>(),
            DietaryPreferences = new List<DietaryPreference> { (DietaryPreference)999 }
        };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeFalse();
    }
}
