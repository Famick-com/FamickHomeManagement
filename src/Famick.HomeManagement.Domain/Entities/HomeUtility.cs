using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a utility or service provider for a home.
/// Tracks company information, account details, and contact information.
/// </summary>
public class HomeUtility : BaseTenantEntity
{
    /// <summary>
    /// Reference to the home this utility belongs to
    /// </summary>
    public Guid HomeId { get; set; }

    /// <summary>
    /// Type of utility (Electric, Gas, Water, etc.)
    /// </summary>
    public UtilityType UtilityType { get; set; }

    /// <summary>
    /// Name of the utility company
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// Account number with the utility company
    /// </summary>
    public string? AccountNumber { get; set; }

    /// <summary>
    /// Customer service phone number
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Company website URL
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Email used to log into the utility account
    /// </summary>
    public string? LoginEmail { get; set; }

    /// <summary>
    /// Additional notes about this utility
    /// </summary>
    public string? Notes { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The home this utility belongs to
    /// </summary>
    public virtual Home Home { get; set; } = null!;

    #endregion
}
