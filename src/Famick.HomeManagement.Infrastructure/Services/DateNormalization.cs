namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Normalizes client-supplied dates before they reach the database.
/// </summary>
public static class DateNormalization
{
    /// <summary>
    /// Normalizes a calendar date (best-before, expiry, and similar) for storage.
    /// <para>
    /// Clients pick a <em>calendar date</em>, not an instant. The mobile date picker uses
    /// <c>DateTime.Today</c>, which is <c>Kind=Local</c>, and System.Text.Json serializes it
    /// with an offset — so it arrives here still <c>Local</c>. Npgsql rejects any non-UTC
    /// <c>DateTime</c> written to <c>timestamp with time zone</c>, which surfaces as a
    /// <c>DbUpdateException</c> at save time rather than anywhere near the cause.
    /// </para>
    /// <para>
    /// This deliberately does <b>not</b> call <c>ToUniversalTime()</c>. For a client east of
    /// UTC, local midnight converts to the previous day in UTC, silently moving the date a
    /// day earlier than the user chose — a wrong expiry date is worse than a rejected save,
    /// because nothing reports it. Taking the date part and stamping it UTC preserves the
    /// day that was picked, and matches the <c>DateTime.UtcNow.Date.AddDays(...)</c>
    /// convention already used for dates derived on the server.
    /// </para>
    /// </summary>
    public static DateTime? ToUtcCalendarDate(DateTime? value)
        => value.HasValue ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc) : null;
}
