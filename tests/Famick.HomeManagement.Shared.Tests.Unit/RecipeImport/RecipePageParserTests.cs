using System.Text;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.RecipeImport;

public class RecipePageParserTests
{
    private static ImportedRecipe? Parse(string html, string url = "https://example.test/page") =>
        RecipePageParser.Parse(Encoding.UTF8.GetBytes(html), new Uri(url));

    [Fact]
    public void A_WordPress_graph_page_imports_with_sections_and_resolved_references()
    {
        var recipe = Parse(RecipePageFixtures.WordPressGraph, "https://kitchen.example/lemon-chicken/?utm_source=x")!;

        recipe.Name.Should().Be("Weeknight Lemon Chicken & Rice");
        recipe.Description.Should().Be("A bright, one-pan dinner.");
        recipe.Servings.Should().Be(4);
        recipe.Steps.Should().Equal(
            new ImportedRecipeStep("Prepare", "Pat the chicken dry and season with salt."),
            new ImportedRecipeStep(null, "Mince the garlic."),
            new ImportedRecipeStep("Cook", "Brown the chicken in the oil for 5 minutes per side."),
            new ImportedRecipeStep(null, "Add the rice, lemon and 2 cups water. Simmer 20 minutes."));
        recipe.Ingredients.Should().HaveCount(6).And.StartWith("2 lbs boneless skinless chicken thighs");
        recipe.ImageUrl.Should().Be("https://kitchen.example/wp-content/uploads/lemon-chicken-1200x1200.jpg");
        recipe.Attribution.Should().Be("Recipe by Jordan Example from Example Kitchen Blog");
        recipe.Source.Should().Be("https://kitchen.example/lemon-chicken/");
    }

    [Fact]
    public void A_big_site_array_imports_with_its_own_url_and_publisher()
    {
        var recipe = Parse(RecipePageFixtures.BigSiteArray, "https://recipes.example/recipe/123/classic-pancakes/")!;

        recipe.Name.Should().Be("Classic Pancakes");
        recipe.Servings.Should().Be(8);
        recipe.Steps.Select(s => s.Instructions).Should().HaveCount(3)
            .And.StartWith("In a large bowl, sift together the flour and baking powder.");
        recipe.Steps.Should().OnlyContain(s => s.Title == null);
        recipe.Ingredients.Should().Equal("1 ½ cups all-purpose flour", "3 ½ teaspoons baking powder", "1 ¼ cups milk", "1 egg");
        recipe.ImageUrl.Should().Be("https://recipes.example/images/classic-pancakes.jpg");
        recipe.Attribution.Should().Be("Recipe by Sam Cook from Recipes Example");
        recipe.Source.Should().Be("https://recipes.example/recipe/123/classic-pancakes/");
    }

    [Fact]
    public void A_broken_block_is_skipped_and_string_instructions_are_split_into_steps()
    {
        var recipe = Parse(RecipePageFixtures.ArrayTypeAfterBrokenBlock, "https://salsa.example/garden-salsa")!;

        recipe.Name.Should().Be("Garden Salsa");
        recipe.Servings.Should().Be(6);
        recipe.Ingredients.Should().Equal("4 ripe tomatoes, diced");
        recipe.Steps.Select(s => s.Instructions).Should().Equal(
            "Dice the tomatoes.", "Stir in the onion and cilantro.", "Chill for 1 hour.");
        recipe.Attribution.Should().Be("Recipe by Riley Example");
        recipe.Source.Should().Be("https://salsa.example/garden-salsa");
        recipe.ImageUrl.Should().BeNull();
    }

    [Fact]
    public void A_microdata_page_imports()
    {
        var recipe = Parse(RecipePageFixtures.MicrodataOnly, "https://oldbox.example/banana-bread")!;

        recipe.Name.Should().Be("Grandma's Banana Bread");
        recipe.Description.Should().Be("Moist and easy.");
        recipe.Servings.Should().Be(1);
        recipe.Ingredients.Should().Equal("3 ripe bananas, mashed", "1/3 cup melted butter", "1 1/2 cups flour");
        recipe.Steps.Select(s => s.Instructions).Should().Equal(
            "Preheat the oven to 350°F.", "Mix the butter into the mashed bananas.", "Bake for 60 minutes.");
        recipe.ImageUrl.Should().Be("https://oldbox.example/img/banana-bread.jpg");
        recipe.Attribution.Should().Be("Recipe by Pat Example from Old Recipe Box");
    }

    [Fact]
    public void A_page_without_a_recipe_has_none()
    {
        Parse(RecipePageFixtures.NoRecipe).Should().BeNull();
        Parse("<html><body><p>Hello</p></body></html>").Should().BeNull();
    }

    [Fact]
    public void Long_text_is_cut_to_the_column_lengths()
    {
        var name = new string('n', 300);
        var step = new string('s', 12000);
        var html = $$"""
            <script type="application/ld+json">
            { "@type": "Recipe", "name": "{{name}}", "recipeInstructions": "{{step}}", "recipeIngredient": ["{{new string('i', 600)}}"] }
            </script>
            """;

        var recipe = Parse(html)!;

        recipe.Name.Should().HaveLength(RecipePageParser.NameLength);
        recipe.Steps.Single().Instructions.Should().HaveLength(RecipePageParser.InstructionsLength);
        recipe.Ingredients.Single().Should().HaveLength(RecipePageParser.IngredientLength);
    }

    [Fact]
    public void A_page_in_another_charset_is_decoded_by_its_meta_tag()
    {
        var html = """
            <html><head><meta charset="iso-8859-1">
            <script type="application/ld+json">{ "@type": "Recipe", "name": "Crème brûlée" }</script>
            </head></html>
            """;

        var recipe = RecipePageParser.Parse(Encoding.Latin1.GetBytes(html), new Uri("https://example.test/"))!;

        recipe.Name.Should().Be("Crème brûlée");
    }
}
