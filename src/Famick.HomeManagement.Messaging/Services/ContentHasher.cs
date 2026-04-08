using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.Services;

/// <summary>
/// Computes a deterministic SHA256 hash of an IMessageData instance
/// by serializing its public properties to JSON.
/// </summary>
public static class ContentHasher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string ComputeHash(IMessageData data)
    {
        var json = JsonSerializer.Serialize(data, data.GetType(), JsonOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(hashBytes);
    }
}
