using Fugue.Domain.Abstractions;
using Fugue.Domain.Audit;
using Fugue.Domain.Ids;

namespace Fugue.Domain.Idempotency;

/// <summary>
/// One claimed <c>Idempotency-Key</c> (ADR-013), scoped to (<see cref="ActorId"/>, <see cref="Endpoint"/>, <see cref="Key"/>). A row is claimed
/// <c>pending</c> BEFORE the action runs and completed after it, both fenced by <see cref="ClaimToken"/>. <c>[NoAudit]</c>: this is operational
/// plumbing and the stored response is ciphertext; claim, completion and purge must write no <c>audit_log</c> rows.
/// </summary>
[NoAudit]
public sealed class IdempotencyRecord : IAuditable
{
    public IdempotencyKeyId Id { get; set; }

    public Guid ActorId { get; set; }

    /// <summary>The HTTP method and the route template, for example <c>POST api/v1/customers</c>.</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    /// <summary>SHA-256 of the raw request body.</summary>
    public byte[] RequestHash { get; set; } = [];

    /// <summary>Random per claim. Completion and deletion must present it, so a request that lost its claim can never touch a newer one.</summary>
    public Guid ClaimToken { get; set; }

    public string Status { get; set; } = IdempotencyStatuses.Pending;

    public int? ResponseStatus { get; set; }

    /// <summary>The allowlisted response headers as a JSON object (jsonb).</summary>
    public string? ResponseHeaders { get; set; }

    /// <summary>The response body, encrypted with Data Protection.</summary>
    public byte[]? ResponseBody { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;
}

public static class IdempotencyStatuses
{
    public const string Pending = "pending";
    public const string Completed = "completed";
}
