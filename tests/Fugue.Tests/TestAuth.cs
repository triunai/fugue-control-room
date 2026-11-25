using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Fugue.Tests;

/// <summary>Auth settings every test host needs: a fresh random signing key, a cheap password hash, generous rate limits.</summary>
internal static class TestAuth
{
    public const string Password = "Test-Passw0rd!"; // secrets-scan:ignore

    public static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static IEnumerable<KeyValuePair<string, string>> Settings(string key, int ipPermits = 100_000, int emailPermits = 100_000) =>
    [
        new("Auth:SigningKey", key),
        new("Auth:PasswordHashIterations", "1000"),
        new("RateLimiting:Auth:PerIpPermits", ipPermits.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        new("RateLimiting:Auth:PerEmailPermits", emailPermits.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        new("RateLimiting:Auth:PerEmailConcurrency", "64"),
        new("Webhooks:Dispatcher:Enabled", "false"), // tests drive the dispatcher by hand (or not at all); a background loop would race them
    ];

    public static void Apply(IWebHostBuilder builder, string key)
    {
        foreach (KeyValuePair<string, string> setting in Settings(key))
        {
            builder.UseSetting(setting.Key, setting.Value);
        }
    }

    /// <summary>The probe controller has no permission attributes; drop the fallback policy for those hosts only.</summary>
    public static void OpenFallback(IServiceCollection services) =>
        services.Configure<AuthorizationOptions>(o => o.FallbackPolicy = null);
}
