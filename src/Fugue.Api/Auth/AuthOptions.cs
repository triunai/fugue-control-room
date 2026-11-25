using Microsoft.Extensions.Options;

namespace Fugue.Api.Auth;

/// <summary>
/// <c>Auth:*</c> settings. The signing key is the base64 of at least 32 cryptographically random bytes
/// (<c>openssl rand -base64 32</c>), supplied by user-secrets or the environment, never by appsettings (Codex finding 10).
/// </summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";
    public const int MinKeyBytes = 32;

    public string Issuer { get; set; } = "fugue";

    public string Audience { get; set; } = "fugue-api";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int ClockSkewSeconds { get; set; } = 30;

    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>The decoded key. Only valid after <see cref="AuthOptionsValidator"/> has passed.</summary>
    public byte[] KeyBytes() => Convert.FromBase64String(SigningKey);
}

/// <summary>Startup validation (<c>ValidateOnStart</c>): a missing, undecodable, short, repetitive or placeholder key stops the host.</summary>
public sealed class AuthOptionsValidator : IValidateOptions<AuthOptions>
{
    /// <summary>Documented placeholder spellings that must never reach a running host.</summary>
    private static readonly string[] Placeholders = ["REPLACE", "CHANGE", "SECRET", "PASSWORD", "EXAMPLE", "PLACEHOLDER", "TODO"];

    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Auth:Issuer and Auth:Audience are required.");
        }

        if (options.AccessTokenMinutes is < 1 or > 60)
        {
            failures.Add("Auth:AccessTokenMinutes must be between 1 and 60.");
        }

        if (options.RefreshTokenDays is < 1 or > 90)
        {
            failures.Add("Auth:RefreshTokenDays must be between 1 and 90.");
        }

        failures.AddRange(ValidateKey(options.SigningKey));
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    public static IEnumerable<string> ValidateKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            yield return "Auth:SigningKey is not set. Generate one with `openssl rand -base64 32` and supply it via user-secrets or Auth__SigningKey.";
            yield break;
        }

        byte[]? bytes = TryDecode(key);
        if (bytes is null)
        {
            yield return "Auth:SigningKey must be base64 (openssl rand -base64 32).";
            yield break;
        }

        if (bytes.Length < AuthOptions.MinKeyBytes)
        {
            yield return $"Auth:SigningKey decodes to {bytes.Length} bytes; at least {AuthOptions.MinKeyBytes} random bytes are required.";
        }

        string text = System.Text.Encoding.ASCII.GetString(bytes).ToUpperInvariant();
        if (Placeholders.Any(text.Contains) || Placeholders.Any(key.ToUpperInvariant().Contains))
        {
            yield return "Auth:SigningKey looks like a placeholder, not random bytes.";
        }

        if (bytes.Distinct().Count() < 8)
        {
            yield return "Auth:SigningKey has too little entropy (fewer than 8 distinct byte values).";
        }
    }

    private static byte[]? TryDecode(string key)
    {
        try
        {
            return Convert.FromBase64String(key);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
