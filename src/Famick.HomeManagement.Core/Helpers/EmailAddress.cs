namespace Famick.HomeManagement.Core.Helpers;

/// <summary>
/// The one spelling a sign-in email is stored and looked up in.
/// </summary>
/// <remarks>
/// A sign-in email names one person across every household, and the database holds it to that
/// with a unique index and a check that the stored value is already in this form. Every write
/// and every lookup goes through <see cref="Normalize"/>, so " Ann@Example.com" and
/// "ann@example.com" are never two accounts.
/// </remarks>
public static class EmailAddress
{
    /// <summary>Trimmed and lower-cased (invariant culture).</summary>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
