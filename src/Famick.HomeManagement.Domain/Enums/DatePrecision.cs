namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Indicates the precision level of a date when exact date is unknown
/// </summary>
public enum DatePrecision
{
    /// <summary>
    /// Date is completely unknown
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Only the year is known (e.g., born in 1985)
    /// </summary>
    Year = 1,

    /// <summary>
    /// Year and month are known (e.g., born March 1985)
    /// </summary>
    YearMonth = 2,

    /// <summary>
    /// Full date is known (e.g., born March 15, 1985)
    /// </summary>
    Full = 3
}
