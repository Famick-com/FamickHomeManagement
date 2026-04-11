using Famick.HomeManagement.Core.DTOs.Products;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class ProductMappingTests
{

    #region Product -> ProductDto

    [Fact]
    public void Product_To_ProductDto_MapsScalarProperties()
    {
        var id = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var quPurchaseId = Guid.NewGuid();
        var quStockId = Guid.NewGuid();

        var product = new Product
        {
            Id = id,
            Name = "Milk",
            Description = "Whole milk",
            LocationId = locationId,
            QuantityUnitIdPurchase = quPurchaseId,
            QuantityUnitIdStock = quStockId,
            QuantityUnitFactorPurchaseToStock = 1.5m,
            MinStockAmount = 2.0m,
            DefaultBestBeforeDays = 7,
            TracksBestBeforeDate = true,
            IsActive = true,
            ExpiryWarningDays = 3,
            ServingSize = 240m,
            ServingUnit = "ml",
            ServingsPerContainer = 4m,
            Brand = "Organic Valley",
            SaleType = ProductSaleType.Unit,
            DataSourceAttribution = "Open Food Facts",
            CreatedAt = new DateTime(2025, 1, 1),
            UpdatedAt = new DateTime(2025, 2, 1),
            Location = new Location { Name = "Refrigerator" },
            QuantityUnitPurchase = new QuantityUnit { Name = "Gallons" },
            QuantityUnitStock = new QuantityUnit { Name = "Liters" }
        };

        var dto = ProductMapper.ToDto(product);

        dto.Id.Should().Be(id);
        dto.Name.Should().Be("Milk");
        dto.Description.Should().Be("Whole milk");
        dto.LocationId.Should().Be(locationId);
        dto.LocationName.Should().Be("Refrigerator");
        dto.QuantityUnitIdPurchase.Should().Be(quPurchaseId);
        dto.QuantityUnitPurchaseName.Should().Be("Gallons");
        dto.QuantityUnitIdStock.Should().Be(quStockId);
        dto.QuantityUnitStockName.Should().Be("Liters");
        dto.QuantityUnitFactorPurchaseToStock.Should().Be(1.5m);
        dto.MinStockAmount.Should().Be(2.0m);
        dto.DefaultBestBeforeDays.Should().Be(7);
        dto.TracksBestBeforeDate.Should().BeTrue();
        dto.IsActive.Should().BeTrue();
        dto.ExpiryWarningDays.Should().Be(3);
        dto.ServingSize.Should().Be(240m);
        dto.ServingUnit.Should().Be("ml");
        dto.ServingsPerContainer.Should().Be(4m);
        dto.Brand.Should().Be("Organic Valley");
        dto.SaleType.Should().Be(ProductSaleType.Unit);
        dto.DataSourceAttribution.Should().Be("Open Food Facts");
    }

    [Fact]
    public void Product_To_ProductDto_ProductGroupName_MapsWhenPresent()
    {
        var product = CreateMinimalProduct();
        product.ProductGroup = new ProductGroup { Name = "Dairy" };

        var dto = ProductMapper.ToDto(product);

        dto.ProductGroupName.Should().Be("Dairy");
    }

    [Fact]
    public void Product_To_ProductDto_ProductGroupName_NullWhenNoGroup()
    {
        var product = CreateMinimalProduct();
        product.ProductGroup = null;

        var dto = ProductMapper.ToDto(product);

        dto.ProductGroupName.Should().BeNull();
    }

    [Fact]
    public void Product_To_ProductDto_ShoppingLocationName_MapsWhenPresent()
    {
        var product = CreateMinimalProduct();
        product.ShoppingLocation = new ShoppingLocation { Name = "Walmart" };

        var dto = ProductMapper.ToDto(product);

        dto.ShoppingLocationName.Should().Be("Walmart");
    }

    [Fact]
    public void Product_To_ProductDto_ShoppingLocationName_NullWhenNoLocation()
    {
        var product = CreateMinimalProduct();
        product.ShoppingLocation = null;

        var dto = ProductMapper.ToDto(product);

        dto.ShoppingLocationName.Should().BeNull();
    }

    [Fact]
    public void Product_To_ProductDto_ParentProductName_MapsWhenPresent()
    {
        var product = CreateMinimalProduct();
        product.ParentProduct = new Product
        {
            Name = "Generic Milk",
            Location = new Location { Name = "X" },
            QuantityUnitPurchase = new QuantityUnit { Name = "X" },
            QuantityUnitStock = new QuantityUnit { Name = "X" }
        };

        var dto = ProductMapper.ToDto(product);

        dto.ParentProductName.Should().Be("Generic Milk");
    }

    [Fact]
    public void Product_To_ProductDto_ParentProductName_NullWhenNoParent()
    {
        var product = CreateMinimalProduct();
        product.ParentProduct = null;

        var dto = ProductMapper.ToDto(product);

        dto.ParentProductName.Should().BeNull();
    }

    [Fact]
    public void Product_To_ProductDto_MasterProductName_MapsWhenPresent()
    {
        var product = CreateMinimalProduct();
        product.MasterProduct = new MasterProduct { Name = "Master Milk" };

        var dto = ProductMapper.ToDto(product);

        dto.MasterProductName.Should().Be("Master Milk");
    }

    [Fact]
    public void Product_To_ProductDto_MasterProductName_NullWhenNoMasterProduct()
    {
        var product = CreateMinimalProduct();
        product.MasterProduct = null;

        var dto = ProductMapper.ToDto(product);

        dto.MasterProductName.Should().BeNull();
    }

    [Fact]
    public void Product_To_ProductDto_ChildProductCount_MapsFromCollection()
    {
        var product = CreateMinimalProduct();
        product.ChildProducts = new List<Product>
        {
            new() { Name = "Child1", QuantityUnitStock = new QuantityUnit { Name = "pcs" }, Location = new Location(), QuantityUnitPurchase = new QuantityUnit() },
            new() { Name = "Child2", QuantityUnitStock = new QuantityUnit { Name = "pcs" }, Location = new Location(), QuantityUnitPurchase = new QuantityUnit() }
        };

        var dto = ProductMapper.ToDto(product);

        dto.ChildProductCount.Should().Be(2);
    }

    [Fact]
    public void Product_To_ProductDto_ChildProductCount_ZeroWhenNull()
    {
        var product = CreateMinimalProduct();
        product.ChildProducts = null!;

        var dto = ProductMapper.ToDto(product);

        dto.ChildProductCount.Should().Be(0);
    }

    [Fact]
    public void Product_To_ProductDto_IsParentProduct_TrueWhenHasChildren()
    {
        var product = CreateMinimalProduct();
        product.ChildProducts = new List<Product>
        {
            new() { Name = "Child", QuantityUnitStock = new QuantityUnit { Name = "pcs" }, Location = new Location(), QuantityUnitPurchase = new QuantityUnit() }
        };

        var dto = ProductMapper.ToDto(product);

        dto.IsParentProduct.Should().BeTrue();
    }

    [Fact]
    public void Product_To_ProductDto_IsParentProduct_FalseWhenNoChildren()
    {
        var product = CreateMinimalProduct();
        product.ChildProducts = new List<Product>();

        var dto = ProductMapper.ToDto(product);

        dto.IsParentProduct.Should().BeFalse();
    }

    [Fact]
    public void Product_To_ProductDto_IsParentProduct_FalseWhenChildrenNull()
    {
        var product = CreateMinimalProduct();
        product.ChildProducts = null!;

        var dto = ProductMapper.ToDto(product);

        dto.IsParentProduct.Should().BeFalse();
    }

    [Fact]
    public void Product_To_ProductDto_ChildProducts_MapsInlineProjection()
    {
        var childId = Guid.NewGuid();
        var product = CreateMinimalProduct();
        product.ChildProducts = new List<Product>
        {
            new()
            {
                Id = childId,
                Name = "Skim Milk",
                Description = "Low fat",
                QuantityUnitStock = new QuantityUnit { Name = "Liters" },
                Location = new Location(),
                QuantityUnitPurchase = new QuantityUnit()
            }
        };

        var dto = ProductMapper.ToDto(product);

        dto.ChildProducts.Should().HaveCount(1);
        dto.ChildProducts[0].Id.Should().Be(childId);
        dto.ChildProducts[0].Name.Should().Be("Skim Milk");
        dto.ChildProducts[0].Description.Should().Be("Low fat");
        dto.ChildProducts[0].TotalStockAmount.Should().Be(0);
        dto.ChildProducts[0].QuantityUnitStockName.Should().Be("Liters");
        dto.ChildProducts[0].PrimaryImageUrl.Should().BeNull();
    }

    [Fact]
    public void Product_To_ProductDto_ChildProducts_EmptyListWhenNull()
    {
        var product = CreateMinimalProduct();
        product.ChildProducts = null!;

        var dto = ProductMapper.ToDto(product);

        dto.ChildProducts.Should().BeEmpty();
    }

    [Fact]
    public void Product_To_ProductDto_ChildProducts_HandlesNullQuantityUnitStock()
    {
        var product = CreateMinimalProduct();
        product.ChildProducts = new List<Product>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "NoUnit",
                QuantityUnitStock = null!,
                Location = new Location(),
                QuantityUnitPurchase = new QuantityUnit()
            }
        };

        var dto = ProductMapper.ToDto(product);

        dto.ChildProducts[0].QuantityUnitStockName.Should().Be(string.Empty);
    }

    [Fact]
    public void Product_To_ProductDto_Barcodes_MapsCollection()
    {
        var product = CreateMinimalProduct();
        product.Barcodes = new List<ProductBarcode>
        {
            new() { Id = Guid.NewGuid(), Barcode = "1234567890123", Note = "Primary", Type2Prefix = null },
            new() { Id = Guid.NewGuid(), Barcode = "9876543210987", Note = null, Type2Prefix = "20" }
        };

        var dto = ProductMapper.ToDto(product);

        dto.Barcodes.Should().HaveCount(2);
        dto.Barcodes[0].Barcode.Should().Be("1234567890123");
        dto.Barcodes[0].Note.Should().Be("Primary");
        dto.Barcodes[1].Barcode.Should().Be("9876543210987");
        dto.Barcodes[1].Type2Prefix.Should().Be("20");
    }

    [Fact]
    public void Product_To_ProductDto_Images_MapsCollection_UrlIgnored()
    {
        var product = CreateMinimalProduct();
        product.Images = new List<ProductImage>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                FileName = "abc.jpg",
                OriginalFileName = "photo.jpg",
                ContentType = "image/jpeg",
                FileSize = 12345,
                SortOrder = 1,
                IsPrimary = true,
                ExternalUrl = "https://example.com/img.jpg",
                ExternalThumbnailUrl = "https://example.com/thumb.jpg",
                ExternalSource = "openfoodfacts"
            }
        };

        var dto = ProductMapper.ToDto(product);

        dto.Images.Should().HaveCount(1);
        dto.Images[0].FileName.Should().Be("abc.jpg");
        dto.Images[0].IsPrimary.Should().BeTrue();
        dto.Images[0].ExternalUrl.Should().Be("https://example.com/img.jpg");
        dto.Images[0].Url.Should().BeEmpty(); // Ignored, stays at default
    }

    [Fact]
    public void Product_To_ProductDto_OverriddenFields_DeserializesValidJson()
    {
        var product = CreateMinimalProduct();
        product.OverriddenFields = "[\"Name\",\"Description\",\"Brand\"]";

        var dto = ProductMapper.ToDto(product);

        dto.OverriddenFields.Should().BeEquivalentTo(new List<string> { "Name", "Description", "Brand" });
    }

    [Fact]
    public void Product_To_ProductDto_OverriddenFields_EmptyWhenNull()
    {
        var product = CreateMinimalProduct();
        product.OverriddenFields = null;

        var dto = ProductMapper.ToDto(product);

        // DeserializeOverriddenFields returns empty list for null/invalid JSON
        dto.OverriddenFields.Should().BeEmpty();
    }

    [Fact]
    public void Product_To_ProductDto_OverriddenFields_EmptyWhenEmptyString()
    {
        var product = CreateMinimalProduct();
        product.OverriddenFields = "";

        var dto = ProductMapper.ToDto(product);

        dto.OverriddenFields.Should().BeEmpty();
    }

    [Fact]
    public void Product_To_ProductDto_OverriddenFields_EmptyWhenEmptyArray()
    {
        var product = CreateMinimalProduct();
        product.OverriddenFields = "[]";

        var dto = ProductMapper.ToDto(product);

        dto.OverriddenFields.Should().BeEmpty();
    }

    [Fact]
    public void Product_To_ProductDto_OverriddenFields_EmptyWhenInvalidJson()
    {
        var product = CreateMinimalProduct();
        product.OverriddenFields = "not-valid-json{";

        var dto = ProductMapper.ToDto(product);

        dto.OverriddenFields.Should().BeEmpty();
    }

    #endregion

    #region CreateProductRequest -> Product

    [Fact]
    public void CreateProductRequest_To_Product_MapsEditableFields()
    {
        var locationId = Guid.NewGuid();
        var quPurchaseId = Guid.NewGuid();
        var quStockId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var masterId = Guid.NewGuid();

        var request = new CreateProductRequest
        {
            Name = "Eggs",
            Description = "Free range",
            LocationId = locationId,
            QuantityUnitIdPurchase = quPurchaseId,
            QuantityUnitIdStock = quStockId,
            QuantityUnitFactorPurchaseToStock = 12.0m,
            MinStockAmount = 1.0m,
            DefaultBestBeforeDays = 21,
            TracksBestBeforeDate = true,
            IsActive = true,
            ExpiryWarningDays = 5,
            ServingSize = 50m,
            ServingUnit = "g",
            ServingsPerContainer = 12m,
            ProductGroupId = Guid.NewGuid(),
            ShoppingLocationId = Guid.NewGuid(),
            ParentProductId = parentId,
            MasterProductId = masterId,
            Brand = "Happy Hen",
            SaleType = ProductSaleType.Weight
        };

        var entity = ProductMapper.FromCreateRequest(request);

        entity.Name.Should().Be("Eggs");
        entity.Description.Should().Be("Free range");
        entity.LocationId.Should().Be(locationId);
        entity.QuantityUnitIdPurchase.Should().Be(quPurchaseId);
        entity.QuantityUnitIdStock.Should().Be(quStockId);
        entity.QuantityUnitFactorPurchaseToStock.Should().Be(12.0m);
        entity.MinStockAmount.Should().Be(1.0m);
        entity.DefaultBestBeforeDays.Should().Be(21);
        entity.TracksBestBeforeDate.Should().BeTrue();
        entity.IsActive.Should().BeTrue();
        entity.ExpiryWarningDays.Should().Be(5);
        entity.ParentProductId.Should().Be(parentId);
        entity.MasterProductId.Should().Be(masterId);
        entity.Brand.Should().Be("Happy Hen");
        entity.SaleType.Should().Be(ProductSaleType.Weight);
    }

    [Fact]
    public void CreateProductRequest_To_Product_IgnoredFieldsRemainAtDefault()
    {
        var request = new CreateProductRequest { Name = "Test" };

        var entity = ProductMapper.FromCreateRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.OverriddenFields.Should().BeNull();
        entity.Location.Should().BeNull();
        entity.QuantityUnitPurchase.Should().BeNull();
        entity.QuantityUnitStock.Should().BeNull();
        entity.ProductGroup.Should().BeNull();
        entity.ShoppingLocation.Should().BeNull();
        entity.ParentProduct.Should().BeNull();
        entity.ChildProducts.Should().BeEmpty();
        entity.MasterProduct.Should().BeNull();
        entity.Barcodes.Should().BeEmpty();
        entity.Images.Should().BeEmpty();
        entity.Nutrition.Should().BeNull();
        entity.StoreMetadata.Should().BeEmpty();
        entity.Allergens.Should().BeEmpty();
        entity.DietaryConflicts.Should().BeEmpty();
    }

    #endregion

    #region UpdateProductRequest -> Product

    [Fact]
    public void UpdateProductRequest_To_Product_MapsEditableFields()
    {
        var request = new UpdateProductRequest
        {
            Name = "Updated Eggs",
            Description = "Organic",
            LocationId = Guid.NewGuid(),
            QuantityUnitIdPurchase = Guid.NewGuid(),
            QuantityUnitIdStock = Guid.NewGuid(),
            QuantityUnitFactorPurchaseToStock = 6.0m,
            MinStockAmount = 2.0m,
            DefaultBestBeforeDays = 14,
            TracksBestBeforeDate = false,
            IsActive = false,
            Brand = "Farm Fresh",
            SaleType = ProductSaleType.Weight,
            ParentProductId = Guid.NewGuid()
        };

        var entity = ProductMapper.FromUpdateRequest(request);

        entity.Name.Should().Be("Updated Eggs");
        entity.Description.Should().Be("Organic");
        entity.IsActive.Should().BeFalse();
        entity.Brand.Should().Be("Farm Fresh");
        entity.SaleType.Should().Be(ProductSaleType.Weight);
    }

    [Fact]
    public void UpdateProductRequest_To_Product_IgnoredFieldsRemainAtDefault()
    {
        var request = new UpdateProductRequest { Name = "Test" };

        var entity = ProductMapper.FromUpdateRequest(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.MasterProductId.Should().BeNull();
        entity.OverriddenFields.Should().BeNull();
        entity.Location.Should().BeNull();
        entity.QuantityUnitPurchase.Should().BeNull();
        entity.QuantityUnitStock.Should().BeNull();
        entity.ProductGroup.Should().BeNull();
        entity.ShoppingLocation.Should().BeNull();
        entity.ParentProduct.Should().BeNull();
        entity.ChildProducts.Should().BeEmpty();
        entity.MasterProduct.Should().BeNull();
        entity.Barcodes.Should().BeEmpty();
        entity.Images.Should().BeEmpty();
        entity.Nutrition.Should().BeNull();
        entity.StoreMetadata.Should().BeEmpty();
        entity.Allergens.Should().BeEmpty();
        entity.DietaryConflicts.Should().BeEmpty();
    }

    #endregion

    #region ProductBarcode -> ProductBarcodeDto

    [Fact]
    public void ProductBarcode_To_ProductBarcodeDto_MapsAllFields()
    {
        var barcode = new ProductBarcode
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Barcode = "0123456789012",
            Note = "Main barcode",
            Type2Prefix = "21",
            CreatedAt = new DateTime(2025, 3, 1)
        };

        var dto = ProductMapper.ToBarcodeDto(barcode);

        dto.Id.Should().Be(barcode.Id);
        dto.ProductId.Should().Be(barcode.ProductId);
        dto.Barcode.Should().Be("0123456789012");
        dto.Note.Should().Be("Main barcode");
        dto.Type2Prefix.Should().Be("21");
        dto.CreatedAt.Should().Be(new DateTime(2025, 3, 1));
    }

    #endregion

    #region ProductImage -> ProductImageDto

    [Fact]
    public void ProductImage_To_ProductImageDto_MapsAllFieldsExceptUrl()
    {
        var image = new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "img_001.jpg",
            OriginalFileName = "my_photo.jpg",
            ContentType = "image/jpeg",
            FileSize = 54321,
            SortOrder = 2,
            IsPrimary = false,
            ExternalUrl = "https://images.example.com/full.jpg",
            ExternalThumbnailUrl = "https://images.example.com/thumb.jpg",
            ExternalSource = "usda",
            CreatedAt = new DateTime(2025, 4, 1)
        };

        var dto = ProductMapper.ToImageDto(image);

        dto.Id.Should().Be(image.Id);
        dto.ProductId.Should().Be(image.ProductId);
        dto.TenantId.Should().Be(image.TenantId);
        dto.FileName.Should().Be("img_001.jpg");
        dto.OriginalFileName.Should().Be("my_photo.jpg");
        dto.ContentType.Should().Be("image/jpeg");
        dto.FileSize.Should().Be(54321);
        dto.SortOrder.Should().Be(2);
        dto.IsPrimary.Should().BeFalse();
        dto.ExternalUrl.Should().Be("https://images.example.com/full.jpg");
        dto.ExternalThumbnailUrl.Should().Be("https://images.example.com/thumb.jpg");
        dto.ExternalSource.Should().Be("usda");
        dto.Url.Should().BeEmpty(); // Ignored by profile, stays at default
    }

    #endregion

    #region Helpers

    private static Product CreateMinimalProduct()
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Location = new Location { Name = "Default Location" },
            QuantityUnitPurchase = new QuantityUnit { Name = "Each" },
            QuantityUnitStock = new QuantityUnit { Name = "Each" }
        };
    }

    #endregion
}
