using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.DTOs;

/// <summary>Data for <c>HouseholdLinked</c>: this household and another now see each other's people.</summary>
public class HouseholdLinkedData : IMessageData
{
    public string OtherHousehold { get; set; } = string.Empty;
    public string? DeepLinkUrl { get; set; }
}

/// <summary>Data for <c>HouseholdLinkBroken</c>: the other household broke the link.</summary>
public class HouseholdLinkBrokenData : IMessageData
{
    public string OtherHousehold { get; set; } = string.Empty;

    /// <summary>
    /// The copies stayed on both sides as ordinary contacts. Otherwise they were removed from
    /// both.
    /// </summary>
    public bool CopiesKept { get; set; }

    public string? DeepLinkUrl { get; set; }
}

/// <summary>
/// Data for <c>LinkedHouseholdJoined</c>: someone invited from this household's contacts accepted.
/// </summary>
public class LinkedHouseholdJoinedData : IMessageData
{
    /// <summary>The person who accepted.</summary>
    public string MemberName { get; set; } = string.Empty;

    /// <summary>Their new household.</summary>
    public string OtherHousehold { get; set; } = string.Empty;

    public string? DeepLinkUrl { get; set; }
}
