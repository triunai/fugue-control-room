using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Webhooks;

/// <summary><c>Webhooks:</c> configuration (ADR-011). Defaults are production-safe; tests and demos override the timings.</summary>
public sealed class WebhookOptions
{
    public const string Section = "Webhooks";

    /// <summary>
    /// Allows http and loopback or private destinations (a local receiver, the Development sink). Link-local addresses (the cloud metadata
    /// service) stay blocked even then. The host refuses to start with this on outside Development.
    /// </summary>
    public bool AllowInsecureLocal { get; set; }

    /// <summary>Total attempts (the first send included) before a delivery, or a failing relay publish, is dead-lettered.</summary>
    public int MaxAttempts { get; set; } = 8;

    /// <summary>Backoff after the first failure; it doubles per attempt (with jitter) up to <see cref="MaxDelaySeconds"/>.</summary>
    public double BaseDelaySeconds { get; set; } = 10;

    public double MaxDelaySeconds { get; set; } = 3600;

    /// <summary>The send timeout of one attempt (the standard resilience handler's attempt timeout).</summary>
    public int AttemptTimeoutSeconds { get; set; } = 10;

    /// <summary>How long a claimed delivery is reserved before another worker may take it. Must exceed the longest batch, see <see cref="DispatcherOptions"/>.</summary>
    public int LeaseSeconds { get; set; } = 120;

    public DispatcherOptions Dispatcher { get; set; } = new();
}

public sealed class DispatcherOptions
{
    /// <summary>False runs no background dispatcher in this process (an API-only node, or a test that drives the dispatcher by hand).</summary>
    public bool Enabled { get; set; } = true;

    public int PollIntervalMs { get; set; } = 1000;

    public int BatchSize { get; set; } = 20;

    /// <summary>Concurrent sends per cycle. BatchSize / this x AttemptTimeoutSeconds should stay well below <c>LeaseSeconds</c>.</summary>
    public int MaxConcurrentSends { get; set; } = 8;
}

internal sealed class WebhookOptionsValidator(IHostEnvironment environment) : IValidateOptions<WebhookOptions>
{
    public ValidateOptionsResult Validate(string? name, WebhookOptions options)
    {
        var errors = new List<string>();
        if (options.AllowInsecureLocal && !environment.IsDevelopment())
        {
            errors.Add("Webhooks:AllowInsecureLocal can only be true in the Development environment.");
        }

        if (options.MaxAttempts < 1)
        {
            errors.Add("Webhooks:MaxAttempts must be at least 1.");
        }

        if (options.BaseDelaySeconds <= 0 || options.MaxDelaySeconds < options.BaseDelaySeconds)
        {
            errors.Add("Webhooks:BaseDelaySeconds must be positive and not above MaxDelaySeconds.");
        }

        if (options.AttemptTimeoutSeconds < 1 || options.LeaseSeconds <= options.AttemptTimeoutSeconds)
        {
            errors.Add("Webhooks:LeaseSeconds must exceed AttemptTimeoutSeconds, which must be at least 1.");
        }

        if (options.Dispatcher.BatchSize < 1 || options.Dispatcher.MaxConcurrentSends < 1 || options.Dispatcher.PollIntervalMs < 10)
        {
            errors.Add("Webhooks:Dispatcher needs BatchSize >= 1, MaxConcurrentSends >= 1 and PollIntervalMs >= 10.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
