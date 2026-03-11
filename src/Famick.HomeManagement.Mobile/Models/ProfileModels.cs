namespace Famick.HomeManagement.Mobile.Models;

#region Profile

public class UserProfileMobile
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PreferredLanguage { get; set; }
    public bool HasPassword { get; set; }
    public Guid? ContactId { get; set; }
    public ProfileContactMobile? Contact { get; set; }
}

public class ProfileContactMobile
{
    public string? ProfileImageUrl { get; set; }
    public string? GravatarUrl { get; set; }
    public bool UseGravatar { get; set; } = true;
    public List<ProfilePhoneNumberMobile> PhoneNumbers { get; set; } = new();
    public List<ProfileEmailAddressMobile> EmailAddresses { get; set; } = new();
}

public class ProfilePhoneNumberMobile
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Label { get; set; }
    public bool IsPrimary { get; set; }
}

public class ProfileEmailAddressMobile
{
    public string Email { get; set; } = string.Empty;
    public string? Label { get; set; }
    public bool IsPrimary { get; set; }
}

public class UpdateProfileMobileRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PreferredLanguage { get; set; }
}

#endregion

#region Linked Accounts

public class LinkedAccountMobile
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderDisplayName { get; set; } = string.Empty;
    public string? ProviderEmail { get; set; }
    public DateTime LinkedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

#endregion

#region Calendar Subscriptions

public class ExternalCalendarSubscriptionMobile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IcsUrl { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int SyncIntervalMinutes { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public string? LastSyncStatus { get; set; }
    public bool IsActive { get; set; }
    public int EventCount { get; set; }
}

public class CreateCalendarSubscriptionMobileRequest
{
    public string Name { get; set; } = string.Empty;
    public string IcsUrl { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int SyncIntervalMinutes { get; set; } = 60;
}

#endregion

#region ICS Tokens

public class IcsTokenMobile
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string? Label { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? FeedUrl { get; set; }
}

public class CreateIcsTokenMobileRequest
{
    public string? Label { get; set; }
}

#endregion

#region VCF Tokens

public class VcfTokenMobile
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string? Label { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? FeedUrl { get; set; }
}

public class CreateVcfTokenMobileRequest
{
    public string? Label { get; set; }
}

#endregion

#region Contact Sync

public class ContactSyncResult
{
    public bool Success { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Deleted { get; set; }
    public int Failed { get; set; }
    public string? ErrorMessage { get; set; }

    public static ContactSyncResult Ok(int created, int updated, int deleted) => new()
    {
        Success = true, Created = created, Updated = updated, Deleted = deleted
    };

    public static ContactSyncResult Fail(string error) => new()
    {
        Success = false, ErrorMessage = error
    };
}

public class ContactSyncStatus
{
    public int SyncedCount { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public bool HasPermission { get; set; }
}

#endregion
