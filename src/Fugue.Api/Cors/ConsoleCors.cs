using Fugue.Api.Correlation;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace Fugue.Api.Cors;

/// <summary>
/// <c>Cors</c> configuration. <see cref="Origins"/> are the exact browser origins allowed to call the API (scheme, host, optional port;
/// no path, no wildcard). In Development the console's dev server (<c>http://localhost:4200</c>) is always allowed in addition.
/// </summary>
public sealed class ApiCorsOptions
{
    public const string Section = "Cors";

    public string[] Origins { get; set; } = [];
}

/// <summary>Fails the host at start when an origin is not an exact origin: a typo must not silently widen or narrow the policy.</summary>
public sealed class ApiCorsOptionsValidator : IValidateOptions<ApiCorsOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiCorsOptions options)
    {
        var failures = new List<string>();
        foreach (string origin in options.Origins)
        {
            if (origin == "*" || !Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri)
                || uri.Scheme is not ("http" or "https") || uri.PathAndQuery != "/" || origin.EndsWith('/') || uri.UserInfo.Length > 0)
            {
                failures.Add($"Cors:Origins entry '{origin}' must be an exact origin such as https://console.example.com (scheme, host, optional port; no wildcard, path or trailing slash).");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>
/// The browser policy for the Fugue Console (D-043). The console authenticates with a bearer token in the <c>Authorization</c> header, never a cookie,
/// so credentials are NOT allowed: a stolen cookie-style ambient credential is not part of this design. Only the listed origins, the methods and
/// request headers the API uses, and the response headers a client must read (the ones that carry versions, paging, correlation and retry hints).
/// </summary>
public static class ConsoleCors
{
    public const string PolicyName = "Console";

    /// <summary>The Angular dev server; allowed only in Development.</summary>
    public const string DevOrigin = "http://localhost:4200";

    public static readonly string[] AllowedRequestHeaders =
        ["Authorization", "Content-Type", "If-Match", "Idempotency-Key", CorrelationHeaders.CorrelationId, CorrelationHeaders.SagaId];

    public static readonly string[] AllowedMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    public static readonly string[] ExposedResponseHeaders =
        ["ETag", "Link", "X-Next-Cursor", CorrelationHeaders.CorrelationId, CorrelationHeaders.SagaId, "Retry-After", "Location"];

    public static IServiceCollection AddConsoleCors(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<ApiCorsOptions>().Bind(configuration.GetSection(ApiCorsOptions.Section)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ApiCorsOptions>, ApiCorsOptionsValidator>();
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<ApiCorsOptions>>((cors, api) =>
        {
            string[] origins = [.. api.Value.Origins.Concat(environment.IsDevelopment() ? [DevOrigin] : []).Distinct(StringComparer.OrdinalIgnoreCase)];
            cors.AddPolicy(PolicyName, policy => policy
                .WithOrigins(origins)
                .WithMethods(AllowedMethods)
                .WithHeaders(AllowedRequestHeaders)
                .WithExposedHeaders(ExposedResponseHeaders)
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
        });
        return services;
    }
}
