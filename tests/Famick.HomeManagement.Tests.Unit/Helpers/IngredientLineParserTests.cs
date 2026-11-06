using Famick.HomeManagement.Core.Helpers;
using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Helpers;

public class IngredientLineParserTests
{
    [Theory]
    [InlineData("2 cups all-purpose flour, sifted", 2.0, "cups", StandardUnit.Cup, "flour", "all-purpose, sifted")]
    [InlineData("1 1/2 tsp baking soda", 1.5, "tsp", StandardUnit.Teaspoon, "baking soda", null)]
    [InlineData("½ cup unsalted butter (softened)", 0.5, "cup", StandardUnit.Cup, "butter", "unsalted, softened")]
    [InlineData("3 large eggs", 3.0, null, null, "eggs", "large")]
    [InlineData("1 T olive oil", 1.0, "tbsp", StandardUnit.Tablespoon, "olive oil", null)]
    [InlineData("1 t vanilla extract", 1.0, "tsp", StandardUnit.Teaspoon, "vanilla extract", null)]
    [InlineData("2 Tbsp. honey", 2.0, "tbsp", StandardUnit.Tablespoon, "honey", null)]
    [InlineData("200g caster sugar", 200.0, "g", StandardUnit.Gram, "caster sugar", null)]
    [InlineData("8 fl oz chicken stock", 8.0, "fl oz", StandardUnit.FluidOunce, "chicken stock", null)]
    [InlineData("1 lb ground beef", 1.0, "lb", StandardUnit.Pound, "ground beef", null)]
    [InlineData("2-3 cloves garlic, minced", 2.0, "cloves", null, "garlic", "minced")]
    [InlineData("2 to 3 cloves garlic", 2.0, "cloves", null, "garlic", null)]
    [InlineData("a pinch of salt", 1.0, "pinch", null, "salt", null)]
    [InlineData("1 (14.5 oz) can diced tomatoes, drained", 1.0, "can", null, "tomatoes", "diced, 14.5 oz, drained")]
    [InlineData("salt to taste", null, null, null, "salt", "to taste")]
    [InlineData("Fresh parsley for garnish", null, null, null, "parsley", "Fresh, for garnish")]
    [InlineData("1 cup butter or margarine", 1.0, "cup", StandardUnit.Cup, "butter", "or margarine")]
    [InlineData("▢ 1 cup milk", 1.0, "cup", StandardUnit.Cup, "milk", null)]
    [InlineData("1⅓ cups whole milk", 1.3333, "cups", StandardUnit.Cup, "whole milk", null)]
    [InlineData("2 cups packed light brown sugar", 2.0, "cups", StandardUnit.Cup, "light brown sugar", "packed")]
    public void Takes_a_line_apart(string line, double? amount, string? unit, StandardUnit? standardUnit, string name, string? note)
    {
        var parsed = IngredientLineParser.Parse(line);

        parsed.Should().Be(new ParsedIngredientLine(
            amount is { } a ? Math.Round((decimal)a, 4) : null, unit, standardUnit, name, note));
    }

    [Fact]
    public void A_word_that_is_not_a_unit_stays_in_the_name()
    {
        var parsed = IngredientLineParser.Parse("2 apples");

        parsed.Unit.Should().BeNull();
        parsed.Name.Should().Be("apples");
    }

    [Fact]
    public void An_absurd_amount_is_no_amount()
    {
        IngredientLineParser.Parse("10000000 cups flour").Amount.Should().BeNull();
    }

    [Theory]
    [InlineData(" ")]
    [InlineData(null)]
    public void An_empty_line_has_an_empty_name(string? line)
    {
        IngredientLineParser.Parse(line).Name.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Eggs", new[] { "eggs", "egg" })]
    [InlineData("egg", new[] { "egg", "eggs" })]
    [InlineData("green onions", new[] { "green onions", "green onion" })]
    [InlineData("cherries", new[] { "cherries", "cherry" })]
    [InlineData("berry", new[] { "berry", "berries" })]
    [InlineData("tomatoes", new[] { "tomatoes", "tomato" })]
    [InlineData("potato", new[] { "potato", "potatoes" })]
    [InlineData("peaches", new[] { "peaches", "peach" })]
    [InlineData("flour", new[] { "flour", "flours" })]
    [InlineData("molasses", new[] { "molasses" })]
    public void Name_forms_cover_singular_and_plural(string name, string[] expected)
    {
        IngredientLineParser.NameForms(name).Should().Equal(expected);
    }
}
