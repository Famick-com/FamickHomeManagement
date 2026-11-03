using Famick.HomeManagement.Core.Helpers;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Converts amounts between a product's own units: its stock unit, which stock is kept in, its purchase
/// unit, which <see cref="Product.QuantityUnitFactorPurchaseToStock"/> relates to the stock unit, and any
/// other unit that converts to the stock unit through the standard measures both are tagged with.
/// </summary>
/// <remarks>
/// Units are looked up by ID in a dictionary the caller loads, rather than through the product's
/// navigation properties, so a caller can load exactly the units it needs. Every method returns null
/// when there is no conversion, rather than guessing.
/// </remarks>
internal static class ProductUnits
{
    /// <summary>
    /// The amount, in <paramref name="unitId"/>, expressed in the product's stock unit. A missing unit is
    /// taken to be the stock unit, which is how a recipe line without one has always been compared.
    /// </summary>
    public static decimal? ToStock(Product product, Guid? unitId, decimal amount, IReadOnlyDictionary<Guid, QuantityUnit> units)
    {
        if (unitId == null || unitId == product.QuantityUnitIdStock)
            return amount;
        if (unitId == product.QuantityUnitIdPurchase && product.QuantityUnitFactorPurchaseToStock > 0)
            return amount * product.QuantityUnitFactorPurchaseToStock;
        return UnitConverter.Convert(amount, StandardOf(unitId.Value, units), StandardOf(product.QuantityUnitIdStock, units));
    }

    /// <summary>The inverse of <see cref="ToStock"/>: an amount in the stock unit, expressed in <paramref name="unitId"/>.</summary>
    public static decimal? FromStock(Product product, Guid? unitId, decimal stockAmount, IReadOnlyDictionary<Guid, QuantityUnit> units)
    {
        if (unitId == null || unitId == product.QuantityUnitIdStock)
            return stockAmount;
        if (unitId == product.QuantityUnitIdPurchase && product.QuantityUnitFactorPurchaseToStock > 0)
            return stockAmount / product.QuantityUnitFactorPurchaseToStock;
        return UnitConverter.Convert(stockAmount, StandardOf(product.QuantityUnitIdStock, units), StandardOf(unitId.Value, units));
    }

    /// <summary>
    /// A child's stock amount in its parent's stock unit. A child uses its parent's stock unit, but one
    /// set up before that rule may not; its stock still counts when the two units convert.
    /// </summary>
    public static decimal? ChildToParentStock(Product parent, Product child, decimal amount, IReadOnlyDictionary<Guid, QuantityUnit> units) =>
        child.QuantityUnitIdStock == parent.QuantityUnitIdStock
            ? amount
            : UnitConverter.Convert(amount, StandardOf(child.QuantityUnitIdStock, units), StandardOf(parent.QuantityUnitIdStock, units));

    /// <summary>
    /// The same as <see cref="ChildToParentStock(Product, Product, decimal, IReadOnlyDictionary{Guid, QuantityUnit})"/>,
    /// reading the units from the products' loaded stock units.
    /// </summary>
    public static decimal? ChildToParentStock(Product parent, Product child, decimal amount) =>
        child.QuantityUnitIdStock == parent.QuantityUnitIdStock
            ? amount
            : UnitConverter.Convert(amount, child.QuantityUnitStock?.StandardUnit, parent.QuantityUnitStock?.StandardUnit);

    /// <summary>
    /// How much to buy, in the purchase unit, to cover an amount in the stock unit. Rounded up to whole
    /// purchase units when they differ from the stock unit: 2 cups of flour is 1 bag, not 0.11 of one.
    /// </summary>
    public static decimal ToPurchaseToBuy(Product product, decimal stockAmount)
    {
        if (stockAmount <= 0)
            return 0;
        if (product.QuantityUnitIdPurchase == product.QuantityUnitIdStock || product.QuantityUnitFactorPurchaseToStock <= 0)
            return stockAmount;
        return Math.Ceiling(stockAmount / product.QuantityUnitFactorPurchaseToStock);
    }

    /// <summary>
    /// Rounds a converted amount for display and drops trailing zeros, so 2 tbsp in cups reads 0.125 rather
    /// than 0.1250000000000000000000.
    /// </summary>
    public static decimal Tidy(decimal amount) =>
        Math.Round(amount, 4) / 1.000000000000000000000000000000000m;

    private static Domain.Enums.StandardUnit? StandardOf(Guid unitId, IReadOnlyDictionary<Guid, QuantityUnit> units) =>
        units.TryGetValue(unitId, out var unit) ? unit.StandardUnit : null;
}
