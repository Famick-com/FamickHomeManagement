using System.Text;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Net;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.RecipeImport;

/// <summary>
/// Recipe imports run as jobs: started at once, checked on, and cancelled with nothing left behind.
/// </summary>
public class RecipeImportJobTests
{
    private const string PageUrl = "https://kitchen.example/lemon-chicken/";

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InMemoryDatabaseRoot _root = new();
    private readonly string _database = Guid.NewGuid().ToString();
    private readonly FakeFetcher _fetcher = new();
    private readonly FakeRunner _runner = new();

    public RecipeImportJobTests()
    {
        _fetcher.Pages[PageUrl] = Encoding.UTF8.GetBytes(RecipePageFixtures.WordPressGraph);

        using var context = Context(_tenantId);
        context.Locations.Add(new Location { Id = Guid.NewGuid(), TenantId = _tenantId, Name = "Pantry", IsActive = true });
        context.SaveChanges();
    }

    [Fact]
    public async Task Starting_an_import_saves_a_running_job_and_hands_it_to_the_runner()
    {
        var job = await Jobs(Context(_tenantId)).StartAsync($" {PageUrl} ", false, _userId);

        job.Status.Should().Be(RecipeImportJobStatus.Running);
        _runner.Started.Should().Equal((job.JobId, _tenantId, (Guid?)_userId));
        (await Context(_tenantId).RecipeImportJobs.SingleAsync()).Url.Should().Be(PageUrl);
    }

    [Fact]
    public async Task A_job_that_runs_saves_its_recipe_and_reports_it_with_each_line()
    {
        var job = await Jobs(Context(_tenantId)).StartAsync(PageUrl, false, _userId);

        await Importer(Context(_tenantId)).RunJobAsync(job.JobId);

        var done = await Jobs(Context(_tenantId)).GetAsync(job.JobId, _userId);
        done!.Status.Should().Be(RecipeImportJobStatus.Succeeded);
        done.Result!.Recipe.Name.Should().Be("Weeknight Lemon Chicken & Rice");
        done.Result.Ingredients.Should().HaveCount(6);
        _fetcher.Timeouts.First().Should().Be(RecipeImportService.PageTimeout, "a recipe page may be slow");
    }

    [Fact]
    public async Task A_failed_import_is_recorded_on_its_job()
    {
        _fetcher.Pages[PageUrl] = Encoding.UTF8.GetBytes(RecipePageFixtures.NoRecipe);
        var job = await Jobs(Context(_tenantId)).StartAsync(PageUrl, false, _userId);

        await Importer(Context(_tenantId)).RunJobAsync(job.JobId);

        var failed = await Jobs(Context(_tenantId)).GetAsync(job.JobId, _userId);
        failed!.Status.Should().Be(RecipeImportJobStatus.Failed);
        failed.ErrorCode.Should().Be(RecipeImportException.NoRecipeFound);
    }

    [Fact]
    public async Task An_import_of_a_page_already_imported_fails_naming_that_recipe()
    {
        var first = await Jobs(Context(_tenantId)).StartAsync(PageUrl, false, _userId);
        await Importer(Context(_tenantId)).RunJobAsync(first.JobId);
        var recipeId = (await Jobs(Context(_tenantId)).GetAsync(first.JobId, _userId))!.Result!.Recipe.Id;

        var second = await Jobs(Context(_tenantId)).StartAsync(PageUrl, false, _userId);
        await Importer(Context(_tenantId)).RunJobAsync(second.JobId);

        var failed = await Jobs(Context(_tenantId)).GetAsync(second.JobId, _userId);
        failed!.ErrorCode.Should().Be(RecipeAlreadyImportedException.Code);
        failed.ExistingRecipeId.Should().Be(recipeId);
    }

    [Fact]
    public async Task A_job_cancelled_while_it_runs_saves_no_recipe()
    {
        var job = await Jobs(Context(_tenantId)).StartAsync(PageUrl, false, _userId);

        // Cancelled from another request, after the import has read the job and while it fetches.
        _fetcher.OnFetch = () => Jobs(Context(_tenantId)).CancelAsync(job.JobId).GetAwaiter().GetResult();
        await Importer(Context(_tenantId)).RunJobAsync(job.JobId);

        (await Context(_tenantId).Recipes.CountAsync()).Should().Be(0);
        (await Jobs(Context(_tenantId)).GetAsync(job.JobId, _userId))!.Status.Should().Be(RecipeImportJobStatus.Cancelled);
        _runner.Cancelled.Should().Equal(job.JobId);
    }

    [Fact]
    public async Task A_cancel_after_the_recipe_was_saved_leaves_the_recipe()
    {
        var job = await Jobs(Context(_tenantId)).StartAsync(PageUrl, false, _userId);
        await Importer(Context(_tenantId)).RunJobAsync(job.JobId);

        var cancelled = await Jobs(Context(_tenantId)).CancelAsync(job.JobId);

        cancelled!.Status.Should().Be(RecipeImportJobStatus.Succeeded);
        (await Context(_tenantId).Recipes.CountAsync()).Should().Be(1);
        _runner.Cancelled.Should().BeEmpty();
    }

    [Fact]
    public async Task A_job_out_of_time_fails_as_timed_out_and_saves_nothing()
    {
        var job = await Jobs(Context(_tenantId)).StartAsync(PageUrl, false, _userId);

        await Importer(Context(_tenantId)).RunJobAsync(job.JobId, new CancellationToken(canceled: true));

        (await Context(_tenantId).Recipes.CountAsync()).Should().Be(0);
        var failed = await Jobs(Context(_tenantId)).GetAsync(job.JobId, _userId);
        failed!.ErrorCode.Should().Be(RecipeImportException.PageTimedOut);
    }

    [Fact]
    public async Task A_job_still_running_long_after_it_could_reads_as_interrupted()
    {
        var job = await Jobs(Context(_tenantId)).StartAsync(PageUrl, false, _userId);
        await Backdate(job.JobId, RecipeImportJobService.StaleAfter + TimeSpan.FromSeconds(1));

        var read = await Jobs(Context(_tenantId)).GetAsync(job.JobId, _userId);

        read!.Status.Should().Be(RecipeImportJobStatus.Failed);
        read.ErrorCode.Should().Be(RecipeImportException.ImportInterrupted);
    }

    [Fact]
    public async Task Another_household_can_neither_see_nor_cancel_a_job()
    {
        var job = await Jobs(Context(_tenantId)).StartAsync(PageUrl, false, _userId);
        var other = Guid.NewGuid();

        (await Jobs(Context(other)).GetAsync(job.JobId, null)).Should().BeNull();
        (await Jobs(Context(other)).CancelAsync(job.JobId)).Should().BeNull();
        (await Context(_tenantId).RecipeImportJobs.SingleAsync()).Status.Should().Be(RecipeImportJobStatus.Running);
    }

    [Fact]
    public async Task Jobs_older_than_a_day_are_cleared_when_another_starts()
    {
        var old = await Jobs(Context(_tenantId)).StartAsync(PageUrl, false, _userId);
        await Backdate(old.JobId, RecipeImportJobService.KeepFor + TimeSpan.FromMinutes(1));

        var fresh = await Jobs(Context(_tenantId)).StartAsync(PageUrl, false, _userId);

        (await Context(_tenantId).RecipeImportJobs.Select(j => j.Id).ToListAsync()).Should().Equal(fresh.JobId);
    }

    [Fact]
    public async Task The_runner_imports_on_its_own_scope_as_the_household_that_asked()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ITenantProvider, SettableTenantProvider>();
        services.AddScoped(sp => new HomeManagementDbContext(Options(), sp.GetRequiredService<ITenantProvider>()));
        services.AddScoped<IRecipeImportService>(sp => Importer(sp.GetRequiredService<HomeManagementDbContext>()));
        var lifetime = new Mock<IHostApplicationLifetime>();
        lifetime.Setup(l => l.ApplicationStopping).Returns(CancellationToken.None);
        var runner = new RecipeImportJobRunner(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            lifetime.Object, NullLogger<RecipeImportJobRunner>.Instance);
        var job = await Jobs(Context(_tenantId)).StartAsync(PageUrl, false, _userId);

        runner.Start(job.JobId, _tenantId, _userId);

        var status = RecipeImportJobStatus.Running;
        for (var i = 0; i < 100 && status == RecipeImportJobStatus.Running; i++)
        {
            await Task.Delay(50);
            status = (await Context(_tenantId).RecipeImportJobs.AsNoTracking().SingleAsync()).Status;
        }

        status.Should().Be(RecipeImportJobStatus.Succeeded);
        (await Context(_tenantId).Recipes.SingleAsync()).TenantId.Should().Be(_tenantId);
    }

    private async Task Backdate(Guid jobId, TimeSpan age)
    {
        using var context = Context(_tenantId);
        var job = await context.RecipeImportJobs.SingleAsync(j => j.Id == jobId);
        job.CreatedAt = DateTime.UtcNow - age;
        await context.SaveChangesAsync();
    }

    private DbContextOptions<HomeManagementDbContext> Options() =>
        new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(_database, _root).Options;

    // A context per request, as in the app, all on the same database.
    private HomeManagementDbContext Context(Guid tenantId)
    {
        var tenant = new SettableTenantProvider();
        tenant.SetTenantId(tenantId);
        return new HomeManagementDbContext(Options(), tenant);
    }

    private RecipeImportJobService Jobs(HomeManagementDbContext context) =>
        new(context, new SettableTenantProvider(_tenantId), _runner, Recipes(context), NullLogger<RecipeImportJobService>.Instance);

    private RecipeImportService Importer(HomeManagementDbContext context)
    {
        var products = new ProductsService(context, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(), _fetcher,
            Mock.Of<IMasterProductImageResolver>(), Mock.Of<IProductSearchService>(), new SettableTenantProvider(_tenantId),
            NullLogger<ProductsService>.Instance);
        return new RecipeImportService(context, _fetcher, products, Recipes(context), NullLogger<RecipeImportService>.Instance);
    }

    private static RecipeService Recipes(HomeManagementDbContext context) =>
        new(context, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(), NullLogger<RecipeService>.Instance);

    private sealed class SettableTenantProvider(Guid? tenantId = null) : ITenantProvider
    {
        public Guid? TenantId { get; private set; } = tenantId;
        public Guid? UserId { get; private set; }
        public void SetTenantId(Guid id) => TenantId = id;
        public void SetUserId(Guid id) => UserId = id;
        public void ClearTenantId() => TenantId = null;
        public void ClearUserId() => UserId = null;
    }

    private sealed class FakeRunner : IRecipeImportJobRunner
    {
        public List<(Guid JobId, Guid TenantId, Guid? UserId)> Started { get; } = new();
        public List<Guid> Cancelled { get; } = new();

        public void Start(Guid jobId, Guid tenantId, Guid? userId) => Started.Add((jobId, tenantId, userId));
        public void Cancel(Guid jobId) => Cancelled.Add(jobId);
    }

    /// <summary>Serves pages by URL, with no photos; anything else is a 404.</summary>
    private sealed class FakeFetcher : IOutboundHttpFetcher
    {
        private readonly OutboundHttpFetcher _urlRules = new(
            null!, new OutboundFetchOptions(), allowPrivateNetworks: false, NullLogger<OutboundHttpFetcher>.Instance);

        public Dictionary<string, byte[]> Pages { get; } = new();
        public List<TimeSpan?> Timeouts { get; } = new();
        public Action? OnFetch { get; set; }

        public OutboundFetchFailure? CheckUrl(Uri url) => _urlRules.CheckUrl(url);

        public Task<OutboundFetchResult> GetAsync(Uri url, long maxBytes, IReadOnlyDictionary<string, string>? headers = null,
            TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Timeouts.Add(timeout);
            OnFetch?.Invoke();
            return Pages.TryGetValue(url.AbsoluteUri, out var page)
                ? Task.FromResult(new OutboundFetchResult(page, "text/html", url))
                : throw new OutboundFetchException(OutboundFetchFailure.HttpError, 404);
        }
    }
}
