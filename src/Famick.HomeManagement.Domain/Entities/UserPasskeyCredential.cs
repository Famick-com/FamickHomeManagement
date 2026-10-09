using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a WebAuthn/FIDO2 passkey credential registered for a user.
/// Enables passwordless authentication via biometrics, security keys, etc.
/// </summary>
public class UserPasskeyCredential : BaseEntity, ITenantEntity
{
    /// <summary>
    /// ID of the tenant this passkey credential belongs to
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// ID of the user this passkey is registered to
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The credential ID returned by the authenticator (Base64 encoded)
    /// </summary>
    public string CredentialId { get; set; } = string.Empty;

    /// <summary>
    /// The public key for this credential (Base64 encoded)
    /// </summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>
    /// Signature counter for replay attack prevention.
    /// Incremented with each authentication to detect cloned authenticators.
    /// </summary>
    public uint SignatureCounter { get; set; }

    /// <summary>
    /// User-defined name for this passkey (e.g., "MacBook Touch ID", "iPhone Face ID")
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// Authenticator Attestation GUID - identifies the authenticator type
    /// </summary>
    public string? AaGuid { get; set; }

    /// <summary>
    /// The credential type (typically "public-key")
    /// </summary>
    public string CredentialType { get; set; } = "public-key";

    /// <summary>
    /// The WebAuthn relying-party ID this credential was registered under.
    /// </summary>
    /// <remarks>
    /// A server can serve two relying parties: its own hostname for browser ceremonies, and
    /// <c>PasskeySettings.NativeRelyingPartyId</c> for the native mobile app, which can only be
    /// associated with a domain Famick controls. A credential is cryptographically bound to one RP
    /// ID, so a browser-created credential cannot be asserted by the app and vice versa. Recording
    /// which one lets a ceremony offer only the credentials it can actually verify.
    ///
    /// Null on rows created before this column existed. Those necessarily came from a browser, so
    /// null reads as the web relying party rather than being backfilled. On a deployment where both
    /// RP IDs are the same string — the cloud app, where both are <c>app.famick.com</c> — the value
    /// never distinguishes anything and no filtering occurs.
    /// </remarks>
    public string? RelyingPartyId { get; set; }

    /// <summary>
    /// Whether user verification (biometric/PIN) was performed during registration
    /// </summary>
    public bool UserVerification { get; set; }

    /// <summary>
    /// When this passkey was last used for authentication
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// The user this passkey is registered to
    /// </summary>
    public User User { get; set; } = null!;
}
