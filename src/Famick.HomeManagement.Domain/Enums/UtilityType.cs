namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Defines the types of utilities and services that can be tracked for a home.
/// </summary>
public enum UtilityType
{
    /// <summary>
    /// Electric utility provider
    /// </summary>
    Electric = 0,

    /// <summary>
    /// Natural gas utility provider
    /// </summary>
    Gas = 1,

    /// <summary>
    /// Water and sewer utility provider
    /// </summary>
    WaterSewer = 2,

    /// <summary>
    /// Trash and recycling service provider
    /// </summary>
    TrashRecycling = 3,

    /// <summary>
    /// Internet service provider
    /// </summary>
    Internet = 4,

    /// <summary>
    /// TV or streaming bundle provider
    /// </summary>
    TvStreaming = 5,

    /// <summary>
    /// Home security system provider
    /// </summary>
    SecuritySystem = 6,

    /// <summary>
    /// HOA dues payment portal
    /// </summary>
    HoaDuesPortal = 7
}
