using Famick.HomeManagement.Core.DTOs.ShoppingLists;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class ShoppingListMappingTests
{

    #region ShoppingList -> ShoppingListDto

    [Fact]
    public void ShoppingList_To_ShoppingListDto_MapsScalarProperties()
    {
        var id = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var list = new ShoppingList
        {
            Id = id,
            Name = "Weekly Groceries",
            Description = "For the week",
            ShoppingLocationId = locationId,
            ShoppingLocation = new ShoppingLocation { Name = "Costco", IntegrationType = "kroger" },
            Items = new List<ShoppingListItem>(),
            CreatedAt = new DateTime(2025, 1, 1),
            UpdatedAt = new DateTime(2025, 2, 1)
        };

        var dto = ShoppingListMapper.ToDto(list);

        dto.Id.Should().Be(id);
        dto.Name.Should().Be("Weekly Groceries");
        dto.Description.Should().Be("For the week");
        dto.ShoppingLocationId.Should().Be(locationId);
        dto.CreatedAt.Should().Be(new DateTime(2025, 1, 1));
        dto.UpdatedAt.Should().Be(new DateTime(2025, 2, 1));
    }

    [Fact]
    public void ShoppingList_To_ShoppingListDto_ShoppingLocationName_MapsWhenPresent()
    {
        var list = new ShoppingList
        {
            ShoppingLocation = new ShoppingLocation { Name = "Walmart" },
            Items = new List<ShoppingListItem>()
        };

        var dto = ShoppingListMapper.ToDto(list);

        dto.ShoppingLocationName.Should().Be("Walmart");
    }

    [Fact]
    public void ShoppingList_To_ShoppingListDto_ShoppingLocationName_NullWhenNoLocation()
    {
        var list = new ShoppingList
        {
            ShoppingLocation = null,
            Items = new List<ShoppingListItem>()
        };

        var dto = ShoppingListMapper.ToDto(list);

        dto.ShoppingLocationName.Should().BeNull();
    }

    [Fact]
    public void ShoppingList_To_ShoppingListDto_HasStoreIntegration_TrueWhenIntegrationTypeSet()
    {
        var list = new ShoppingList
        {
            ShoppingLocation = new ShoppingLocation { Name = "Kroger", IntegrationType = "kroger" },
            Items = new List<ShoppingListItem>()
        };

        var dto = ShoppingListMapper.ToDto(list);

        dto.HasStoreIntegration.Should().BeTrue();
    }

    [Fact]
    public void ShoppingList_To_ShoppingListDto_HasStoreIntegration_FalseWhenIntegrationTypeEmpty()
    {
        var list = new ShoppingList
        {
            ShoppingLocation = new ShoppingLocation { Name = "Local Store", IntegrationType = "" },
            Items = new List<ShoppingListItem>()
        };

        var dto = ShoppingListMapper.ToDto(list);

        dto.HasStoreIntegration.Should().BeFalse();
    }

    [Fact]
    public void ShoppingList_To_ShoppingListDto_HasStoreIntegration_FalseWhenIntegrationTypeNull()
    {
        var list = new ShoppingList
        {
            ShoppingLocation = new ShoppingLocation { Name = "Manual Store", IntegrationType = null },
            Items = new List<ShoppingListItem>()
        };

        var dto = ShoppingListMapper.ToDto(list);

        dto.HasStoreIntegration.Should().BeFalse();
    }

    [Fact]
    public void ShoppingList_To_ShoppingListDto_HasStoreIntegration_FalseWhenNoLocation()
    {
        var list = new ShoppingList
        {
            ShoppingLocation = null,
            Items = new List<ShoppingListItem>()
        };

        var dto = ShoppingListMapper.ToDto(list);

        dto.HasStoreIntegration.Should().BeFalse();
    }

    [Fact]
    public void ShoppingList_To_ShoppingListDto_CanAddToCart_MatchesHasStoreIntegration()
    {
        var list = new ShoppingList
        {
            ShoppingLocation = new ShoppingLocation { Name = "Kroger", IntegrationType = "kroger" },
            Items = new List<ShoppingListItem>()
        };

        var dto = ShoppingListMapper.ToDto(list);

        dto.CanAddToCart.Should().BeTrue();
    }

    [Fact]
    public void ShoppingList_To_ShoppingListDto_ItemCount_CountsAllItems()
    {
        var list = new ShoppingList
        {
            ShoppingLocation = new ShoppingLocation { Name = "Store" },
            Items = new List<ShoppingListItem>
            {
                new() { IsPurchased = false },
                new() { IsPurchased = true },
                new() { IsPurchased = false }
            }
        };

        var dto = ShoppingListMapper.ToDto(list);

        dto.ItemCount.Should().Be(3);
    }

    [Fact]
    public void ShoppingList_To_ShoppingListDto_PurchasedCount_CountsOnlyPurchased()
    {
        var list = new ShoppingList
        {
            ShoppingLocation = new ShoppingLocation { Name = "Store" },
            Items = new List<ShoppingListItem>
            {
                new() { IsPurchased = false },
                new() { IsPurchased = true },
                new() { IsPurchased = true }
            }
        };

        var dto = ShoppingListMapper.ToDto(list);

        dto.PurchasedCount.Should().Be(2);
    }

    [Fact]
    public void ShoppingList_To_ShoppingListDto_ItemCount_ZeroWhenItemsNull()
    {
        var list = new ShoppingList
        {
            ShoppingLocation = new ShoppingLocation { Name = "Store" },
            Items = null
        };

        var dto = ShoppingListMapper.ToDto(list);

        dto.ItemCount.Should().Be(0);
        dto.PurchasedCount.Should().Be(0);
    }

    [Fact]
    public void ShoppingList_To_ShoppingListDto_ItemCount_ZeroWhenEmpty()
    {
        var list = new ShoppingList
        {
            ShoppingLocation = new ShoppingLocation { Name = "Store" },
            Items = new List<ShoppingListItem>()
        };

        var dto = ShoppingListMapper.ToDto(list);

        dto.ItemCount.Should().Be(0);
        dto.PurchasedCount.Should().Be(0);
    }

    #endregion

    #region ShoppingList -> ShoppingListSummaryDto

    [Fact]
    public void ShoppingList_To_ShoppingListSummaryDto_MapsScalarProperties()
    {
        var id = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var list = new ShoppingList
        {
            Id = id,
            Name = "Party Supplies",
            ShoppingLocationId = locationId,
            ShoppingLocation = new ShoppingLocation { Name = "Target", IntegrationType = null },
            Items = new List<ShoppingListItem>
            {
                new() { IsPurchased = true },
                new() { IsPurchased = false }
            },
            UpdatedAt = new DateTime(2025, 5, 1)
        };

        var dto = ShoppingListMapper.ToSummaryDto(list);

        dto.Id.Should().Be(id);
        dto.Name.Should().Be("Party Supplies");
        dto.ShoppingLocationId.Should().Be(locationId);
        dto.ShoppingLocationName.Should().Be("Target");
        dto.HasStoreIntegration.Should().BeFalse();
        dto.TotalItems.Should().Be(2);
        dto.PurchasedItems.Should().Be(1);
        dto.UpdatedAt.Should().Be(new DateTime(2025, 5, 1));
    }

    [Fact]
    public void ShoppingList_To_ShoppingListSummaryDto_ShoppingLocationName_EmptyWhenNull()
    {
        var list = new ShoppingList
        {
            ShoppingLocation = null,
            Items = new List<ShoppingListItem>()
        };

        var dto = ShoppingListMapper.ToSummaryDto(list);

        dto.ShoppingLocationName.Should().Be(string.Empty);
    }

    [Fact]
    public void ShoppingList_To_ShoppingListSummaryDto_TotalItems_ZeroWhenNull()
    {
        var list = new ShoppingList
        {
            ShoppingLocation = new ShoppingLocation { Name = "Store" },
            Items = null
        };

        var dto = ShoppingListMapper.ToSummaryDto(list);

        dto.TotalItems.Should().Be(0);
        dto.PurchasedItems.Should().Be(0);
    }

    #endregion

    #region ShoppingListItem -> ShoppingListItemDto

    [Fact]
    public void ShoppingListItem_To_ShoppingListItemDto_MapsScalarProperties()
    {
        var id = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var item = new ShoppingListItem
        {
            Id = id,
            ProductId = productId,
            ProductName = "Custom Name",
            Amount = 3.5m,
            Note = "Get organic",
            IsPurchased = true,
            PurchasedAt = new DateTime(2025, 3, 15),
            PurchasedQuantity = 2.0m,
            BestBeforeDate = new DateTime(2025, 4, 1),
            Aisle = "5",
            Shelf = "Top",
            Department = "Dairy",
            ExternalProductId = "ext-123",
            Price = 4.99m,
            ImageUrl = "https://img.example.com/milk.jpg",
            Barcode = "0123456789"
        };

        var dto = ShoppingListMapper.ToItemDto(item);

        dto.Id.Should().Be(id);
        dto.ProductId.Should().Be(productId);
        dto.Amount.Should().Be(3.5m);
        dto.Note.Should().Be("Get organic");
        dto.IsPurchased.Should().BeTrue();
        dto.PurchasedAt.Should().Be(new DateTime(2025, 3, 15));
        dto.PurchasedQuantity.Should().Be(2.0m);
        dto.BestBeforeDate.Should().Be(new DateTime(2025, 4, 1));
        dto.Aisle.Should().Be("5");
        dto.Shelf.Should().Be("Top");
        dto.Department.Should().Be("Dairy");
        dto.ExternalProductId.Should().Be("ext-123");
        dto.Price.Should().Be(4.99m);
        dto.ImageUrl.Should().Be("https://img.example.com/milk.jpg");
        dto.Barcode.Should().Be("0123456789");
    }

    [Fact]
    public void ShoppingListItem_To_ShoppingListItemDto_ProductName_FromProductWhenLinked()
    {
        var item = new ShoppingListItem
        {
            ProductName = "Fallback Name",
            Product = new Product
            {
                Name = "Product Name",
                Location = new Location(),
                QuantityUnitPurchase = new QuantityUnit { Name = "Each" },
                QuantityUnitStock = new QuantityUnit()
            }
        };

        var dto = ShoppingListMapper.ToItemDto(item);

        dto.ProductName.Should().Be("Product Name");
    }

    [Fact]
    public void ShoppingListItem_To_ShoppingListItemDto_ProductName_FallsBackToItemProductName()
    {
        var item = new ShoppingListItem
        {
            ProductName = "Manual Entry",
            Product = null
        };

        var dto = ShoppingListMapper.ToItemDto(item);

        dto.ProductName.Should().Be("Manual Entry");
    }

    [Fact]
    public void ShoppingListItem_To_ShoppingListItemDto_QuantityUnitName_FromProductPurchaseUnit()
    {
        var item = new ShoppingListItem
        {
            Product = new Product
            {
                Name = "Milk",
                Location = new Location(),
                QuantityUnitPurchase = new QuantityUnit { Name = "Gallons" },
                QuantityUnitStock = new QuantityUnit()
            }
        };

        var dto = ShoppingListMapper.ToItemDto(item);

        dto.QuantityUnitName.Should().Be("Gallons");
    }

    [Fact]
    public void ShoppingListItem_To_ShoppingListItemDto_QuantityUnitName_NullWhenNoProduct()
    {
        var item = new ShoppingListItem { Product = null };

        var dto = ShoppingListMapper.ToItemDto(item);

        dto.QuantityUnitName.Should().BeNull();
    }

    [Fact]
    public void ShoppingListItem_To_ShoppingListItemDto_QuantityUnitName_NullWhenNoPurchaseUnit()
    {
        var item = new ShoppingListItem
        {
            Product = new Product
            {
                Name = "Test",
                Location = new Location(),
                QuantityUnitPurchase = null!,
                QuantityUnitStock = new QuantityUnit()
            }
        };

        var dto = ShoppingListMapper.ToItemDto(item);

        dto.QuantityUnitName.Should().BeNull();
    }

    [Fact]
    public void ShoppingListItem_To_ShoppingListItemDto_TracksBestBeforeDate_FromProduct()
    {
        var item = new ShoppingListItem
        {
            Product = new Product
            {
                Name = "Yogurt",
                TracksBestBeforeDate = true,
                DefaultBestBeforeDays = 14,
                LocationId = Guid.NewGuid(),
                Location = new Location(),
                QuantityUnitPurchase = new QuantityUnit(),
                QuantityUnitStock = new QuantityUnit()
            }
        };

        var dto = ShoppingListMapper.ToItemDto(item);

        dto.TracksBestBeforeDate.Should().BeTrue();
        dto.DefaultBestBeforeDays.Should().Be(14);
    }

    [Fact]
    public void ShoppingListItem_To_ShoppingListItemDto_TracksBestBeforeDate_FalseWhenNoProduct()
    {
        var item = new ShoppingListItem { Product = null };

        var dto = ShoppingListMapper.ToItemDto(item);

        dto.TracksBestBeforeDate.Should().BeFalse();
        dto.DefaultBestBeforeDays.Should().Be(0);
    }

    [Fact]
    public void ShoppingListItem_To_ShoppingListItemDto_DefaultLocationId_FromProduct()
    {
        var locationId = Guid.NewGuid();
        var item = new ShoppingListItem
        {
            Product = new Product
            {
                Name = "Cheese",
                LocationId = locationId,
                Location = new Location(),
                QuantityUnitPurchase = new QuantityUnit(),
                QuantityUnitStock = new QuantityUnit()
            }
        };

        var dto = ShoppingListMapper.ToItemDto(item);

        dto.DefaultLocationId.Should().Be(locationId);
    }

    [Fact]
    public void ShoppingListItem_To_ShoppingListItemDto_DefaultLocationId_NullWhenNoProduct()
    {
        var item = new ShoppingListItem { Product = null };

        var dto = ShoppingListMapper.ToItemDto(item);

        dto.DefaultLocationId.Should().BeNull();
    }

    [Fact]
    public void ShoppingListItem_To_ShoppingListItemDto_IgnoredFields_RemainAtDefault()
    {
        var item = new ShoppingListItem
        {
            Product = new Product
            {
                Name = "Test",
                Location = new Location(),
                QuantityUnitPurchase = new QuantityUnit(),
                QuantityUnitStock = new QuantityUnit()
            }
        };

        var dto = ShoppingListMapper.ToItemDto(item);

        dto.IsParentProduct.Should().BeFalse();
        dto.HasChildren.Should().BeFalse();
        dto.ChildProductCount.Should().Be(0);
        dto.HasChildrenAtStore.Should().BeFalse();
        dto.ChildPurchasedQuantity.Should().Be(0);
        dto.ChildProducts.Should().BeNull();
        dto.ChildPurchases.Should().BeNull();
        dto.Barcodes.Should().BeEmpty();
    }

    #endregion

    #region CreateShoppingListRequest -> ShoppingList

    [Fact]
    public void CreateShoppingListRequest_To_ShoppingList_MapsEditableFields()
    {
        var locationId = Guid.NewGuid();
        var request = new CreateShoppingListRequest
        {
            Name = "New List",
            Description = "Test description",
            ShoppingLocationId = locationId
        };

        var entity = ShoppingListMapper.FromCreateRequest(request);

        entity.Name.Should().Be("New List");
        entity.Description.Should().Be("Test description");
        entity.ShoppingLocationId.Should().Be(locationId);
    }

    [Fact]
    public void CreateShoppingListRequest_To_ShoppingList_IgnoredFieldsRemainAtDefault()
    {
        var request = new CreateShoppingListRequest { Name = "Test" };

        var entity = ShoppingListMapper.FromCreateRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.ShoppingLocation.Should().BeNull();
        entity.Items.Should().BeNull();
    }

    #endregion

    #region UpdateShoppingListRequest -> ShoppingList

    [Fact]
    public void UpdateShoppingListRequest_To_ShoppingList_MapsEditableFields()
    {
        var locationId = Guid.NewGuid();
        var request = new UpdateShoppingListRequest
        {
            Name = "Updated List",
            Description = "Updated description",
            ShoppingLocationId = locationId
        };

        var entity = new ShoppingList();
        ShoppingListMapper.ApplyUpdateRequest(request, entity);

        entity.Name.Should().Be("Updated List");
        entity.Description.Should().Be("Updated description");
        entity.ShoppingLocationId.Should().Be(locationId);
    }

    [Fact]
    public void UpdateShoppingListRequest_To_ShoppingList_ShoppingLocationId_NotMappedWhenNull()
    {
        // The .Condition() means ShoppingLocationId only maps when it HasValue
        var existingLocationId = Guid.NewGuid();
        var existingEntity = new ShoppingList
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
            ShoppingLocationId = existingLocationId
        };

        var request = new UpdateShoppingListRequest
        {
            Name = "Updated",
            ShoppingLocationId = null
        };

        ShoppingListMapper.ApplyUpdateRequest(request, existingEntity);

        existingEntity.Name.Should().Be("Updated");
        existingEntity.ShoppingLocationId.Should().Be(existingLocationId);
    }

    [Fact]
    public void UpdateShoppingListRequest_To_ShoppingList_ShoppingLocationId_MappedWhenHasValue()
    {
        var existingLocationId = Guid.NewGuid();
        var newLocationId = Guid.NewGuid();
        var existingEntity = new ShoppingList
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
            ShoppingLocationId = existingLocationId
        };

        var request = new UpdateShoppingListRequest
        {
            Name = "Updated",
            ShoppingLocationId = newLocationId
        };

        ShoppingListMapper.ApplyUpdateRequest(request, existingEntity);

        existingEntity.ShoppingLocationId.Should().Be(newLocationId);
    }

    [Fact]
    public void UpdateShoppingListRequest_To_ShoppingList_IgnoredFieldsRemainAtDefault()
    {
        var request = new UpdateShoppingListRequest { Name = "Test" };

        var entity = new ShoppingList();
        ShoppingListMapper.ApplyUpdateRequest(request, entity);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.ShoppingLocation.Should().BeNull();
        entity.Items.Should().BeNull();
    }

    #endregion

    #region AddShoppingListItemRequest -> ShoppingListItem

    [Fact]
    public void AddShoppingListItemRequest_To_ShoppingListItem_MapsEditableFields()
    {
        var productId = Guid.NewGuid();
        var request = new AddShoppingListItemRequest
        {
            ProductId = productId,
            Amount = 5.0m,
            Note = "Check for sales",
            ExternalProductId = "ext-456",
            Aisle = "3",
            Shelf = "Bottom",
            Department = "Produce",
            Price = 2.49m,
            Barcode = "9999999999",
            ProductName = "Fresh Apples",
            ImageUrl = "https://img.example.com/apples.jpg"
        };

        var entity = ShoppingListMapper.FromAddItemRequest(request);

        entity.ProductId.Should().Be(productId);
        entity.Amount.Should().Be(5.0m);
        entity.Note.Should().Be("Check for sales");
        entity.ExternalProductId.Should().Be("ext-456");
        entity.Aisle.Should().Be("3");
        entity.Shelf.Should().Be("Bottom");
        entity.Department.Should().Be("Produce");
        entity.Price.Should().Be(2.49m);
        entity.Barcode.Should().Be("9999999999");
        entity.ProductName.Should().Be("Fresh Apples");
        entity.ImageUrl.Should().Be("https://img.example.com/apples.jpg");
    }

    [Fact]
    public void AddShoppingListItemRequest_To_ShoppingListItem_IgnoredFieldsRemainAtDefault()
    {
        var request = new AddShoppingListItemRequest { Amount = 1 };

        var entity = ShoppingListMapper.FromAddItemRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.ShoppingListId.Should().Be(Guid.Empty);
        entity.IsPurchased.Should().BeFalse();
        entity.PurchasedAt.Should().BeNull();
        entity.PurchasedQuantity.Should().Be(0);
        entity.BestBeforeDate.Should().BeNull();
        entity.ShoppingList.Should().BeNull();
        entity.Product.Should().BeNull();
        entity.ChildPurchasesJson.Should().BeNull();
    }

    #endregion

    #region UpdateShoppingListItemRequest -> ShoppingListItem

    [Fact]
    public void UpdateShoppingListItemRequest_To_ShoppingListItem_MapsEditableFields()
    {
        var request = new UpdateShoppingListItemRequest
        {
            Amount = 10.0m,
            Note = "Updated note"
        };

        var entity = ShoppingListMapper.FromUpdateItemRequest(request);

        entity.Amount.Should().Be(10.0m);
        entity.Note.Should().Be("Updated note");
    }

    [Fact]
    public void UpdateShoppingListItemRequest_To_ShoppingListItem_IgnoredFieldsRemainAtDefault()
    {
        var request = new UpdateShoppingListItemRequest { Amount = 1 };

        var entity = ShoppingListMapper.FromUpdateItemRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.ShoppingListId.Should().Be(Guid.Empty);
        entity.ProductId.Should().BeNull();
        entity.ProductName.Should().BeNull();
        entity.IsPurchased.Should().BeFalse();
        entity.PurchasedAt.Should().BeNull();
        entity.PurchasedQuantity.Should().Be(0);
        entity.BestBeforeDate.Should().BeNull();
        entity.Aisle.Should().BeNull();
        entity.Shelf.Should().BeNull();
        entity.Department.Should().BeNull();
        entity.ExternalProductId.Should().BeNull();
        entity.ShoppingList.Should().BeNull();
        entity.Product.Should().BeNull();
        entity.ChildPurchasesJson.Should().BeNull();
    }

    #endregion
}
