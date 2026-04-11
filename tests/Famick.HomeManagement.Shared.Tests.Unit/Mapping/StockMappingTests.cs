using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Stock;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class StockMappingTests
{
    private readonly IMapper _mapper;

    public StockMappingTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<StockMappingProfile>();
        }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        // Validation skipped: profiles are tested in isolation
        _mapper = config.CreateMapper();
    }

    [Fact]
    public void StockEntry_To_StockEntryDto_MapsWithNavigationProperties()
    {
        var entry = new StockEntry
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Amount = 5.0m,
            BestBeforeDate = new DateTime(2026, 12, 31),
            Product = new Product
            {
                Name = "Milk",
                Barcodes = new List<ProductBarcode>
                {
                    new() { Barcode = "1234567890" }
                },
                QuantityUnitStock = new QuantityUnit { Name = "Liters" }
            },
            Location = new Location { Name = "Refrigerator" }
        };

        var dto = _mapper.Map<StockEntryDto>(entry);

        dto.Id.Should().Be(entry.Id);
        dto.Amount.Should().Be(5.0m);
        dto.ProductName.Should().Be("Milk");
        dto.ProductBarcode.Should().Be("1234567890");
        dto.LocationName.Should().Be("Refrigerator");
        dto.QuantityUnitName.Should().Be("Liters");
    }

    [Fact]
    public void StockEntry_To_StockEntryDto_HandlesNullProduct()
    {
        var entry = new StockEntry
        {
            Id = Guid.NewGuid(),
            Amount = 1.0m,
            Product = null,
            Location = null
        };

        var dto = _mapper.Map<StockEntryDto>(entry);

        dto.ProductName.Should().Be(string.Empty);
        dto.ProductBarcode.Should().BeNull();
        dto.LocationName.Should().BeNull();
        dto.QuantityUnitName.Should().Be(string.Empty);
    }

    [Fact]
    public void StockEntry_To_StockEntryDto_HandlesProductWithNoBarcodes()
    {
        var entry = new StockEntry
        {
            Id = Guid.NewGuid(),
            Product = new Product
            {
                Name = "Test",
                Barcodes = null,
                QuantityUnitStock = null
            }
        };

        var dto = _mapper.Map<StockEntryDto>(entry);

        dto.ProductBarcode.Should().BeNull();
        dto.QuantityUnitName.Should().Be(string.Empty);
    }

    [Fact]
    public void StockEntry_To_StockEntrySummaryDto_MapsCorrectly()
    {
        var entry = new StockEntry
        {
            Id = Guid.NewGuid(),
            Amount = 3.0m,
            Product = new Product
            {
                Name = "Eggs",
                QuantityUnitStock = new QuantityUnit { Name = "Dozen" }
            },
            Location = new Location { Name = "Kitchen" }
        };

        var dto = _mapper.Map<StockEntrySummaryDto>(entry);

        dto.ProductName.Should().Be("Eggs");
        dto.LocationName.Should().Be("Kitchen");
        dto.QuantityUnitName.Should().Be("Dozen");
    }

    [Fact]
    public void AddStockRequest_To_StockEntry_MapsAmountAndProduct()
    {
        var request = new AddStockRequest
        {
            ProductId = Guid.NewGuid(),
            Amount = 10.0m,
            LocationId = Guid.NewGuid(),
            BestBeforeDate = new DateTime(2027, 6, 1),
            Price = 4.99m
        };

        var entity = _mapper.Map<StockEntry>(request);

        entity.ProductId.Should().Be(request.ProductId);
        entity.Amount.Should().Be(10.0m);
        entity.LocationId.Should().Be(request.LocationId);
        entity.BestBeforeDate.Should().Be(new DateTime(2027, 6, 1));
        entity.Price.Should().Be(4.99m);
    }

    [Fact]
    public void AddStockRequest_To_StockEntry_IgnoresSystemFields()
    {
        var request = new AddStockRequest
        {
            ProductId = Guid.NewGuid(),
            Amount = 1.0m
        };

        var entity = _mapper.Map<StockEntry>(request);

        entity.Id.Should().Be(Guid.Empty);
        entity.TenantId.Should().Be(Guid.Empty);
        entity.StockId.Should().Be(string.Empty);
        entity.Open.Should().BeFalse();
        entity.Product.Should().BeNull();
        entity.Location.Should().BeNull();
    }
}
