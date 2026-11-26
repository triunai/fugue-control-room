namespace Fugue.Infrastructure.Webhooks;

/// <summary>
/// Exponential backoff with equal jitter: the nth failure waits between half and all of <c>min(max, base * 2^(n-1))</c>. The jitter spreads the retries of
/// many deliveries that failed together (a receiver outage) so they do not return as one wave.
/// </summary>
internal static class Backoff
{
    /// <param name="failedAttempts">How many attempts have failed so far (1 after the first failure).</param>
    /// <param name="jitter">A value in [0, 1); injectable so tests are deterministic.</param>
    public static TimeSpan Delay(int failedAttempts, WebhookOptions options, double jitter)
    {
        double exponent = Math.Min(Math.Max(failedAttempts - 1, 0), 30);
        double ceiling = Math.Min(options.MaxDelaySeconds, options.BaseDelaySeconds * Math.Pow(2, exponent));
        return TimeSpan.FromSeconds(ceiling * (0.5 + (0.5 * jitter)));
    }
}
