namespace Famick.HomeManagement.Core.DTOs.ExternalAuth;

/// <summary>
/// Request for passkey registration options (new user or adding to existing account)
/// </summary>
public class PasskeyRegisterOptionsRequest
{
    /// <summary>
    /// Email for new user registration (not required if authenticated)
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// First name for new user registration
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Last name for new user registration
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Optional device name for the passkey
    /// </summary>
    public string? DeviceName { get; set; }
}

/// <summary>
/// Response containing WebAuthn registration options
/// </summary>
public class PasskeyRegisterOptionsResponse
{
    /// <summary>
    /// Serialized WebAuthn PublicKeyCredentialCreationOptions (JSON)
    /// </summary>
    public string Options { get; set; } = string.Empty;

    /// <summary>
    /// Session identifier for correlating registration flow
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
}

/// <summary>
/// Request to verify passkey registration
/// </summary>
public class PasskeyRegisterVerifyRequest
{
    /// <summary>
    /// Session identifier from options response
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Serialized AuthenticatorAttestationResponse (JSON)
    /// </summary>
    public string AttestationResponse { get; set; } = string.Empty;

    /// <summary>
    /// Optional device name for the passkey
    /// </summary>
    public string? DeviceName { get; set; }
}

/// <summary>
/// Response after successful passkey registration (includes tokens if new user)
/// </summary>
public class PasskeyRegisterVerifyResponse
{
    /// <summary>
    /// Whether registration was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// ID of the registered credential
    /// </summary>
    public Guid? CredentialId { get; set; }

    /// <summary>
    /// JWT access token (for new user registration)
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Refresh token (for new user registration)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// When the access token expires (for new user registration)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Error message if registration failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request for passkey authentication options
/// </summary>
public class PasskeyAuthenticateOptionsRequest
{
    /// <summary>
    /// Optional email to pre-filter allowed credentials
    /// </summary>
    public string? Email { get; set; }
}

/// <summary>
/// Response containing WebAuthn authentication options
/// </summary>
public class PasskeyAuthenticateOptionsResponse
{
    /// <summary>
    /// Serialized WebAuthn PublicKeyCredentialRequestOptions (JSON)
    /// </summary>
    public string Options { get; set; } = string.Empty;

    /// <summary>
    /// Session identifier for correlating authentication flow
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
}

/// <summary>
/// Request to verify passkey authentication
/// </summary>
public class PasskeyAuthenticateVerifyRequest
{
    /// <summary>
    /// Session identifier from options response
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Serialized AuthenticatorAssertionResponse (JSON)
    /// </summary>
    public string AssertionResponse { get; set; } = string.Empty;

    /// <summary>
    /// Whether to extend the refresh token expiration
    /// </summary>
    public bool RememberMe { get; set; }
}

/// <summary>
/// Information about a registered passkey credential
/// </summary>
public class PasskeyCredentialDto
{
    /// <summary>
    /// Unique identifier for this credential
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User-defined name for this passkey
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// When this passkey was registered
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this passkey was last used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Authenticator type identifier
    /// </summary>
    public string? AaGuid { get; set; }
}

/// <summary>
/// Request to rename a passkey
/// </summary>
public class PasskeyRenameRequest
{
    /// <summary>
    /// New name for the passkey
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;
}
