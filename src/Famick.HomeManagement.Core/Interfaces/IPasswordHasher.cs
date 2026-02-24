namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for hashing and verifying passwords using BCrypt
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plaintext password using BCrypt with configured work factor
    /// </summary>
    /// <param name="password">The plaintext password to hash</param>
    /// <returns>The BCrypt hash string</returns>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a plaintext password against a BCrypt hash
    /// </summary>
    /// <param name="password">The plaintext password to verify</param>
    /// <param name="hash">The BCrypt hash to verify against</param>
    /// <returns>True if the password matches the hash, false otherwise</returns>
    bool VerifyPassword(string password, string hash);

    /// <summary>
    /// Checks if a hash needs to be rehashed (e.g., if the work factor has changed)
    /// </summary>
    /// <param name="hash">The BCrypt hash to check</param>
    /// <returns>True if the hash should be regenerated, false otherwise</returns>
    bool NeedsRehash(string hash);
}
