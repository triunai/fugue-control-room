using System.Security.Cryptography;
using System.Text;

namespace Fugue.Application.Features.Webhooks;

/// <summary>
/// The secret and the signature scheme, in one place so the dispatcher, the Development sink and the documentation cannot drift (ADR-011).
/// Signature: <c>v1=</c> + lower-case hex of HMAC-SHA256 with the secret string as the UTF-8 key over <c>{timestamp}.{rawBody}</c>
/// (the timestamp is unix seconds, exactly as sent in <c>X-Fugue-Timestamp</c>; the body is the raw bytes received).
/// </summary>
public static class WebhookSecretFormat
{
    public const string Prefix = "whsec_";
    public const string SignatureVersion = "v1";

    /// <summary>The replay window receivers must enforce.</summary>
    public static readonly TimeSpan ReplayWindow = TimeSpan.FromMinutes(5);

    /// <summary><c>whsec_</c> plus 32 random bytes, base64url.</summary>
    public static string Generate() =>
        Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Sign(string secret, long unixSeconds, ReadOnlySpan<byte> rawBody)
    {
        byte[] prefix = Encoding.UTF8.GetBytes(unixSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        byte[] message = new byte[prefix.Length + rawBody.Length];
        prefix.CopyTo(message, 0);
        rawBody.CopyTo(message.AsSpan(prefix.Length));
        return SignatureVersion + "=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), message)).ToLowerInvariant();
    }

    /// <summary>Constant-time comparison of the received header value with the expected signature.</summary>
    public static bool Verify(string secret, long unixSeconds, ReadOnlySpan<byte> rawBody, string? header)
    {
        if (string.IsNullOrEmpty(header))
        {
            return false;
        }

        byte[] expected = Encoding.UTF8.GetBytes(Sign(secret, unixSeconds, rawBody));
        byte[] actual = Encoding.UTF8.GetBytes(header);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
