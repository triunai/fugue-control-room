using System.Security.Cryptography;
using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Idempotency;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Idempotency;

/// <summary>
/// The Dapper write path of <c>idempotency_keys</c> (ADR-013), through <see cref="SqlExecutor"/> and OUTSIDE any unit of work: every statement here is its
/// own committed statement, so a claim is visible to a concurrent duplicate before the action runs. The claim replaces only an EXPIRED row, so a claimed key
/// is never taken over; completion and deletion are fenced by the claim token. The <c>raw-sql-writes</c> fitness rule exempts it because every table it writes maps to a
/// <c>[NoAudit]</c> entity (operational plumbing; the stored body is ciphertext). Reads go through EF.
/// </summary>
internal sealed class IdempotencyStore(
    FugueDbContext db,
    SqlExecutor sql,
    IAuditStamp stamp,
    TimeProvider time,
    IDataProtectionProvider dataProtection,
    IOptions<IdempotencyOptions> options) : IIdempotencyStore
{
    /// <summary>Versioned: change it and every stored response becomes unreadable (the replay then answers 500 and keeps the key).</summary>
    public const string Purpose = "Fugue.Idempotency.v1";

    private const int ClaimAttempts = 3;

    private const string ClaimSql = """
        INSERT INTO idempotency_keys (id, actor_id, endpoint, key, request_hash, claim_token, status, response_status, response_headers, response_body,
                                      expires_at, created_at, created_by, updated_at, updated_by)
        VALUES (@Id, @ActorId, @Endpoint, @Key, @RequestHash, @ClaimToken, 'pending', NULL, NULL, NULL, @ExpiresAt, @Now, @Actor, @Now, @Actor)
        ON CONFLICT (actor_id, endpoint, key) DO UPDATE SET
            id = EXCLUDED.id, request_hash = EXCLUDED.request_hash, claim_token = EXCLUDED.claim_token, status = 'pending',
            response_status = NULL, response_headers = NULL, response_body = NULL, expires_at = EXCLUDED.expires_at,
            created_at = EXCLUDED.created_at, created_by = EXCLUDED.created_by, updated_at = EXCLUDED.updated_at, updated_by = EXCLUDED.updated_by
        WHERE idempotency_keys.expires_at < @Now
        RETURNING claim_token
        """;

    private const string CompleteSql = """
        UPDATE idempotency_keys
        SET status = 'completed', response_status = @Status, response_headers = CAST(@Headers AS jsonb), response_body = @Body, updated_at = @Now, updated_by = @Actor
        WHERE id = @Id AND claim_token = @ClaimToken AND status = 'pending'
        """;

    private const string ReleaseSql = "DELETE FROM idempotency_keys WHERE id = @Id AND claim_token = @ClaimToken";

    private const string PurgeSql = """
        DELETE FROM idempotency_keys
        WHERE id IN (SELECT id FROM idempotency_keys WHERE expires_at < @Now ORDER BY expires_at LIMIT @Batch)
        """;

    private readonly IDataProtector _protector = dataProtection.CreateProtector(Purpose);

    public async Task<IdempotencyClaim> ClaimAsync(Guid actorId, string endpoint, string key, byte[] requestHash, CancellationToken ct)
    {
        IdempotencyOptions settings = options.Value;
        for (int attempt = 0; attempt < ClaimAttempts; attempt++)
        {
            AuditStampValue now = stamp.Capture();
            Guid token = Guid.NewGuid();
            var id = IdempotencyKeyId.New();
            IReadOnlyList<Guid> claimed = await sql.QueryAsync<Guid>(
                ClaimSql,
                new
                {
                    Id = id,
                    ActorId = actorId,
                    Endpoint = endpoint,
                    Key = key,
                    RequestHash = requestHash,
                    ClaimToken = token,
                    ExpiresAt = now.Now.AddHours(settings.RetentionHours),
                    now.Now,
                    now.Actor,
                },
                ct);
            if (claimed.Count == 1)
            {
                return new IdempotencyClaim(IdempotencyDecision.Execute, id.Value, token);
            }

            // Somebody else holds a live row. A concurrent release can delete it between the conflict and this read: try the claim again.
            IdempotencyRecord? existing = await db.IdempotencyKeys.AsNoTracking()
                .FirstOrDefaultAsync(r => r.ActorId == actorId && r.Endpoint == endpoint && r.Key == key, ct);
            if (existing is null || existing.ExpiresAt < now.Now)
            {
                continue;
            }

            return Decide(existing, requestHash, now.Now, settings);
        }

        return new IdempotencyClaim(IdempotencyDecision.InProgress);
    }

    public async Task<bool> CompleteAsync(Guid recordId, Guid claimToken, int status, string headersJson, byte[] body, CancellationToken ct)
    {
        AuditStampValue now = stamp.Capture();
        int rows = await sql.ExecuteAsync(
            CompleteSql,
            new { Id = new IdempotencyKeyId(recordId), ClaimToken = claimToken, Status = status, Headers = headersJson, Body = _protector.Protect(body), now.Now, now.Actor },
            ct);
        return rows == 1;
    }

    public async Task<bool> ReleaseAsync(Guid recordId, Guid claimToken, CancellationToken ct) =>
        await sql.ExecuteAsync(ReleaseSql, new { Id = new IdempotencyKeyId(recordId), ClaimToken = claimToken }, ct) == 1;

    public Task<int> PurgeExpiredAsync(int batchSize, CancellationToken ct) =>
        sql.ExecuteAsync(PurgeSql, new { Now = time.GetUtcNow(), Batch = batchSize }, ct);

    private IdempotencyClaim Decide(IdempotencyRecord row, byte[] requestHash, DateTimeOffset now, IdempotencyOptions settings)
    {
        if (!CryptographicOperations.FixedTimeEquals(row.RequestHash, requestHash))
        {
            return new IdempotencyClaim(IdempotencyDecision.KeyReused);
        }

        if (row.Status == IdempotencyStatuses.Completed && row.ResponseStatus is { } status && row.ResponseBody is { } ciphertext)
        {
            try
            {
                return new IdempotencyClaim(IdempotencyDecision.Replay, row.Id.Value, ResponseStatus: status, ResponseHeaders: row.ResponseHeaders, ResponseBody: _protector.Unprotect(ciphertext));
            }
            catch (CryptographicException)
            {
                return new IdempotencyClaim(IdempotencyDecision.ReplayUnavailable);
            }
        }

        return now - row.UpdatedAt < TimeSpan.FromSeconds(settings.InProgressSeconds)
            ? new IdempotencyClaim(IdempotencyDecision.InProgress)
            : new IdempotencyClaim(IdempotencyDecision.OutcomeUnknown);
    }
}
