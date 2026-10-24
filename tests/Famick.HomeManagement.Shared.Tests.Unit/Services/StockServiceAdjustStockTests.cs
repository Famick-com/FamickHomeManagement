using System.ComponentModel.DataAnnotations;
using Famick.HomeManagement.Core.DTOs.Stock;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Editing a stock entry: a null value leaves a field unchanged, and only the Clear flags remove a
/// best-before date or a location.
/// </summary>
public class StockServiceAdjustStockTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly StockService _service;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _pantryId = Guid.NewGuid();
    private readonly Guid _fridgeId = Guid.NewGuid();
    private readonly DateTime _bestBefore = new(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);
    private Guid _entryId;

    public StockServiceAdjustStockTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options);

        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        tenantProvider.Setup(t => t.UserId).Returns(_userId);

        _service = new StockService(_context, tenantProvider.Object, new Mock<IFileUrlService>().Object);

        SeedEntry();
    }

    private void SeedEntry()
    {
        _context.Locations.AddRange(
            new Location { Id = _pantryId, Name = "Pantry", TenantId = _tenantId },
            new Location { Id = _fridgeId, Name = "Fridge", TenantId = _tenantId });

        var unit = new QuantityUnit { Id = Guid.NewGuid(), Name = "Gallon", TenantId = _tenantId };
        _context.QuantityUnits.Add(unit);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Milk",
            LocationId = _pantryId,
            QuantityUnitIdPurchase = unit.Id,
            QuantityUnitIdStock = unit.Id,
            TenantId = _tenantId
        };
        _context.Products.Add(product);

        _entryId = Guid.NewGuid();
        _context.Stock.Add(new StockEntry
        {
            Id = _entryId,
            TenantId = _tenantId,
            ProductId = product.Id,
            Amount = 2,
            BestBeforeDate = _bestBefore,
            PurchasedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            StockId = "stock-1",
            LocationId = _pantryId,
            Note = "Top shelf"
        });

        _context.SaveChanges();
    }

    private async Task<StockEntry> ReloadEntryAsync()
    {
        _context.ChangeTracker.Clear();
        return await _context.Stock.SingleAsync(s => s.Id == _entryId);
    }

    #region Service

    [Fact]
    public async Task AdjustStock_WithoutClearFlags_LeavesDateLocationAndNoteUnchanged()
    {
        // What the existing web callers send: null for everything they did not set.
        await _service.AdjustStockAsync(_entryId, new AdjustStockRequest { Amount = 1 });

        var entry = await ReloadEntryAsync();
        entry.Amount.Should().Be(1);
        entry.BestBeforeDate.Should().Be(_bestBefore);
        entry.LocationId.Should().Be(_pantryId);
        entry.Note.Should().Be("Top shelf");
    }

    [Fact]
    public async Task AdjustStock_ClearBestBeforeDate_RemovesTheDateOnly()
    {
        var result = await _service.AdjustStockAsync(_entryId, new AdjustStockRequest
        {
            Amount = 2,
            ClearBestBeforeDate = true
        });

        result.BestBeforeDate.Should().BeNull();

        var entry = await ReloadEntryAsync();
        entry.BestBeforeDate.Should().BeNull();
        entry.LocationId.Should().Be(_pantryId);
    }

    [Fact]
    public async Task AdjustStock_ClearLocation_RemovesTheLocationOnly()
    {
        var result = await _service.AdjustStockAsync(_entryId, new AdjustStockRequest
        {
            Amount = 2,
            ClearLocation = true
        });

        result.LocationId.Should().BeNull();

        var entry = await ReloadEntryAsync();
        entry.LocationId.Should().BeNull();
        entry.BestBeforeDate.Should().Be(_bestBefore);
    }

    [Fact]
    public async Task AdjustStock_WithNewDateAndLocation_SetsBoth()
    {
        var newDate = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);

        await _service.AdjustStockAsync(_entryId, new AdjustStockRequest
        {
            Amount = 2,
            BestBeforeDate = newDate,
            LocationId = _fridgeId
        });

        var entry = await ReloadEntryAsync();
        entry.BestBeforeDate.Should().Be(newDate);
        entry.LocationId.Should().Be(_fridgeId);
    }

    [Fact]
    public async Task AdjustStock_ClearingBoth_LogsTheBeforeAndAfterValues()
    {
        await _service.AdjustStockAsync(_entryId, new AdjustStockRequest
        {
            Amount = 2,
            ClearBestBeforeDate = true,
            ClearLocation = true
        });

        var logs = await _context.StockLog.Where(l => l.StockRowId == _entryId).ToListAsync();
        var before = logs.Should().ContainSingle(l => l.TransactionType == "stock-edit-old").Subject;
        var after = logs.Should().ContainSingle(l => l.TransactionType == "stock-edit-new").Subject;

        before.BestBeforeDate.Should().Be(_bestBefore);
        before.LocationId.Should().Be(_pantryId);
        after.BestBeforeDate.Should().BeNull();
        after.LocationId.Should().BeNull();
        after.CorrelationId.Should().Be(before.CorrelationId);
    }

    #endregion

    #region Request validation

    private static List<ValidationResult> Validate(AdjustStockRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Request_WithADateAndClearBestBeforeDate_IsInvalid()
    {
        var results = Validate(new AdjustStockRequest
        {
            Amount = 1,
            BestBeforeDate = DateTime.UtcNow,
            ClearBestBeforeDate = true
        });

        results.Should().ContainSingle()
            .Which.MemberNames.Should().BeEquivalentTo(
                nameof(AdjustStockRequest.BestBeforeDate), nameof(AdjustStockRequest.ClearBestBeforeDate));
    }

    [Fact]
    public void Request_WithALocationAndClearLocation_IsInvalid()
    {
        var results = Validate(new AdjustStockRequest
        {
            Amount = 1,
            LocationId = Guid.NewGuid(),
            ClearLocation = true
        });

        results.Should().ContainSingle()
            .Which.MemberNames.Should().BeEquivalentTo(
                nameof(AdjustStockRequest.LocationId), nameof(AdjustStockRequest.ClearLocation));
    }

    [Fact]
    public void Request_WithOnlyClearFlags_IsValid()
    {
        Validate(new AdjustStockRequest { Amount = 1, ClearBestBeforeDate = true, ClearLocation = true })
            .Should().BeEmpty();
    }

    [Fact]
    public void Request_WithoutClearFlags_IsValid()
    {
        Validate(new AdjustStockRequest { Amount = 1, BestBeforeDate = DateTime.UtcNow, LocationId = Guid.NewGuid() })
            .Should().BeEmpty();
    }

    #endregion

    public void Dispose() => _context.Dispose();
}
