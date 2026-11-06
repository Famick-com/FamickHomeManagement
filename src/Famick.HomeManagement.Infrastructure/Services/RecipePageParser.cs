using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>A step read from a recipe page. <see cref="Title"/> is the name of the section it starts.</summary>
public sealed record ImportedRecipeStep(string? Title, string Instructions);

/// <summary>
/// A recipe read from a web page, cleaned of markup and cut to the lengths the recipe tables hold.
/// </summary>
/// <param name="Source">The page's canonical URL, which also identifies the recipe when it is imported again.</param>
/// <param name="Attribution">"Recipe by {author} from {site}", without whichever part the page lacks; null when it has neither.</param>
public sealed record ImportedRecipe(
    string Name,
    string? Description,
    int Servings,
    IReadOnlyList<ImportedRecipeStep> Steps,
    IReadOnlyList<string> Ingredients,
    string? ImageUrl,
    string Source,
    string? Attribution);

/// <summary>
/// Reads the schema.org <c>Recipe</c> that recipe sites publish for search engines: JSON-LD first, then
/// microdata. Nothing here fetches; the importer hands over the page it fetched.
/// </summary>
public static partial class RecipePageParser
{
    public const int NameLength = 200;
    public const int TitleLength = 200;
    public const int InstructionsLength = 10000;
    public const int DescriptionLength = 4000;
    public const int IngredientLength = 500;
    public const int SourceLength = 2000;
    public const int AttributionLength = 1000;

    // Enough for any real recipe, and a bound on what a hostile page can make the importer save.
    private const int MaxSteps = 100;
    private const int MaxIngredients = 200;

    // How far into nested JSON-LD to look for the recipe: @graph, arrays and wrappers like WebPage.mainEntity.
    private const int MaxJsonDepth = 8;

    [GeneratedRegex(@"<\s*br\s*/?\s*>|<\s*/\s*(?:p|li|div|h[1-6])\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakTagRegex();

    [GeneratedRegex(@"[ \t\f\v ]+")]
    private static partial Regex SpacesRegex();

    [GeneratedRegex(@"[0-9]+")]
    private static partial Regex WholeNumberRegex();

    [GeneratedRegex(@"schema\.org/Recipe$", RegexOptions.IgnoreCase)]
    private static partial Regex MicrodataRecipeTypeRegex();

    /// <summary>The recipe on the page, or null when it has none.</summary>
    /// <param name="html">The page as fetched; its charset is read from the page itself.</param>
    /// <param name="pageUrl">Where the page was finally fetched from, after redirects. Relative links resolve against it.</param>
    public static ImportedRecipe? Parse(byte[] html, Uri pageUrl)
    {
        using var stream = new MemoryStream(html, writable: false);
        using var document = new HtmlParser().ParseDocument(stream);

        var fields = FromJsonLd(document) ?? FromMicrodata(document);
        if (fields == null)
            return null;

        var name = Truncate(Clean(fields.Name), NameLength);
        if (string.IsNullOrEmpty(name))
            name = Truncate(Clean(Meta(document, "og:title") ?? document.Title), NameLength);
        if (string.IsNullOrEmpty(name))
            return null;

        var steps = fields.Steps
            .Select(s => new ImportedRecipeStep(
                NullIfEmpty(Truncate(Clean(s.Title), TitleLength)),
                Truncate(Clean(s.Instructions), InstructionsLength)))
            .Where(s => s.Instructions.Length > 0 || s.Title != null)
            .Take(MaxSteps)
            .ToList();

        var ingredients = fields.Ingredients
            .Select(i => Truncate(Clean(i), IngredientLength))
            .Where(i => i.Length > 0)
            .Take(MaxIngredients)
            .ToList();

        var source = Absolute(fields.Url, pageUrl)
            ?? Absolute(document.QuerySelector("link[rel~='canonical']")?.GetAttribute("href"), pageUrl)
            ?? pageUrl;

        var author = NullIfEmpty(Clean(fields.Author));
        var site = NullIfEmpty(Clean(fields.Publisher)) ?? NullIfEmpty(Clean(Meta(document, "og:site_name")));
        var attribution = (author, site) switch
        {
            (not null, not null) => $"Recipe by {author} from {site}",
            (not null, null) => $"Recipe by {author}",
            (null, not null) => $"Recipe from {site}",
            _ => null
        };

        return new ImportedRecipe(
            name,
            NullIfEmpty(Truncate(Clean(fields.Description), DescriptionLength)),
            Servings(fields.Yields),
            steps,
            ingredients,
            Absolute(fields.Image, pageUrl)?.AbsoluteUri,
            Truncate(WithoutFragment(source), SourceLength),
            attribution == null ? null : Truncate(attribution, AttributionLength));
    }

    /// <summary>What the page says, before cleaning: text may still hold markup and entities.</summary>
    private sealed record RawRecipe(
        string? Name,
        string? Description,
        List<string> Yields,
        List<(string? Title, string Instructions)> Steps,
        List<string> Ingredients,
        string? Image,
        string? Url,
        string? Author,
        string? Publisher);

    #region JSON-LD

    private static RawRecipe? FromJsonLd(IDocument document)
    {
        foreach (var script in document.QuerySelectorAll("script[type]"))
        {
            if (!script.GetAttribute("type")!.Trim().Equals("application/ld+json", StringComparison.OrdinalIgnoreCase))
                continue;

            JsonDocument json;
            try
            {
                json = JsonDocument.Parse(script.TextContent, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
            }
            catch (JsonException)
            {
                // One broken block doesn't hide a good one after it.
                continue;
            }

            using (json)
            {
                if (FindRecipe(json.RootElement, 0) is { } recipe)
                {
                    var nodes = new Dictionary<string, JsonElement>();
                    CollectNodes(json.RootElement, nodes, 0);
                    return ReadJsonRecipe(recipe, nodes);
                }
            }
        }

        return null;
    }

    private static JsonElement? FindRecipe(JsonElement element, int depth)
    {
        if (depth > MaxJsonDepth)
            return null;

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                if (FindRecipe(item, depth + 1) is { } found)
                    return found;
            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (IsType(element, "Recipe"))
            return element;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                && FindRecipe(property.Value, depth + 1) is { } found)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Every node with an <c>@id</c>, so a reference such as <c>"author": { "@id": "#/schema/person/1" }</c>
    /// can be followed to the node it names. WordPress SEO plugins write authors and images this way.
    /// </summary>
    private static void CollectNodes(JsonElement element, Dictionary<string, JsonElement> nodes, int depth)
    {
        if (depth > MaxJsonDepth)
            return;

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                CollectNodes(item, nodes, depth + 1);
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            // A node with more than its id is the node itself, not a reference to it.
            if (element.TryGetProperty("@id", out var id) && id.ValueKind == JsonValueKind.String
                && element.EnumerateObject().Count() > 1)
                nodes.TryAdd(id.GetString()!, element);

            foreach (var property in element.EnumerateObject())
                CollectNodes(property.Value, nodes, depth + 1);
        }
    }

    /// <summary>The node a bare <c>{ "@id": … }</c> reference names, or the element itself.</summary>
    private static JsonElement Resolve(JsonElement element, Dictionary<string, JsonElement> nodes)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("@id", out var id) && id.ValueKind == JsonValueKind.String
            && element.EnumerateObject().Count() == 1
            && nodes.TryGetValue(id.GetString()!, out var node))
            return node;

        return element;
    }

    /// <summary>Whether <c>@type</c> is, or includes, <paramref name="type"/>, with or without a schema.org prefix.</summary>
    private static bool IsType(JsonElement element, string type)
    {
        if (!element.TryGetProperty("@type", out var value))
            return false;

        static bool Matches(string? candidate, string type) =>
            candidate != null
            && (candidate.Equals(type, StringComparison.OrdinalIgnoreCase)
                || candidate.EndsWith("/" + type, StringComparison.OrdinalIgnoreCase)
                || candidate.EndsWith(":" + type, StringComparison.OrdinalIgnoreCase));

        return value.ValueKind switch
        {
            JsonValueKind.String => Matches(value.GetString(), type),
            JsonValueKind.Array => value.EnumerateArray().Any(t => t.ValueKind == JsonValueKind.String && Matches(t.GetString(), type)),
            _ => false
        };
    }

    private static RawRecipe ReadJsonRecipe(JsonElement recipe, Dictionary<string, JsonElement> nodes)
    {
        var ingredients = Strings(Property(recipe, "recipeIngredient") ?? Property(recipe, "ingredients")).ToList();

        var steps = new List<(string? Title, string Instructions)>();
        if (Property(recipe, "recipeInstructions") is { } instructions)
            ReadInstructions(instructions, null, steps, 0);

        return new RawRecipe(
            Text(Property(recipe, "name")),
            Text(Property(recipe, "description")),
            Strings(Property(recipe, "recipeYield")).ToList(),
            steps,
            ingredients,
            ImageUrl(Property(recipe, "image"), nodes),
            Text(Property(recipe, "url")),
            Name(Property(recipe, "author"), nodes),
            Name(Property(recipe, "publisher"), nodes));
    }

    /// <summary>
    /// Steps in order. A HowToSection's name becomes the title of its first step; a plain string is split
    /// into one step per line.
    /// </summary>
    private static void ReadInstructions(JsonElement element, string? sectionTitle, List<(string?, string)> steps, int depth)
    {
        if (depth > MaxJsonDepth)
            return;

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                foreach (var line in Lines(element.GetString()))
                {
                    steps.Add((sectionTitle, line));
                    sectionTitle = null;
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var before = steps.Count;
                    ReadInstructions(item, sectionTitle, steps, depth + 1);
                    if (steps.Count > before)
                        sectionTitle = null;
                }
                break;

            case JsonValueKind.Object:
                if (IsType(element, "HowToSection") || (Property(element, "itemListElement") != null && !IsType(element, "HowToStep")))
                {
                    var title = Text(Property(element, "name")) ?? sectionTitle;
                    if (Property(element, "itemListElement") is { } items)
                        ReadInstructions(items, title, steps, depth + 1);
                }
                else if ((Text(Property(element, "text")) ?? Text(Property(element, "name"))) is { } text)
                {
                    steps.Add((sectionTitle, text));
                }
                break;
        }
    }

    private static JsonElement? Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value
            : null;

    /// <summary>A value as text: a string or number as it is, the first of an array, an object's name or text.</summary>
    private static string? Text(JsonElement? element) => element?.ValueKind switch
    {
        JsonValueKind.String => element.Value.GetString(),
        JsonValueKind.Number => element.Value.GetRawText(),
        JsonValueKind.Array => element.Value.EnumerateArray().Select(e => Text(e)).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)),
        JsonValueKind.Object => Text(Property(element.Value, "name")) ?? Text(Property(element.Value, "text")) ?? Text(Property(element.Value, "@value")),
        _ => null
    };

    private static IEnumerable<string> Strings(JsonElement? element)
    {
        if (element is not { } value)
            yield break;

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
                if (Text(item) is { } text)
                    yield return text;
        }
        else if (Text(value) is { } text)
        {
            yield return text;
        }
    }

    /// <summary>A person or organization's name: a string, an object's name, or the first of an array.</summary>
    private static string? Name(JsonElement? element, Dictionary<string, JsonElement> nodes) => element?.ValueKind switch
    {
        JsonValueKind.Array => element.Value.EnumerateArray().Select(e => Name(e, nodes)).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
        JsonValueKind.Object => Text(Property(Resolve(element.Value, nodes), "name")),
        _ => Text(element)
    };

    private static string? ImageUrl(JsonElement? element, Dictionary<string, JsonElement> nodes) => element?.ValueKind switch
    {
        JsonValueKind.String => element.Value.GetString(),
        JsonValueKind.Array => element.Value.EnumerateArray().Select(e => ImageUrl(e, nodes)).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u)),
        JsonValueKind.Object => Text(Property(Resolve(element.Value, nodes), "url")) ?? Text(Property(Resolve(element.Value, nodes), "contentUrl")),
        _ => null
    };

    #endregion

    #region Microdata

    private static RawRecipe? FromMicrodata(IDocument document)
    {
        var recipe = document.QuerySelectorAll("[itemscope][itemtype]")
            .FirstOrDefault(e => e.GetAttribute("itemtype")!
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(t => MicrodataRecipeTypeRegex().IsMatch(t)));
        if (recipe == null)
            return null;

        var ingredients = Props(recipe, "recipeIngredient").ToList();
        if (ingredients.Count == 0)
            ingredients = Props(recipe, "ingredients").ToList();

        var steps = new List<(string? Title, string Instructions)>();
        foreach (var element in Props(recipe, "recipeInstructions"))
        {
            // One element holding a list, or one element per step.
            var items = element.QuerySelectorAll("li, p").ToList();
            if (items.Count > 0)
                steps.AddRange(items.Select(i => ((string?)null, i.InnerHtml)));
            else
                steps.AddRange(Lines(PropValue(element)).Select(l => ((string?)null, l)));
        }

        return new RawRecipe(
            Props(recipe, "name").Select(PropValue).FirstOrDefault(),
            Props(recipe, "description").Select(PropValue).FirstOrDefault(),
            Props(recipe, "recipeYield").Select(PropValue).ToList(),
            steps,
            ingredients.Select(PropValue).ToList(),
            Props(recipe, "image").Select(PropValue).FirstOrDefault(),
            Props(recipe, "url").Select(PropValue).FirstOrDefault(),
            Props(recipe, "author").Select(ItemName).FirstOrDefault(),
            Props(recipe, "publisher").Select(ItemName).FirstOrDefault());
    }

    /// <summary>The recipe's own properties called <paramref name="name"/>, not those of an item inside it.</summary>
    private static IEnumerable<IElement> Props(IElement scope, string name) =>
        scope.QuerySelectorAll("[itemprop]")
            .Where(e => e.GetAttribute("itemprop")!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(name)
                        && e.ParentElement?.Closest("[itemscope]") == scope);

    private static string PropValue(IElement element) => element.LocalName switch
    {
        "meta" => element.GetAttribute("content") ?? string.Empty,
        "img" or "audio" or "video" or "source" or "embed" or "iframe" => element.GetAttribute("src") ?? string.Empty,
        "a" or "area" or "link" => element.GetAttribute("href") ?? string.Empty,
        "time" => element.GetAttribute("datetime") ?? element.TextContent,
        "data" or "meter" => element.GetAttribute("value") ?? element.TextContent,
        _ => element.GetAttribute("content") ?? element.InnerHtml
    };

    /// <summary>An author or publisher: a nested item's name, or the element's own value.</summary>
    private static string ItemName(IElement element) =>
        element.HasAttribute("itemscope")
            ? Props(element, "name").Select(PropValue).FirstOrDefault() ?? element.TextContent
            : PropValue(element);

    #endregion

    #region Cleaning

    /// <summary>Markup removed, entities decoded, and whitespace collapsed to single spaces.</summary>
    private static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return string.Join(' ', Lines(text));
    }

    /// <summary>
    /// The text's lines, with markup removed and entities decoded. Paragraphs, list items and line breaks
    /// end a line, as newlines do.
    /// </summary>
    private static IEnumerable<string> Lines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var withBreaks = LineBreakTagRegex().Replace(text, "\n");
        string decoded;
        if (withBreaks.Contains('<') || withBreaks.Contains('&'))
        {
            using var fragment = new HtmlParser().ParseDocument("<!doctype html><html><body>" + withBreaks + "</body></html>");
            decoded = fragment.Body?.TextContent ?? withBreaks;
        }
        else
        {
            decoded = withBreaks;
        }

        return decoded
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => SpacesRegex().Replace(l, " ").Trim())
            .Where(l => l.Length > 0)
            .ToList();
    }

    /// <summary>The first whole number in any of the yields ("4 servings", ["6", "6 slices"]); 1 when there is none.</summary>
    private static int Servings(IEnumerable<string> yields)
    {
        foreach (var yield in yields)
        {
            var match = WholeNumberRegex().Match(yield);
            if (match.Success && int.TryParse(match.Value, out var servings) && servings is > 0 and <= 1000)
                return servings;
        }

        return 1;
    }

    private static Uri? Absolute(string? url, Uri baseUrl)
    {
        url = url?.Trim();
        if (string.IsNullOrEmpty(url))
            return null;

        return Uri.TryCreate(baseUrl, url, out var absolute)
               && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
            ? absolute
            : null;
    }

    private static string WithoutFragment(Uri url) =>
        string.IsNullOrEmpty(url.Fragment) ? url.AbsoluteUri : url.GetLeftPart(UriPartial.Query);

    private static string? Meta(IDocument document, string property) =>
        document.QuerySelectorAll("meta[property], meta[name]")
            .FirstOrDefault(m => string.Equals(m.GetAttribute("property") ?? m.GetAttribute("name"), property, StringComparison.OrdinalIgnoreCase))
            ?.GetAttribute("content");

    private static string Truncate(string text, int length) =>
        text.Length <= length ? text : text[..length].TrimEnd();

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;

    #endregion
}
