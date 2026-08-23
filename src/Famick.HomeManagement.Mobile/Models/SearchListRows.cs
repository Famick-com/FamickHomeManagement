namespace Famick.HomeManagement.Mobile.Models;

/// <summary>
/// Non-product rows shown in the add-item results list.
/// <para>
/// The list mixes the household's own products with store results that arrive later, so it
/// needs rows that are not products: a heading to say which source a run of results came
/// from, and a placeholder while the slower store search is still running. They live in the
/// same collection as the products and are told apart by <c>SearchResultTemplateSelector</c>.
/// </para>
/// </summary>
public sealed record SearchSectionHeader(string Title);

/// <summary>
/// A row indicating a search is still in flight, so an empty store section reads as
/// "still looking" rather than "nothing found".
/// </summary>
public sealed record SearchProgressRow(string Message);
