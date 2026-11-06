namespace Famick.HomeManagement.Shared.Tests.Unit.RecipeImport;

/// <summary>
/// Hand-written pages in the shapes recipe sites publish. None is a copy of a real page: the names,
/// text and URLs are made up, and only the structure follows what the sites emit.
/// </summary>
internal static class RecipePageFixtures
{
    /// <summary>
    /// A WordPress blog with a recipe plugin and an SEO plugin: one JSON-LD block holding an <c>@graph</c>,
    /// with the author and image as <c>@id</c> references, instructions in HowToSections, and a canonical link.
    /// </summary>
    public const string WordPressGraph = """
        <!doctype html>
        <html><head>
        <meta charset="utf-8">
        <title>Weeknight Lemon Chicken - Example Kitchen Blog</title>
        <meta property="og:site_name" content="Example Kitchen Blog">
        <link rel="canonical" href="https://kitchen.example/lemon-chicken/">
        <script type="application/ld+json">
        {
          "@context": "https://schema.org",
          "@graph": [
            { "@type": "Organization", "@id": "https://kitchen.example/#organization", "name": "Example Kitchen Blog" },
            { "@type": "Person", "@id": "https://kitchen.example/#/schema/person/abc", "name": "Jordan Example" },
            { "@type": "ImageObject", "@id": "https://kitchen.example/lemon-chicken/#primaryimage",
              "url": "https://kitchen.example/wp-content/uploads/lemon-chicken.jpg" },
            { "@type": "WebPage", "@id": "https://kitchen.example/lemon-chicken/", "name": "Weeknight Lemon Chicken" },
            {
              "@type": "Recipe",
              "@id": "https://kitchen.example/lemon-chicken/#recipe",
              "name": "Weeknight Lemon Chicken &amp; Rice",
              "author": { "@id": "https://kitchen.example/#/schema/person/abc" },
              "description": "<p>A bright, <strong>one-pan</strong> dinner.</p>",
              "image": [ "https://kitchen.example/wp-content/uploads/lemon-chicken-1200x1200.jpg",
                         "https://kitchen.example/wp-content/uploads/lemon-chicken-1200x900.jpg" ],
              "recipeYield": [ "4", "4 servings" ],
              "recipeIngredient": [
                "2 lbs boneless skinless chicken thighs",
                "1 cup long grain rice",
                "2 tablespoons olive oil",
                "1 lemon, juiced",
                "3 cloves garlic, minced",
                "salt to taste"
              ],
              "recipeInstructions": [
                {
                  "@type": "HowToSection",
                  "name": "Prepare",
                  "itemListElement": [
                    { "@type": "HowToStep", "text": "Pat the chicken dry and season with salt.", "name": "Pat the chicken dry" },
                    { "@type": "HowToStep", "text": "Mince the garlic." }
                  ]
                },
                {
                  "@type": "HowToSection",
                  "name": "Cook",
                  "itemListElement": [
                    { "@type": "HowToStep", "text": "Brown the chicken in the oil for 5 minutes per side." },
                    { "@type": "HowToStep", "text": "Add the rice, lemon and 2 cups water. Simmer 20 minutes." }
                  ]
                }
              ],
              "mainEntityOfPage": "https://kitchen.example/lemon-chicken/"
            }
          ]
        }
        </script>
        </head><body><h1>Weeknight Lemon Chicken</h1></body></html>
        """;

    /// <summary>
    /// A large recipe site: the JSON-LD is a top-level array whose recipe has an array <c>@type</c>, an
    /// ImageObject, an array of authors, a publisher, and its own <c>url</c> with a fragment.
    /// </summary>
    public const string BigSiteArray = """
        <!doctype html>
        <html><head>
        <title>Classic Pancakes Recipe</title>
        <script type="application/ld+json">
        [
          {
            "@context": "http://schema.org",
            "@type": ["Recipe", "NewsArticle"],
            "headline": "Classic Pancakes",
            "name": "Classic Pancakes",
            "url": "https://recipes.example/recipe/123/classic-pancakes/#recipe",
            "image": { "@type": "ImageObject", "url": "/images/classic-pancakes.jpg", "width": 1500, "height": 1125 },
            "author": [ { "@type": "Person", "name": "Sam Cook" }, { "@type": "Person", "name": "Alex Baker" } ],
            "publisher": { "@type": "Organization", "name": "Recipes Example" },
            "recipeYield": "8 pancakes",
            "recipeIngredient": [ "1 ½ cups all-purpose flour", "3 ½ teaspoons baking powder", "1 ¼ cups milk", "1 egg" ],
            "recipeInstructions": [
              { "@type": "HowToStep", "text": "In a large bowl, sift together the flour and baking powder." },
              { "@type": "HowToStep", "text": "Make a well in the center and pour in the milk and egg; mix until smooth." },
              { "@type": "HowToStep", "text": "Cook on a hot griddle about 2 minutes per side." }
            ]
          }
        ]
        </script>
        </head><body></body></html>
        """;

    /// <summary>
    /// A broken JSON-LD block before a good one, a single object with an array <c>@type</c>, and the
    /// instructions as one string with line breaks.
    /// </summary>
    public const string ArrayTypeAfterBrokenBlock = """
        <!doctype html>
        <html><head>
        <title>Garden Salsa</title>
        <script type="application/ld+json">{ "@context": "https://schema.org", "@type": "WebSite", </script>
        <script type="application/ld+json">
        {
          "@context": "https://schema.org",
          "@type": ["Recipe"],
          "name": "Garden Salsa",
          "author": "Riley Example",
          "recipeYield": 6,
          "recipeIngredient": "4 ripe tomatoes, diced",
          "recipeInstructions": "Dice the tomatoes.\nStir in the onion and cilantro.<br>Chill for 1 hour."
        }
        </script>
        </head><body></body></html>
        """;

    /// <summary>An older site with microdata only, an author item, and the instructions as a list.</summary>
    public const string MicrodataOnly = """
        <!doctype html>
        <html><head><title>Grandma's Banana Bread</title>
        <meta property="og:site_name" content="Old Recipe Box">
        </head>
        <body>
        <div itemscope itemtype="http://schema.org/Recipe">
          <h1 itemprop="name">Grandma's Banana Bread</h1>
          <img itemprop="image" src="https://oldbox.example/img/banana-bread.jpg" alt="">
          <span itemprop="author" itemscope itemtype="http://schema.org/Person">By <span itemprop="name">Pat Example</span></span>
          <p itemprop="description">Moist and easy.</p>
          <meta itemprop="recipeYield" content="1 loaf (10 slices)">
          <ul>
            <li itemprop="recipeIngredient">3 ripe bananas, mashed</li>
            <li itemprop="recipeIngredient">1/3 cup melted butter</li>
            <li itemprop="recipeIngredient">1 1/2 cups flour</li>
          </ul>
          <ol itemprop="recipeInstructions">
            <li>Preheat the oven to 350&deg;F.</li>
            <li>Mix the butter into the mashed bananas.</li>
            <li>Bake for 60 minutes.</li>
          </ol>
        </div>
        </body></html>
        """;

    /// <summary>An article page with JSON-LD but no recipe.</summary>
    public const string NoRecipe = """
        <!doctype html>
        <html><head><title>Ten Kitchen Tips</title>
        <script type="application/ld+json">
        { "@context": "https://schema.org", "@type": "Article", "headline": "Ten Kitchen Tips", "author": { "@type": "Person", "name": "Sam Cook" } }
        </script>
        </head><body><p>Keep your knives sharp.</p></body></html>
        """;
}
