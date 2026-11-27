namespace Fugue.Application.Abstractions;

/// <summary>What <see cref="IIdempotencyStore.ClaimAsync"/> decided (ADR-013).</summary>
public enum IdempotencyDecision
{
    /// <summary>The caller owns the key: run the action, then complete or release with the returned token.</summary>
    Execute,

    /// <summary>The same request already completed: answer with the stored response.</summary>
    Replay,

    /// <summary>The key was used with a different request body (422).</summary>
    KeyReused,

    /// <summary>The first request is still running (409 with Retry-After).</summary>
    InProgress,

    /// <summary>The first request was claimed long ago and never completed: it may or may not have committed (409, no automatic retry).</summary>
    OutcomeUnknown,

    /// <summary>A completed response exists but cannot be decrypted (500); the key is kept.</summary>
    ReplayUnavailable,
}

/// <summary>The result of a claim. <see cref="RecordId"/> and <see cref="ClaimToken"/> are set for <see cref="IdempotencyDecision.Execute"/>; the response parts for <see cref="IdempotencyDecision.Replay"/>.</summary>
public sealed record IdempotencyClaim(
    IdempotencyDecision Decision,
    Guid RecordId = default,
    Guid ClaimToken = default,
    int ResponseStatus = 0,
    string? ResponseHeaders = null,
    byte[]? ResponseBody = null);

/// <summary>
/// The idempotency key store (ADR-013). Primitive types only: HTTP concerns (headers, status, bytes) stay in <c>Fugue.Api</c>.
/// A claimed key is never reclaimed: an ambiguous outcome is answered, never re-run.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Claims (<paramref name="actorId"/>, <paramref name="endpoint"/>, <paramref name="key"/>) atomically in its own committed statement, replacing only an
    /// EXPIRED row. Otherwise reads the existing row and decides replay, reuse, in progress or outcome unknown.
    /// </summary>
    Task<IdempotencyClaim> ClaimAsync(Guid actorId, string endpoint, string key, byte[] requestHash, CancellationToken ct);

    /// <summary>Stores the first 2xx response (body encrypted) fenced by <paramref name="claimToken"/>. False when the claim is no longer ours.</summary>
    Task<bool> CompleteAsync(Guid recordId, Guid claimToken, int status, string headersJson, byte[] body, CancellationToken ct);

    /// <summary>Deletes the claim (a business 4xx: nothing committed) so a corrected retry can run. Fenced by <paramref name="claimToken"/>.</summary>
    Task<bool> ReleaseAsync(Guid recordId, Guid claimToken, CancellationToken ct);

    /// <summary>Deletes up to <paramref name="batchSize"/> expired rows and returns how many went.</summary>
    Task<int> PurgeExpiredAsync(int batchSize, CancellationToken ct);
}
