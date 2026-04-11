using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class MealPlannerMappingTests
{

    #region CreateMealTypeRequest -> MealType

    [Fact]
    public void CreateMealTypeRequest_To_MealType_MapsAllProperties()
    {
        var request = new CreateMealTypeRequest
        {
            Name = "Breakfast",
            SortOrder = 1,
            Color = "#FF5733"
        };

        var entity = MealPlannerMapper.FromCreateMealTypeRequest(request);

        entity.Name.Should().Be("Breakfast");
        entity.SortOrder.Should().Be(1);
        entity.Color.Should().Be("#FF5733");
    }

    [Fact]
    public void CreateMealTypeRequest_To_MealType_IgnoresSystemFields()
    {
        var request = new CreateMealTypeRequest
        {
            Name = "Lunch",
            SortOrder = 2
        };

        var entity = MealPlannerMapper.FromCreateMealTypeRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.IsDefault.Should().BeFalse();
        entity.CreatedAt.Should().NotBe(default);
        entity.UpdatedAt.Should().BeNull();
        entity.MealPlanEntries.Should().BeEmpty();
    }

    [Fact]
    public void CreateMealTypeRequest_To_MealType_NullColor()
    {
        var request = new CreateMealTypeRequest
        {
            Name = "Snack",
            SortOrder = 3,
            Color = null
        };

        var entity = MealPlannerMapper.FromCreateMealTypeRequest(request);

        entity.Color.Should().BeNull();
    }

    #endregion

    #region UpdateMealTypeRequest -> MealType

    [Fact]
    public void UpdateMealTypeRequest_To_MealType_MapsAllProperties()
    {
        var request = new UpdateMealTypeRequest
        {
            Name = "Dinner",
            SortOrder = 4,
            Color = "#00FF00"
        };

        var entity = new MealType();
        MealPlannerMapper.UpdateMealType(request, entity);

        entity.Name.Should().Be("Dinner");
        entity.SortOrder.Should().Be(4);
        entity.Color.Should().Be("#00FF00");
    }

    [Fact]
    public void UpdateMealTypeRequest_To_MealType_IgnoresSystemFields()
    {
        var existingId = Guid.NewGuid();
        var existingTenantId = Guid.NewGuid();
        var existing = new MealType
        {
            Id = existingId,
            TenantId = existingTenantId,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            MealPlanEntries = new List<MealPlanEntry> { new() }
        };

        var request = new UpdateMealTypeRequest
        {
            Name = "Updated Dinner",
            SortOrder = 5,
            Color = "#0000FF"
        };

        MealPlannerMapper.UpdateMealType(request, existing);

        existing.Id.Should().Be(existingId);
        existing.TenantId.Should().Be(existingTenantId);
        existing.IsDefault.Should().BeTrue();
        existing.Name.Should().Be("Updated Dinner");
        existing.SortOrder.Should().Be(5);
        existing.Color.Should().Be("#0000FF");
        existing.MealPlanEntries.Should().HaveCount(1);
    }

    #endregion

    #region MealType -> MealTypeDto

    [Fact]
    public void MealType_To_MealTypeDto_MapsAllProperties()
    {
        var entity = new MealType
        {
            Id = Guid.NewGuid(),
            Name = "Breakfast",
            SortOrder = 1,
            IsDefault = true,
            Color = "#FF5733"
        };

        var dto = MealPlannerMapper.ToMealTypeDto(entity);

        dto.Id.Should().Be(entity.Id);
        dto.Name.Should().Be("Breakfast");
        dto.SortOrder.Should().Be(1);
        dto.IsDefault.Should().BeTrue();
        dto.Color.Should().Be("#FF5733");
    }

    [Fact]
    public void MealType_To_MealTypeDto_NullColor()
    {
        var entity = new MealType
        {
            Id = Guid.NewGuid(),
            Name = "Lunch",
            SortOrder = 2,
            IsDefault = false,
            Color = null
        };

        var dto = MealPlannerMapper.ToMealTypeDto(entity);

        dto.Color.Should().BeNull();
        dto.IsDefault.Should().BeFalse();
    }

    #endregion

    #region CreateMealRequest -> Meal

    [Fact]
    public void CreateMealRequest_To_Meal_MapsAllProperties()
    {
        var request = new CreateMealRequest
        {
            Name = "Chicken Stir Fry",
            Notes = "Quick weeknight dinner",
            IsFavorite = true
        };

        var entity = MealPlannerMapper.FromCreateMealRequest(request);

        entity.Name.Should().Be("Chicken Stir Fry");
        entity.Notes.Should().Be("Quick weeknight dinner");
        entity.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void CreateMealRequest_To_Meal_IgnoresSystemFields()
    {
        var request = new CreateMealRequest
        {
            Name = "Pasta",
            IsFavorite = false
        };

        var entity = MealPlannerMapper.FromCreateMealRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.CreatedAt.Should().NotBe(default);
        entity.UpdatedAt.Should().BeNull();
        entity.Items.Should().BeEmpty();
        entity.MealPlanEntries.Should().BeEmpty();
    }

    [Fact]
    public void CreateMealRequest_To_Meal_NullNotes()
    {
        var request = new CreateMealRequest
        {
            Name = "Simple Meal",
            Notes = null,
            IsFavorite = false
        };

        var entity = MealPlannerMapper.FromCreateMealRequest(request);

        entity.Notes.Should().BeNull();
    }

    #endregion

    #region UpdateMealRequest -> Meal

    [Fact]
    public void UpdateMealRequest_To_Meal_MapsAllProperties()
    {
        var request = new UpdateMealRequest
        {
            Name = "Updated Meal",
            Notes = "Updated notes",
            IsFavorite = true
        };

        var entity = new Meal();
        MealPlannerMapper.UpdateMeal(request, entity);

        entity.Name.Should().Be("Updated Meal");
        entity.Notes.Should().Be("Updated notes");
        entity.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void UpdateMealRequest_To_Meal_IgnoresSystemFields()
    {
        var existingId = Guid.NewGuid();
        var existingTenantId = Guid.NewGuid();
        var existing = new Meal
        {
            Id = existingId,
            TenantId = existingTenantId,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            Items = new List<MealItem> { new() { ItemType = MealItemType.Freetext } },
            MealPlanEntries = new List<MealPlanEntry> { new() }
        };

        var request = new UpdateMealRequest
        {
            Name = "Modified Meal",
            Notes = "Modified notes",
            IsFavorite = false
        };

        MealPlannerMapper.UpdateMeal(request, existing);

        existing.Id.Should().Be(existingId);
        existing.TenantId.Should().Be(existingTenantId);
        existing.Name.Should().Be("Modified Meal");
        existing.Items.Should().HaveCount(1);
        existing.MealPlanEntries.Should().HaveCount(1);
    }

    #endregion

    #region CreateMealItemRequest -> MealItem

    [Fact]
    public void CreateMealItemRequest_To_MealItem_MapsRecipeItem()
    {
        var recipeId = Guid.NewGuid();
        var request = new CreateMealItemRequest
        {
            ItemType = MealItemType.Recipe,
            RecipeId = recipeId,
            ProductId = null,
            ProductQuantity = null,
            ProductQuantityUnitId = null,
            FreetextDescription = null,
            SortOrder = 0
        };

        var entity = MealPlannerMapper.FromCreateMealItemRequest(request);

        entity.ItemType.Should().Be(MealItemType.Recipe);
        entity.RecipeId.Should().Be(recipeId);
        entity.ProductId.Should().BeNull();
        entity.ProductQuantity.Should().BeNull();
        entity.ProductQuantityUnitId.Should().BeNull();
        entity.FreetextDescription.Should().BeNull();
        entity.SortOrder.Should().Be(0);
    }

    [Fact]
    public void CreateMealItemRequest_To_MealItem_MapsProductItem()
    {
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var request = new CreateMealItemRequest
        {
            ItemType = MealItemType.Product,
            RecipeId = null,
            ProductId = productId,
            ProductQuantity = 2.5m,
            ProductQuantityUnitId = unitId,
            FreetextDescription = null,
            SortOrder = 1
        };

        var entity = MealPlannerMapper.FromCreateMealItemRequest(request);

        entity.ItemType.Should().Be(MealItemType.Product);
        entity.ProductId.Should().Be(productId);
        entity.ProductQuantity.Should().Be(2.5m);
        entity.ProductQuantityUnitId.Should().Be(unitId);
        entity.RecipeId.Should().BeNull();
        entity.SortOrder.Should().Be(1);
    }

    [Fact]
    public void CreateMealItemRequest_To_MealItem_MapsFreetextItem()
    {
        var request = new CreateMealItemRequest
        {
            ItemType = MealItemType.Freetext,
            FreetextDescription = "Side salad with vinaigrette",
            SortOrder = 2
        };

        var entity = MealPlannerMapper.FromCreateMealItemRequest(request);

        entity.ItemType.Should().Be(MealItemType.Freetext);
        entity.FreetextDescription.Should().Be("Side salad with vinaigrette");
        entity.SortOrder.Should().Be(2);
    }

    [Fact]
    public void CreateMealItemRequest_To_MealItem_IgnoresSystemFields()
    {
        var request = new CreateMealItemRequest
        {
            ItemType = MealItemType.Freetext,
            FreetextDescription = "Toast",
            SortOrder = 0
        };

        var entity = MealPlannerMapper.FromCreateMealItemRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.MealId.Should().Be(Guid.Empty);
        entity.CreatedAt.Should().NotBe(default);
        entity.UpdatedAt.Should().BeNull();
        entity.Meal.Should().BeNull();
        entity.Recipe.Should().BeNull();
        entity.Product.Should().BeNull();
        entity.ProductQuantityUnit.Should().BeNull();
    }

    #endregion

    #region CreateMealPlanEntryRequest -> MealPlanEntry

    [Fact]
    public void CreateMealPlanEntryRequest_To_MealPlanEntry_MapsAllProperties()
    {
        var mealId = Guid.NewGuid();
        var mealTypeId = Guid.NewGuid();
        var request = new CreateMealPlanEntryRequest
        {
            MealId = mealId,
            InlineNote = null,
            MealTypeId = mealTypeId,
            DayOfWeek = 3,
            SortOrder = 1,
            IsBatchSource = true,
            BatchSourceEntryId = null
        };

        var entity = MealPlannerMapper.FromCreateMealPlanEntryRequest(request);

        entity.MealId.Should().Be(mealId);
        entity.InlineNote.Should().BeNull();
        entity.MealTypeId.Should().Be(mealTypeId);
        entity.DayOfWeek.Should().Be(3);
        entity.SortOrder.Should().Be(1);
        entity.IsBatchSource.Should().BeTrue();
        entity.BatchSourceEntryId.Should().BeNull();
    }

    [Fact]
    public void CreateMealPlanEntryRequest_To_MealPlanEntry_MapsInlineNote()
    {
        var mealTypeId = Guid.NewGuid();
        var request = new CreateMealPlanEntryRequest
        {
            MealId = null,
            InlineNote = "Leftovers from Sunday",
            MealTypeId = mealTypeId,
            DayOfWeek = 1,
            SortOrder = 0,
            IsBatchSource = false,
            BatchSourceEntryId = null
        };

        var entity = MealPlannerMapper.FromCreateMealPlanEntryRequest(request);

        entity.MealId.Should().BeNull();
        entity.InlineNote.Should().Be("Leftovers from Sunday");
    }

    [Fact]
    public void CreateMealPlanEntryRequest_To_MealPlanEntry_MapsBatchDependent()
    {
        var batchSourceId = Guid.NewGuid();
        var request = new CreateMealPlanEntryRequest
        {
            MealId = Guid.NewGuid(),
            MealTypeId = Guid.NewGuid(),
            DayOfWeek = 4,
            SortOrder = 0,
            IsBatchSource = false,
            BatchSourceEntryId = batchSourceId
        };

        var entity = MealPlannerMapper.FromCreateMealPlanEntryRequest(request);

        entity.IsBatchSource.Should().BeFalse();
        entity.BatchSourceEntryId.Should().Be(batchSourceId);
    }

    [Fact]
    public void CreateMealPlanEntryRequest_To_MealPlanEntry_IgnoresSystemFields()
    {
        var request = new CreateMealPlanEntryRequest
        {
            MealId = Guid.NewGuid(),
            MealTypeId = Guid.NewGuid(),
            DayOfWeek = 0,
            SortOrder = 0,
            IsBatchSource = false
        };

        var entity = MealPlannerMapper.FromCreateMealPlanEntryRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.MealPlanId.Should().Be(Guid.Empty);
        entity.CreatedAt.Should().NotBe(default);
        entity.UpdatedAt.Should().BeNull();
        entity.MealPlan.Should().BeNull();
        entity.Meal.Should().BeNull();
        entity.MealType.Should().BeNull();
        entity.BatchSourceEntry.Should().BeNull();
        entity.BatchDependentEntries.Should().BeEmpty();
    }

    #endregion

    #region UpdateMealPlanEntryRequest -> MealPlanEntry

    [Fact]
    public void UpdateMealPlanEntryRequest_To_MealPlanEntry_MapsAllProperties()
    {
        var mealId = Guid.NewGuid();
        var mealTypeId = Guid.NewGuid();
        var request = new UpdateMealPlanEntryRequest
        {
            MealId = mealId,
            InlineNote = null,
            MealTypeId = mealTypeId,
            DayOfWeek = 6,
            SortOrder = 2,
            IsBatchSource = false,
            BatchSourceEntryId = Guid.NewGuid()
        };

        var entity = new MealPlanEntry();
        MealPlannerMapper.UpdateMealPlanEntry(request, entity);

        entity.MealId.Should().Be(mealId);
        entity.InlineNote.Should().BeNull();
        entity.MealTypeId.Should().Be(mealTypeId);
        entity.DayOfWeek.Should().Be(6);
        entity.SortOrder.Should().Be(2);
        entity.IsBatchSource.Should().BeFalse();
        entity.BatchSourceEntryId.Should().Be(request.BatchSourceEntryId);
    }

    [Fact]
    public void UpdateMealPlanEntryRequest_To_MealPlanEntry_IgnoresSystemFields()
    {
        var existingId = Guid.NewGuid();
        var existingMealPlanId = Guid.NewGuid();
        var existing = new MealPlanEntry
        {
            Id = existingId,
            MealPlanId = existingMealPlanId,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            BatchDependentEntries = new List<MealPlanEntry> { new() }
        };

        var request = new UpdateMealPlanEntryRequest
        {
            MealId = Guid.NewGuid(),
            MealTypeId = Guid.NewGuid(),
            DayOfWeek = 2,
            SortOrder = 1,
            IsBatchSource = true
        };

        MealPlannerMapper.UpdateMealPlanEntry(request, existing);

        existing.Id.Should().Be(existingId);
        existing.MealPlanId.Should().Be(existingMealPlanId);
        existing.BatchDependentEntries.Should().HaveCount(1);
    }

    #endregion
}
