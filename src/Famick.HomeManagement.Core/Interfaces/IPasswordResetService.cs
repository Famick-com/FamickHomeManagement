using Famick.HomeManagement.Core.DTOs.Authentication;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for handling password reset operations
/// </summary>
public interface IPasswordResetService
{
    /// <summary>
    /// Initiates a password reset request. Always returns success even if email doesn't exist (security).
    /// </summary>
    /// <param name="request">The forgot password request with email</param>
    /// <param name="ipAddress">The IP address of the client making the request</param>
    /// <param name="baseUrl">The base URL for building the reset link</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generic success response (always succeeds to prevent email enumeration)</returns>
    Task<ForgotPasswordResponse> RequestPasswordResetAsync(
        ForgotPasswordRequest request,
        string ipAddress,
        string baseUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emails a member the link to choose a new password after an admin has reset theirs.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="RequestPasswordResetAsync"/>, the caller already knows the account exists,
    /// so there is no uniform response to keep and no per-account limit to hide behind. It reports
    /// whether the email went out, so the admin can be given another way to reach the member.
    /// </remarks>
    /// <param name="userId">The member whose password was reset</param>
    /// <param name="baseUrl">The base URL for building the reset link</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True when the email was handed to the mail provider</returns>
    Task<bool> SendAdminResetAsync(
        Guid userId,
        string baseUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a password reset token
    /// </summary>
    /// <param name="token">The reset token to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with email if valid</returns>
    Task<ValidateResetTokenResponse> ValidateResetTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the user's password using a valid reset token
    /// </summary>
    /// <param name="request">The reset password request with token and new password</param>
    /// <param name="ipAddress">The IP address of the client making the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<ResetPasswordResponse> ResetPasswordAsync(
        ResetPasswordRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default);
}
