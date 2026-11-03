using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.ShoppingLists;
using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// How much of a shopping-list item has been bought, and how much is left. A purchase is recorded in
/// two places: <see cref="ShoppingListItem.PurchasedQuantity"/>, and, for a parent product checked off
/// as one of its children, <see cref="ShoppingListItem.ChildPurchasesJson"/>.
/// </summary>
internal static class ShoppingListItemQuantities
{
    /// <summary>The child purchases recorded on an item; none if the JSON is missing or unreadable.</summary>
    public static List<ChildPurchaseEntry> ParseChildPurchases(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<ChildPurchaseEntry>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>What has been bought against an item, both kinds of purchase together.</summary>
    public static decimal Bought(ShoppingListItem item) =>
        item.PurchasedQuantity + ParseChildPurchases(item.ChildPurchasesJson).Sum(p => p.Quantity);

    /// <summary>What is still to buy: nothing once purchased, otherwise the amount less both kinds of purchase.</summary>
    public static decimal Remaining(ShoppingListItem item) =>
        item.IsPurchased ? 0m : Math.Max(item.Amount - Bought(item), 0m);
}
