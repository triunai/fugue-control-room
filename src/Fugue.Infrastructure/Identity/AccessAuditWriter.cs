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
                    entry.ReasonCode,
                    UserId = entry.UserId?.Value,
                    EmailHash = entry.Email is null ? null : HashEmail(entry.Email),
                    Ip = entry.Client?.Ip,
                    UserAgent = entry.Client?.UserAgent,
                    CorrelationId = correlation.CorrelationId,
                },
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            WriteFailed(logger, entry.Event, ex);
        }
    }

    private string HashEmail(string email)
    {
        string? key = configuration["Auth:SigningKey"];
        byte[] keyBytes = key is not null && TryDecode(key) is { } decoded ? decoded : Encoding.UTF8.GetBytes("fugue-access-audit");
        return Convert.ToHexString(HMACSHA256.HashData(keyBytes, Encoding.UTF8.GetBytes(EmailKey.Canonicalize(email)))).ToLowerInvariant();
    }

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
