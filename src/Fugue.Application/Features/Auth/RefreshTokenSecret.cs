using System.Security.Cryptography;
using System.Text;

namespace Fugue.Application.Features.Auth;

/// <summary>An opaque 256-bit refresh token and the SHA-256 hex digest that is the only thing stored.</summary>
public static class RefreshTokenSecret
{
    public static (string Value, string Hash) Generate()
    {
        string value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (value, Hash(value));
    }

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
