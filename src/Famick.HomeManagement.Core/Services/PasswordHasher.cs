using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Famick.HomeManagement.Core.Services;

/// <summary>
/// BCrypt-based password hashing service
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private readonly int _workFactor;

    public PasswordHasher(IConfiguration configuration)
    {
        _workFactor = configuration.GetValue<int>("BCryptWorkFactor", 12);

        if (_workFactor < 4 || _workFactor > 31)
        {
            throw new ArgumentException("BCrypt work factor must be between 4 and 31");
        }
    }

    /// <inheritdoc />
    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be null or whitespace", nameof(password));
        }

        return BCrypt.Net.BCrypt.HashPassword(password, _workFactor);
    }

    /// <inheritdoc />
    public bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool NeedsRehash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.PasswordNeedsRehash(hash, _workFactor);
        }
        catch
        {
            return false;
        }
    }
}
