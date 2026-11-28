using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Caching;

/// <summary><c>Cache:</c> configuration for the in-process HybridCache (L1 only: there is no Redis in compose, so no L2).</summary>
public sealed class CacheOptions
{
    public const string Section = "Cache";

    /// <summary>How long a user's <c>/me</c> answer (effective permissions, profile, modules) lives. Writes invalidate it sooner.</summary>
    public int MePermissionsTtlSeconds { get; set; } = 30;

    /// <summary>Upper bound for the in-memory cache, in bytes as HybridCache sizes its entries.</summary>
    public long L1SizeLimitBytes { get; set; } = 64L * 1024 * 1024;
}

internal sealed class CacheOptionsValidator : IValidateOptions<CacheOptions>
{
    public ValidateOptionsResult Validate(string? name, CacheOptions options)
    {
        var errors = new List<string>();
        if (options.MePermissionsTtlSeconds is < 1 or > 300)
        {
            errors.Add("Cache:MePermissionsTtlSeconds must be between 1 and 300.");
        }

        if (options.L1SizeLimitBytes < 1024 * 1024)
        {
            errors.Add("Cache:L1SizeLimitBytes must be at least 1048576.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
