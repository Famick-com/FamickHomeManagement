using System.Text.Json;
using System.Text.RegularExpressions;
using Famick.HomeManagement.Core.DTOs.Recipes;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Helpers;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Imports a recipe from a web page: fetched through the guarded outbound fetcher, read from its
/// schema.org data by <see cref="RecipePageParser"/>, and saved with each ingredient line matched to a
/// product by the <see cref="IngredientMatcher"/> where one matches exactly.
/// </summary>
public partial class RecipeImportService : IRecipeImportService
{
    /// <summary>The largest page the importer reads. Recipe pages with all their markup run well under this.</summary>
    public const long MaxPageBytes = 3 * 1024 * 1024;

    /// <summary>
    /// How long a recipe page may take. Longer than the fetcher's own budget: some recipe sites take
    /// over ten seconds to send a page even to a browser, and the import runs in the background.
    /// </summary>
    public static readonly TimeSpan PageTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The largest photo the importer saves, as for a product image from a URL.</summary>
    public const long MaxImageBytes = ProductsService.MaxImageFromUrlBytes;

    public const string ImageExternalSource = "import";

    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    /// <summary>
    /// Many recipe sites refuse a request that doesn't look like a browser's, so the page is fetched with
    /// a browser's User-Agent.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> PageHeaders = new Dictionary<string, string>
    {
        ["User-Agent"] = BrowserUserAgent,
        ["Accept"] = "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8",
        ["Accept-Language"] = "en-US,en;q=0.9"
    };

    /// <summary>
    /// The photo is asked for only in the formats it can be stored as: an image server picks AVIF for a
    /// browser that accepts it, and an AVIF photo would be dropped.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ImageHeaders = new Dictionary<string, string>
    {
        ["User-Agent"] = BrowserUserAgent,
        ["Accept"] = "image/webp,image/png,image/jpeg;q=0.9,*/*;q=0.5"
    };

    private readonly HomeManagementDbContext _context;
    private readonly IOutboundHttpFetcher _fetcher;
    private readonly IngredientMatcher _matcher;
    private readonly IRecipeService _recipeService;
    private readonly ILogger<RecipeImportService> _logger;
    private readonly IHostApplicationLifetime? _lifetime;

    public RecipeImportService(
        HomeManagementDbContext context,
        IOutboundHttpFetcher fetcher,
        IngredientMatcher matcher,
        IRecipeService recipeService,
        ILogger<RecipeImportService> logger,
        IHostApplicationLifetime? lifetime = null)
    {
        _context = context;
        _fetcher = fetcher;
        _matcher = matcher;
        _recipeService = recipeService;
        _logger = logger;
        _lifetime = lifetime;
    }

    public Task<RecipeImportResultDto> ImportAsync(string url, bool allowDuplicate, Guid? userId, CancellationToken ct = default) =>
        ImportAsync(url, allowDuplicate, userId, job: null, ct);

    public async Task RunJobAsync(Guid jobId, CancellationToken ct = default)
    {
        // Read whatever the token says, so a job already out of time is still marked so below.
        var job = await _context.RecipeImportJobs.FirstOrDefaultAsync(j => j.Id == jobId, CancellationToken.None);
        if (job is not { Status: RecipeImportJobStatus.Running })
            return;

        try
        {
            await ImportAsync(job.Url, job.AllowDuplicate, job.UserId, job, ct);
        }
        catch (RecipeAlreadyImportedException ex)
        {
            await FailJobAsync(jobId, RecipeAlreadyImportedException.Code, ex.Message, ex.ExistingRecipeId);
        }
        catch (RecipeImportException ex)
        {
            await FailJobAsync(jobId, ex.Code, ex.Message);
        }
        catch (OperationCanceledException)
        {
            // Cancelled, out of time, or the server stopping. A cancel has already marked the job, which
            // this leaves alone.
            if (_lifetime?.ApplicationStopping.IsCancellationRequested == true)
                await FailJobAsync(jobId, RecipeImportException.ImportInterrupted, "The import stopped before it finished");
            else
                await FailJobAsync(jobId, RecipeImportException.PageTimedOut, "That site didn't respond");
        }
        catch (DbUpdateConcurrencyException)
        {
            // The job changed under the save, as a cancel changes it; a cancelled job stays so.
            await FailJobAsync(jobId, RecipeImportException.ImportFailed, "Couldn't import the recipe");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Recipe import job {JobId} failed", jobId);
            await FailJobAsync(jobId, RecipeImportException.ImportFailed, "Couldn't import the recipe");
        }
    }

    /// <summary>Marks a job failed, unless it has already ended: a cancelled or finished job stays as it is.</summary>
    private async Task FailJobAsync(Guid jobId, string code, string message, Guid? existingRecipeId = null)
    {
        // Whatever the import had added and not saved goes; only the job is saved.
        _context.ChangeTracker.Clear();

        var job = await _context.RecipeImportJobs.FirstOrDefaultAsync(j => j.Id == jobId, CancellationToken.None);
        if (job is not { Status: RecipeImportJobStatus.Running })
            return;

        job.Status = RecipeImportJobStatus.Failed;
        job.Version = Guid.NewGuid();
        job.ErrorCode = code;
        job.ErrorMessage = message;
        job.ExistingRecipeId = existingRecipeId;
        job.CompletedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Cancelled meanwhile, which stands.
        }
    }

    /// <param name="job">
    /// The job this import runs for, tracked by this service's context. The recipe is saved with the job
    /// marked done, and only while the job is as it was read, so a cancel in between saves neither.
    /// </param>
    private async Task<RecipeImportResultDto> ImportAsync(string url, bool allowDuplicate, Guid? userId, RecipeImportJob? job, CancellationToken ct)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var pageUrl))
            throw new RecipeImportException(RecipeImportException.UrlNotAllowed, "Enter the web address of a recipe page");

        // Before fetching anything: the same link pasted twice is caught without a request.
        if (!allowDuplicate)
            await ThrowIfImportedAsync([pageUrl.AbsoluteUri], ct);

        var page = await FetchPageAsync(pageUrl, ct);
        var imported = RecipePageParser.Parse(page.Body, page.FinalUrl)
            ?? throw new RecipeImportException(RecipeImportException.NoRecipeFound, "No recipe found on that page");

        if (!allowDuplicate)
            await ThrowIfImportedAsync([imported.Source, page.FinalUrl.AbsoluteUri], ct);

        var lines = await _matcher.MatchLinesAsync(imported.Ingredients, ct);
        var recipe = BuildRecipe(imported, lines);
        var outcomes = lines.Select(l => new ImportedIngredientDto
        {
            Text = l.Text,
            Outcome = l.Outcome,
            ProductName = l.ProductName
        }).ToList();

        _context.Recipes.Add(recipe);
        if (job != null)
        {
            // Cancelled while it ran: stop here. A cancel from now until the save is caught by the version.
            if (await _context.RecipeImportJobs.AsNoTracking()
                    .AnyAsync(j => j.Id == job.Id && j.Status != RecipeImportJobStatus.Running, ct))
                throw new OperationCanceledException("The import was cancelled");

            job.Status = RecipeImportJobStatus.Succeeded;
            job.Version = Guid.NewGuid();
            job.RecipeId = recipe.Id;
            job.IngredientsJson = JsonSerializer.Serialize(outcomes);
            job.CompletedAt = DateTime.UtcNow;
        }

        // A job cancelled since it was read fails the concurrency check here, and the recipe isn't saved.
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Imported recipe {RecipeId} from {Host} with {Linked} of {Total} ingredients linked",
            recipe.Id, page.FinalUrl.Host, lines.Count(l => l.Outcome != ImportedIngredientOutcome.Unlinked), lines.Count);

        if (imported.ImageUrl != null)
            await AddPhotoAsync(recipe.Id, imported.ImageUrl, ct);

        var dto = await _recipeService.GetByIdAsync(recipe.Id, userId, ct)
            ?? throw new InvalidOperationException($"Imported recipe {recipe.Id} not found");

        return new RecipeImportResultDto { Recipe = dto, Ingredients = outcomes };
    }

    private async Task ThrowIfImportedAsync(IEnumerable<string> sources, CancellationToken ct)
    {
        var candidates = sources.Distinct().ToList();
        var existing = await _context.Recipes
            .Where(r => r.Source != null && candidates.Contains(r.Source))
            .OrderBy(r => r.CreatedAt)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        if (existing is { } id)
            throw new RecipeAlreadyImportedException(id);
    }

    private async Task<OutboundFetchResult> FetchPageAsync(Uri pageUrl, CancellationToken ct)
    {
        if (_fetcher.CheckUrl(pageUrl) != null)
            throw new RecipeImportException(RecipeImportException.UrlNotAllowed, "That address can't be imported from");

        OutboundFetchResult page;
        try
        {
            page = await _fetcher.GetAsync(pageUrl, MaxPageBytes, PageHeaders, PageTimeout, ct);
        }
        catch (OutboundFetchException ex)
        {
            _logger.LogInformation("Recipe import fetch from {Host} failed: {Failure} {Status}",
                pageUrl.Host, ex.Failure, ex.StatusCode);

            throw ex.Failure switch
            {
                OutboundFetchFailure.UrlNotAllowed or OutboundFetchFailure.AddressNotAllowed =>
                    new RecipeImportException(RecipeImportException.UrlNotAllowed, "That address can't be imported from"),
                OutboundFetchFailure.TooLarge =>
                    new RecipeImportException(RecipeImportException.PageTooLarge, "That page is too large to import"),
                // Some sites never answer a request that isn't from a browser; the address is fine.
                OutboundFetchFailure.TimedOut =>
                    new RecipeImportException(RecipeImportException.PageTimedOut, "That site didn't respond"),
                _ => new RecipeImportException(RecipeImportException.PageUnreachable, "Couldn't open that page")
            };
        }

        // A missing Content-Type is read as HTML; a declared one must say so.
        var mediaType = page.ContentType?.Split(';')[0].Trim();
        if (!string.IsNullOrEmpty(mediaType)
            && !mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase)
            && !mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
            throw new RecipeImportException(RecipeImportException.NotAWebPage, "That address isn't a web page");

        return page;
    }

    private static Recipe BuildRecipe(ImportedRecipe imported, List<MatchedIngredientLine> lines)
    {
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Name = imported.Name,
            Servings = imported.Servings,
            Notes = imported.Description,
            Source = imported.Source,
            Attribution = imported.Attribution
        };

        var steps = imported.Steps.Count > 0
            ? imported.Steps
            : [new ImportedRecipeStep(null, string.Empty)];

        for (var i = 0; i < steps.Count; i++)
        {
            recipe.Steps.Add(new RecipeStep
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                StepOrder = i + 1,
                Title = steps[i].Title,
                Instructions = steps[i].Instructions
            });
        }

        var sharesOf = StepsUsing(steps, lines.Select(l => l.Parsed).ToList());
        var recipeSteps = recipe.Steps.ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var linked = line.ProductId.HasValue;

            // A line kept as text has no amount to share, so it goes whole on the first of its steps.
            var shares = linked ? sharesOf[i] : [sharesOf[i][0] with { Amount = null }];
            foreach (var share in shares)
            {
                var step = recipeSteps[share.Step];
                step.Ingredients.Add(new RecipePosition
                {
                    Id = Guid.NewGuid(),
                    RecipeStepId = step.Id,
                    ProductId = line.ProductId,
                    Amount = linked ? share.Amount ?? line.Parsed.Amount!.Value : 0,
                    QuantityUnitId = linked ? line.QuantityUnitId : null,
                    Note = linked ? Truncate(line.Parsed.Note, 500) : null,
                    OriginalText = line.Text,
                    SortOrder = i
                });
            }
        }

        return recipe;
    }

    /// <summary>
    /// A step an ingredient line goes on, and how much of the line's amount: null for all of it.
    /// </summary>
    public sealed record StepShare(int Step, decimal? Amount);

    /// <summary>
    /// The steps each ingredient goes on. The page doesn't say which step uses what, so this reads the
    /// instructions:
    /// <list type="bullet">
    /// <item>When steps give amounts of it that add up to the line's, in the line's unit, the line is
    /// split between them: "2 cups sugar" with "add 1/2 cup of the sugar" and "beat in 1 1/2 cups sugar".
    /// "The remaining sugar" takes what the others leave.</item>
    /// <item>Otherwise it all goes on the first step whose title or instructions name it, else the first
    /// step.</item>
    /// </list>
    /// A name is looked for whole ("baking soda"), then by one of its words that no other ingredient has
    /// ("chocolate" for "semisweet chocolate"). Another ingredient's longer name doesn't count for it, so
    /// "brown sugar" isn't the sugar.
    /// </summary>
    public static IReadOnlyList<StepShare>[] StepsUsing(IReadOnlyList<ImportedRecipeStep> steps, IReadOnlyList<ParsedIngredientLine> lines)
    {
        var texts = steps.Select(s => $"{s.Title} {s.Instructions}").ToList();
        var names = lines.Select(l => l.Name).ToList();
        var words = names
            .Select(n => n.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        static Regex Whole(IEnumerable<string> terms) => new(
            $@"(?<![\p{{L}}-])(?:{string.Join('|', terms.SelectMany(IngredientLineParser.NameForms).Distinct()
                .OrderByDescending(t => t.Length).Select(Regex.Escape))})(?![\p{{L}}-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var sharesOf = new IReadOnlyList<StepShare>[names.Count];
        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];
            if (string.IsNullOrWhiteSpace(name))
            {
                sharesOf[i] = [new StepShare(0, null)];
                continue;
            }

            // Each pattern is built once: a page can have a couple of hundred ingredients.
            var self = Whole([name]);

            // Another ingredient's longer name isn't this one: "brown sugar" doesn't name the sugar.
            var longer = names
                .Where((other, j) => j != i && other.Length > name.Length && self.IsMatch(other))
                .ToList();
            var mask = longer.Count == 0 ? null : Whole(longer);
            var searched = mask == null
                ? texts
                : texts.Select(t => mask.Replace(t, " ")).ToList();

            int? FirstNaming(Regex pattern)
            {
                var index = searched.FindIndex(text => pattern.Matches(text).Any(m => !IsVerb(text, m)));
                return index < 0 ? null : index;
            }

            if (Split(lines[i], searched, self) is { } split)
            {
                sharesOf[i] = split;
                continue;
            }

            var own = words[i].Length > 1
                ? words[i].Where(w => w.Length > 2 && !words.Where((_, j) => j != i).Any(other => other.Contains(w))).ToList()
                : [];
            sharesOf[i] = [new StepShare(FirstNaming(self) ?? (own.Count > 0 ? FirstNaming(Whole(own)) : null) ?? 0, null)];
        }

        return sharesOf;
    }

    // Amounts are kept to four places, so thirds add up to a little under the whole.
    private const decimal SplitTolerance = 0.001m;

    /// <summary>
    /// The line split between the steps that give amounts of it, or null when they don't account for
    /// all of it, use another unit, or are all one step. A wrong split is worse than none.
    /// </summary>
    private static List<StepShare>? Split(ParsedIngredientLine line, IReadOnlyList<string> texts, Regex name)
    {
        if (line.Amount is not { } total)
            return null;

        var parts = new List<StepShare>();
        int? restStep = null;
        for (var step = 0; step < texts.Count; step++)
        {
            foreach (Match mention in name.Matches(texts[step]))
            {
                if (IsVerb(texts[step], mention)
                    || IngredientLineParser.AmountBefore(texts[step][..mention.Index]) is not { } said)
                    continue;

                if (said.Amount is not { } part)
                    restStep ??= step;
                else if (SameUnit(line, said))
                    parts.Add(new StepShare(step, part));
                else
                    return null;
            }
        }

        if (parts.Count == 0)
            return null;

        var given = parts.Sum(p => p.Amount!.Value);
        if (Math.Abs(total - given) > SplitTolerance)
        {
            if (restStep is not { } rest || given > total)
                return null;
            parts.Add(new StepShare(rest, total - given));
        }

        var byStep = parts
            .GroupBy(p => p.Step)
            .OrderBy(g => g.Key)
            .Select(g => new StepShare(g.Key, g.Sum(p => p.Amount)))
            .ToList();
        return byStep.Count > 1 ? byStep : null;
    }

    /// <summary>Whether the instructions' amount is in the line's unit: cups and cups, or a count and a count.</summary>
    private static bool SameUnit(ParsedIngredientLine line, InstructionAmount said)
    {
        if (line.StandardUnit is { } standard)
            return said.StandardUnit == standard;
        if (line.Unit is { } unit)
            return said is { Unit: { } saidUnit, StandardUnit: null }
                   && IngredientLineParser.NameForms(unit).Contains(saidUnit.ToLowerInvariant());
        return said.Unit is null;
    }

    // "Butter a loaf pan", "Flour the counter": an ingredient's name starting a sentence before an article
    // is something done to the pan, not the ingredient going in.
    private static bool IsVerb(string text, Match match) =>
        SentenceStartRegex().IsMatch(text[..match.Index])
        && ArticleRegex().IsMatch(text[(match.Index + match.Length)..]);

    [GeneratedRegex(@"(?:^|[.!?;:]\s*)\s*$")]
    private static partial Regex SentenceStartRegex();

    [GeneratedRegex(@"^\s+(?:a|an|the|your)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ArticleRegex();

    /// <summary>
    /// The page's photo as the recipe's primary image. Best effort: a photo that can't be fetched, or isn't
    /// a JPEG, PNG or WebP, leaves the recipe without one.
    /// </summary>
    private async Task AddPhotoAsync(Guid recipeId, string imageUrl, CancellationToken ct)
    {
        try
        {
            var fetched = await _fetcher.GetAsync(new Uri(imageUrl), MaxImageBytes, ImageHeaders, cancellationToken: ct);

            // Trust the bytes, not the remote Content-Type or the URL's file name.
            var format = ImageSignature.Detect(fetched.Body);
            if (format == null || format.ContentType == "image/gif")
            {
                _logger.LogInformation("Skipped imported recipe photo for {RecipeId}: not JPEG, PNG or WebP", recipeId);
                return;
            }

            using var stream = new MemoryStream(fetched.Body, writable: false);
            var image = await _recipeService.AddImageAsync(recipeId, stream, $"recipe-image{format.Extension}",
                format.ContentType, fetched.Body.Length, ct);

            // Only the source is recorded. An ExternalUrl would be served in place of the stored copy, and
            // the clients would load the photo from the recipe site.
            var entity = await _context.Set<RecipeImage>().FirstAsync(i => i.Id == image.Id, ct);
            entity.ExternalSource = ImageExternalSource;
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Couldn't add the imported photo to recipe {RecipeId}", recipeId);
        }
    }

    private static string? Truncate(string? text, int length) =>
        text == null || text.Length <= length ? text : text[..length];
}
