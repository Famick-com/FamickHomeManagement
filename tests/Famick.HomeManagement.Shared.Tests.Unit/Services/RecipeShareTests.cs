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
/// Recipe share links (#194): the link a household hands out opens the readable share page.
/// </summary>
public class RecipeShareTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly HomeManagementDbContext _context;
    private readonly RecipeService _service;

    public RecipeShareTests()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        _context = new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantProvider.Object);
        _service = new RecipeService(_context, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(),
            NullLogger<RecipeService>.Instance);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task A_share_link_opens_the_share_page_not_the_api()
    {
        var recipeId = await SeedRecipeAsync("Roux");

        var share = await _service.GenerateShareTokenAsync(recipeId);

        share.ShareUrl.Should().Be($"/recipes/share/{share.Token}");
    }

    [Fact]
    public async Task The_page_still_reads_the_recipe_by_its_token()
    {
        var recipeId = await SeedRecipeAsync("Roux");
        var share = await _service.GenerateShareTokenAsync(recipeId);

        var shared = await _service.GetByShareTokenAsync(share.Token);

        shared!.Name.Should().Be("Roux");
    }

    private async Task<Guid> SeedRecipeAsync(string name)
    {
        var recipe = new Recipe { Id = Guid.NewGuid(), TenantId = _tenantId, Name = name };
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();
        return recipe.Id;
    }
}
