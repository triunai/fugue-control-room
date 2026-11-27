using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Idempotency;

/// <summary><c>Idempotency:</c> configuration (ADR-013). Defaults are production-safe.</summary>
public sealed class IdempotencyOptions
{
    public const string Section = "Idempotency";

    /// <summary>How long a key (and its stored response) lives after it is claimed.</summary>
    public int RetentionHours { get; set; } = 24;

    /// <summary>A pending claim younger than this answers 409 <c>idempotency.in_progress</c>; older answers 409 <c>idempotency.outcome_unknown</c>.</summary>
    public int InProgressSeconds { get; set; } = 60;

    /// <summary>The largest request body an idempotent action accepts (it is buffered to fingerprint it); larger is 413.</summary>
    public int MaxRequestBytes { get; set; } = 64 * 1024;

    /// <summary>The largest response stored for replay; a larger one leaves the key pending (outcome unknown).</summary>
    public int MaxResponseBytes { get; set; } = 256 * 1024;

    public int PurgeIntervalMinutes { get; set; } = 60;

    public int PurgeBatchSize { get; set; } = 500;
}

internal sealed class IdempotencyOptionsValidator : IValidateOptions<IdempotencyOptions>
{
    public ValidateOptionsResult Validate(string? name, IdempotencyOptions options)
    {
        var errors = new List<string>();
        if (options.RetentionHours < 1)
        {
            errors.Add("Idempotency:RetentionHours must be at least 1.");
        }

        if (options.InProgressSeconds < 1)
        {
            errors.Add("Idempotency:InProgressSeconds must be at least 1.");
        }

        if (options.MaxRequestBytes is < 1 or > 1024 * 1024)
        {
            errors.Add("Idempotency:MaxRequestBytes must be between 1 and 1048576.");
        }

        if (options.MaxResponseBytes < 1)
        {
            errors.Add("Idempotency:MaxResponseBytes must be at least 1.");
        }

        if (options.PurgeIntervalMinutes < 1 || options.PurgeBatchSize < 1)
        {
            errors.Add("Idempotency:PurgeIntervalMinutes and PurgeBatchSize must be at least 1.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
