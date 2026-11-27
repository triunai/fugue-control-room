using System.Security.Cryptography;
using System.Text;
using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fugue.Infrastructure.Identity;

/// <summary>
/// Inserts <c>access_audit</c> rows with Dapper through <see cref="SqlExecutor"/> (the insert path of ADR-006), so a row written inside a
/// unit of work joins its transaction. The email is stored only as an HMAC-SHA256 of its canonical form keyed by the signing key
/// (repeated attempts correlate; the address cannot be recovered by a dictionary without the key).
/// </summary>
internal sealed class AccessAuditWriter(
    SqlExecutor sql, IAuditStamp stamp, ICorrelationContext correlation, IConfiguration configuration, ILogger<AccessAuditWriter> logger)
    : IAccessAuditWriter
{
    private static readonly Action<ILogger, string, Exception?> WriteFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1), "Could not write an access_audit row for {Event}");

    private const string InsertSql = """
        INSERT INTO access_audit (id, occurred_at, event, outcome, reason_code, user_id, email_hash, ip, user_agent, correlation_id)
        VALUES (@Id, @OccurredAt, @Event, @Outcome, @ReasonCode, @UserId, @EmailHash, @Ip, @UserAgent, @CorrelationId)
        """;

    public async Task WriteAsync(AccessAuditEntry entry, CancellationToken ct)
    {
        try
        {
            await sql.ExecuteAsync(
                InsertSql,
                new
                {
                    Id = AccessAuditId.New(),
                    OccurredAt = stamp.Capture().Now,
                    entry.Event,
                    entry.Outcome,
                    ReasonCode = Fit(entry.ReasonCode, 64),
                    UserId = entry.UserId?.Value,
                    EmailHash = entry.Email is null ? null : HashEmail(entry.Email),
                    Ip = Fit(entry.Client?.Ip, 64),
                    UserAgent = Fit(entry.Client?.UserAgent, 256),
                    CorrelationId = Fit(correlation.CorrelationId, 64),
                },
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AuditMetrics.RecordWriteFailure(AuditMetrics.AccessAuditTable, ex.GetType().Name);
            WriteFailed(logger, entry.Event, ex);
        }
    }

    /// <summary>
    /// Clips a value to its column width. An over-long reason (a permission or module code is caller-influenced) would otherwise fail the
    /// insert with 22001, and a best-effort writer would swallow that and lose the row.
    /// </summary>
    internal static string? Fit(string? value, int max) => value is { } v && v.Length > max ? v[..max] : value;

    /// <summary>
    /// HMAC of the canonical email under a SUBKEY derived from the signing key (never the signing key itself, so a leaked hash set says
    /// nothing about token signing). With no usable signing key no hash is stored: a constant fallback key would make the hashes
    /// reversible by a dictionary.
    /// </summary>
    private string? HashEmail(string email)
    {
        string? key = configuration["Auth:SigningKey"];
        if (key is null || TryDecode(key) is not { } keyBytes)
        {
            return null;
        }

        byte[] subkey = HMACSHA256.HashData(keyBytes, Encoding.UTF8.GetBytes(SubkeyLabel));
        return Convert.ToHexString(HMACSHA256.HashData(subkey, Encoding.UTF8.GetBytes(EmailKey.Canonicalize(email)))).ToLowerInvariant();
    }

    /// <summary>The derivation label; changing it changes every future hash (a new version suffix).</summary>
    internal const string SubkeyLabel = "fugue:access-audit:email:v1";

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
