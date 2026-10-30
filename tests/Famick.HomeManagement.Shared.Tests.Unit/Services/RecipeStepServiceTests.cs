using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// A step can be saved with no instructions. JSON null binds to the non-nullable request
/// property, so the service has to keep it out of the NOT NULL column.
/// </summary>
public class RecipeStepServiceTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly HomeManagementDbContext _context;
    private readonly RecipeService _service;

    public RecipeStepServiceTests()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        _context = new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            tenantProvider.Object);
        _service = new RecipeService(_context, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(),
            NullLogger<RecipeService>.Instance);
    }

    public void Dispose() => _context.Dispose();

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task A_step_can_be_added_without_instructions(string? instructions)
    {
        var recipeId = await SeedRecipeAsync();

        var step = await _service.AddStepAsync(recipeId, new CreateRecipeStepRequest { Instructions = instructions! });

        step.Instructions.Should().BeEmpty();
        (await _context.Set<RecipeStep>().SingleAsync()).Instructions.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task A_step_can_be_updated_to_have_no_instructions(string? instructions)
    {
        var recipeId = await SeedRecipeAsync();
        var added = await _service.AddStepAsync(recipeId, new CreateRecipeStepRequest { Instructions = "Preheat the oven" });

        var step = await _service.UpdateStepAsync(recipeId, added.Id,
            new UpdateRecipeStepRequest { Title = "Gather", Instructions = instructions! });

        step.Title.Should().Be("Gather");
        step.Instructions.Should().BeEmpty();
    }

    private async Task<Guid> SeedRecipeAsync()
    {
        var recipe = new Recipe { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Pancakes" };
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();
        return recipe.Id;
    }
}
