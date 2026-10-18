namespace Famick.HomeManagement.Core.Validators.Common;

/// <summary>
/// Bounds on paging and result-count parameters, so one request cannot ask the server for an
/// unbounded amount of work. Out-of-range values are brought into range rather than refused:
/// the clients ask for at most 100 a page, and a page below 1 means the first page.
/// </summary>
public static class QueryLimits
{
    /// <summary>Largest page of a paged list.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Most suggestions an autocomplete, search or lookup returns.</summary>
    public const int MaxSearchResults = 50;

    /// <summary>Most entries a history or log request returns.</summary>
    public const int MaxLogEntries = 500;

    /// <summary>Most servings a recipe may be scaled to.</summary>
    public const int MaxServings = 1000;

    /// <summary>A page number of at least 1.</summary>
    public static int Page(int? page) => page is null or < 1 ? 1 : page.Value;

    /// <summary>A page size between 1 and <see cref="MaxPageSize"/>, or <paramref name="fallback"/> when unset.</summary>
    public static int PageSize(int? size, int fallback) =>
        size is null or < 1 ? fallback : Math.Min(size.Value, MaxPageSize);

    /// <summary>
    /// How many rows to skip for <paramref name="page"/>, computed without overflowing for a
    /// very large page number.
    /// </summary>
    public static int Skip(int page, int pageSize) =>
        (int)Math.Min((long)(Page(page) - 1) * pageSize, int.MaxValue);

    /// <summary>A result count between 1 and <paramref name="max"/>, or <paramref name="fallback"/> when unset.</summary>
    public static int Count(int? requested, int fallback, int max) =>
        requested is null or < 1 ? fallback : Math.Min(requested.Value, max);
}
