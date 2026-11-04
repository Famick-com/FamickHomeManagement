using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Core.Validators.Recipes;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Data.Configurations;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Rating recipes (#159): 1 to 5 stars in half stars, one rating per household member, with the
/// household's average beside each member's own.
/// </summary>
public class RecipeRatingTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly string _database = Guid.NewGuid().ToString();
    private readonly ITenantProvider _tenantProvider;
    private readonly HomeManagementDbContext _context;
    private readonly RecipeService _service;

    public RecipeRatingTests()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        _tenantProvider = tenantProvider.Object;
        _context = NewContext();
        _service = new RecipeService(_context, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(),
            NullLogger<RecipeService>.Instance);

        _context.Users.AddRange(
            new User { Id = _alice, TenantId = _tenantId, Email = "alice@example.com", Username = "alice" },
            new User { Id = _bob, TenantId = _tenantId, Email = "bob@example.com", Username = "bob" });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(4.5)]
    [InlineData(5.0)]
    public void Whole_and_half_stars_from_one_to_five_are_accepted(decimal rating)
    {
        new SetRecipeRatingRequestValidator().Validate(new SetRecipeRatingRequest { Rating = rating })
            .IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(5.5)]
    [InlineData(3.3)]
    [InlineData(-1.0)]
    public void Anything_else_is_rejected(decimal rating)
    {
        new SetRecipeRatingRequestValidator().Validate(new SetRecipeRatingRequest { Rating = rating })
            .IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(3.5, true)]
    [InlineData(3.3, false)]
    [InlineData(0.5, false)]
    public void A_minimum_rating_filter_takes_the_same_steps(decimal minRating, bool valid)
    {
        new RecipeFilterRequestValidator().Validate(new RecipeFilterRequest { MinRating = minRating })
            .IsValid.Should().Be(valid);
    }

    [Fact]
    public void Ratings_are_stored_in_half_stars()
    {
        RecipeRatings.ToHalfStars(4.5m).Should().Be(9);
        RecipeRatings.ToHalfStars(1m).Should().Be(2);
        RecipeRatings.ToStars(9).Should().Be(4.5m);
    }

    [Fact]
    public void The_average_is_rounded_to_one_decimal_place()
    {
        // 4.5 and 4 stars: 4.25, shown as 4.3.
        RecipeRatings.Average([9, 8]).Should().Be(4.3m);
        RecipeRatings.Average([10, 10, 9]).Should().Be(4.8m);
        RecipeRatings.Average([]).Should().BeNull();
    }

    [Fact]
    public async Task Rating_again_replaces_the_members_rating_rather_than_adding_one()
    {
        var recipeId = await SeedRecipeAsync("Roux");

        await _service.SetRatingAsync(recipeId, _alice, 3m);
        var ratings = await _service.SetRatingAsync(recipeId, _alice, 4.5m);

        (await _context.RecipeRatings.CountAsync()).Should().Be(1);
        ratings.MyRating.Should().Be(4.5m);
        ratings.AverageRating.Should().Be(4.5m);
        ratings.RatingCount.Should().Be(1);
    }

    [Fact]
    public async Task Two_members_ratings_are_kept_apart_and_both_count_in_the_average()
    {
        var recipeId = await SeedRecipeAsync("Roux");

        await _service.SetRatingAsync(recipeId, _alice, 5m);
        await _service.SetRatingAsync(recipeId, _bob, 3.5m);

        var forAlice = await _service.GetByIdAsync(recipeId, _alice);
        var forBob = await _service.GetByIdAsync(recipeId, _bob);

        forAlice!.MyRating.Should().Be(5m);
        forBob!.MyRating.Should().Be(3.5m);
        forAlice.AverageRating.Should().Be(4.3m, "5 and 3.5 average 4.25");
        forAlice.RatingCount.Should().Be(2);
        forBob.AverageRating.Should().Be(forAlice.AverageRating);
    }

    [Fact]
    public async Task Clearing_removes_only_the_callers_rating()
    {
        var recipeId = await SeedRecipeAsync("Roux");
        await _service.SetRatingAsync(recipeId, _alice, 5m);
        await _service.SetRatingAsync(recipeId, _bob, 3m);

        var ratings = await _service.ClearRatingAsync(recipeId, _alice);

        ratings.MyRating.Should().BeNull();
        ratings.AverageRating.Should().Be(3m);
        ratings.RatingCount.Should().Be(1);
        (await _context.RecipeRatings.SingleAsync()).UserId.Should().Be(_bob);
    }

    [Fact]
    public async Task Clearing_a_rating_that_isnt_there_is_not_an_error()
    {
        var recipeId = await SeedRecipeAsync("Roux");

        var ratings = await _service.ClearRatingAsync(recipeId, _alice);

        ratings.RatingCount.Should().Be(0);
        ratings.AverageRating.Should().BeNull();
    }

    [Fact]
    public async Task An_unknown_recipe_cannot_be_rated()
    {
        var rate = () => _service.SetRatingAsync(Guid.NewGuid(), _alice, 4m);

        await rate.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task An_unrated_recipe_has_no_average()
    {
        var recipeId = await SeedRecipeAsync("Roux");

        var recipe = await _service.GetByIdAsync(recipeId, _alice);

        recipe!.AverageRating.Should().BeNull();
        recipe.RatingCount.Should().Be(0);
        recipe.MyRating.Should().BeNull();
    }

    [Fact]
    public async Task The_list_shows_each_recipes_average_and_the_callers_own()
    {
        var recipeId = await SeedRecipeAsync("Roux");
        await _service.SetRatingAsync(recipeId, _alice, 4m);
        await _service.SetRatingAsync(recipeId, _bob, 5m);

        var row = (await _service.ListAsync(null, _bob)).Single();

        row.AverageRating.Should().Be(4.5m);
        row.RatingCount.Should().Be(2);
        row.MyRating.Should().Be(5m);
    }

    [Fact]
    public async Task Sorting_by_average_puts_unrated_recipes_last_either_way()
    {
        var good = await SeedRecipeAsync("Good");
        var best = await SeedRecipeAsync("Best");
        await SeedRecipeAsync("Unrated");
        await _service.SetRatingAsync(good, _alice, 3m);
        await _service.SetRatingAsync(best, _alice, 5m);

        var highestFirst = await _service.ListAsync(new RecipeFilterRequest { SortBy = "AverageRating", Descending = true }, _alice);
        var lowestFirst = await _service.ListAsync(new RecipeFilterRequest { SortBy = "AverageRating" }, _alice);

        highestFirst.Select(r => r.Name).Should().Equal("Best", "Good", "Unrated");
        lowestFirst.Select(r => r.Name).Should().Equal("Good", "Best", "Unrated");
    }

    [Fact]
    public async Task Sorting_by_my_rating_uses_only_the_callers_ratings()
    {
        var aliceLikes = await SeedRecipeAsync("Alice likes");
        var bobLikes = await SeedRecipeAsync("Bob likes");
        await _service.SetRatingAsync(aliceLikes, _alice, 5m);
        await _service.SetRatingAsync(aliceLikes, _bob, 1m);
        await _service.SetRatingAsync(bobLikes, _bob, 5m);

        var forAlice = await _service.ListAsync(new RecipeFilterRequest { SortBy = "MyRating", Descending = true }, _alice);

        forAlice.Select(r => r.Name).Should().Equal("Alice likes", "Bob likes");
        forAlice.Select(r => r.MyRating).Should().Equal(5m, null);
    }

    [Fact]
    public async Task A_minimum_rating_leaves_out_lower_and_unrated_recipes()
    {
        var four = await SeedRecipeAsync("Four");
        var threeAndAHalf = await SeedRecipeAsync("Three and a half");
        await SeedRecipeAsync("Unrated");
        await _service.SetRatingAsync(four, _alice, 4m);
        await _service.SetRatingAsync(four, _bob, 4m);
        await _service.SetRatingAsync(threeAndAHalf, _alice, 3.5m);

        var atLeastFour = await _service.ListAsync(new RecipeFilterRequest { MinRating = 4m }, _alice);
        var atLeastThree = await _service.ListAsync(new RecipeFilterRequest { MinRating = 3m }, _alice);

        atLeastFour.Select(r => r.Name).Should().Equal("Four");
        atLeastThree.Select(r => r.Name).Should().Equal("Four", "Three and a half");
    }

    [Theory]
    [InlineData(9, 1, true)]   // nine 4s and a 3.5: 3.95, shown as 4.0
    [InlineData(4, 4, false)]  // four 4s and four 3.5s: 3.75, shown as 3.8
    public async Task A_minimum_rating_is_met_by_an_average_shown_as_it(int fours, int threeAndAHalves, bool kept)
    {
        var recipeId = await SeedRecipeAsync("Roux");
        for (var i = 0; i < fours; i++)
            await AddDepartedRatingAsync(recipeId, 8);
        for (var i = 0; i < threeAndAHalves; i++)
            await AddDepartedRatingAsync(recipeId, 7);

        var recipes = await _service.ListAsync(new RecipeFilterRequest { MinRating = 4m }, _alice);

        recipes.Should().HaveCount(kept ? 1 : 0);
    }

    [Fact]
    public async Task A_shared_link_shows_no_ratings()
    {
        var recipeId = await SeedRecipeAsync("Roux");
        await _service.SetRatingAsync(recipeId, _alice, 5m);
        var share = await _service.GenerateShareTokenAsync(recipeId);

        var shared = await _service.GetByShareTokenAsync(share.Token);

        shared!.AverageRating.Should().BeNull();
        shared.RatingCount.Should().Be(0);
        shared.MyRating.Should().BeNull();
    }

    [Fact]
    public async Task A_member_who_leaves_keeps_their_rating_in_the_average_without_their_name()
    {
        var recipeId = await SeedRecipeAsync("Roux");
        await _service.SetRatingAsync(recipeId, _alice, 5m);
        await _service.SetRatingAsync(recipeId, _bob, 3m);

        _context.Users.Remove(await _context.Users.SingleAsync(u => u.Id == _bob));
        await _context.SaveChangesAsync();

        var recipe = await _service.GetByIdAsync(recipeId, _alice);
        recipe!.RatingCount.Should().Be(2);
        recipe.AverageRating.Should().Be(4m);
        (await _context.RecipeRatings.CountAsync(r => r.UserId == null)).Should().Be(1);
    }

    [Fact]
    public async Task Deleting_a_recipe_deletes_its_ratings()
    {
        var recipeId = await SeedRecipeAsync("Roux");
        await _service.SetRatingAsync(recipeId, _alice, 5m);

        await _service.DeleteAsync(recipeId);

        (await _context.RecipeRatings.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task A_rating_the_members_other_request_saved_first_is_replaced_rather_than_failing()
    {
        var recipeId = await SeedRecipeAsync("Roux");
        var race = new OtherRequestSavesFirst(() => NewContext(), recipeId, _alice, halfStars: 6);
        using var racing = NewContext(race);
        var service = new RecipeService(racing, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(),
            NullLogger<RecipeService>.Instance);

        var ratings = await service.SetRatingAsync(recipeId, _alice, 4.5m);

        race.Raced.Should().BeTrue();
        ratings.MyRating.Should().Be(4.5m);
        ratings.RatingCount.Should().Be(1);
        using var check = NewContext();
        (await check.RecipeRatings.SingleAsync()).HalfStars.Should().Be(9);
    }

    private HomeManagementDbContext NewContext(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(_database)
            .AddInterceptors(interceptors)
            .Options,
        _tenantProvider);

    /// <summary>
    /// The member's other request inserts its rating between this request's check and its save, which
    /// then fails on the unique index as Postgres would fail it.
    /// </summary>
    private sealed class OtherRequestSavesFirst(
        Func<HomeManagementDbContext> otherRequest, Guid recipeId, Guid userId, short halfStars) : SaveChangesInterceptor
    {
        public bool Raced { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var adding = eventData.Context!.ChangeTracker.Entries<RecipeRating>().Any(e => e.State == EntityState.Added);
            if (Raced || !adding)
                return result;

            Raced = true;
            using (var other = otherRequest())
            {
                other.RecipeRatings.Add(new RecipeRating
                {
                    Id = Guid.NewGuid(), TenantId = eventData.Context.ChangeTracker.Entries<RecipeRating>().First().Entity.TenantId,
                    RecipeId = recipeId, UserId = userId, HalfStars = halfStars
                });
                await other.SaveChangesAsync(cancellationToken);
            }

            throw new DbUpdateException("duplicate key value violates unique constraint",
                new PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR",
                    PostgresErrorCodes.UniqueViolation, constraintName: RecipeRatingConfiguration.UniqueIndexName));
        }
    }

    private async Task<Guid> SeedRecipeAsync(string name)
    {
        var recipe = new Recipe { Id = Guid.NewGuid(), TenantId = _tenantId, Name = name };
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();
        return recipe.Id;
    }

    /// <summary>A rating whose member has since left, so it has no user: any number can share a recipe.</summary>
    private async Task AddDepartedRatingAsync(Guid recipeId, short halfStars)
    {
        _context.RecipeRatings.Add(new RecipeRating
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, RecipeId = recipeId, UserId = null, HalfStars = halfStars
        });
        await _context.SaveChangesAsync();
    }
}
