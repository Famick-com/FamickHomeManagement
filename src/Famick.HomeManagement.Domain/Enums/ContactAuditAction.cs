namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Actions tracked in contact audit logs
/// </summary>
public enum ContactAuditAction
{
    // Contact lifecycle
    Created = 1,
    Updated = 2,
    Deleted = 3,
    Restored = 4,

    // Address changes
    AddressAdded = 10,
    AddressRemoved = 11,
    AddressUpdated = 12,
    PrimaryAddressChanged = 13,

    // Phone changes
    PhoneAdded = 20,
    PhoneRemoved = 21,
    PhoneUpdated = 22,
    PrimaryPhoneChanged = 23,

    // Social media changes
    SocialMediaAdded = 30,
    SocialMediaRemoved = 31,
    SocialMediaUpdated = 32,

    // Relationship changes
    RelationshipAdded = 40,
    RelationshipRemoved = 41,
    RelationshipUpdated = 42,

    // Tag changes
    TagAdded = 50,
    TagRemoved = 51,

    // Visibility/Sharing changes
    VisibilityChanged = 60,
    SharedWithUser = 61,
    UnsharedFromUser = 62,

    // Email changes
    EmailAdded = 70,
    EmailRemoved = 71,
    EmailUpdated = 72,
    PrimaryEmailChanged = 73,

    // Profile image changes
    ProfileImageUpdated = 80,
    ProfileImageRemoved = 81
}
