using Fugue.Domain.Ids;

namespace Fugue.Application.Abstractions;

/// <summary>One authentication or authorization event for <c>access_audit</c>. Never carries a password or a token.</summary>
/// <param name="Event">login, refresh, refresh_reuse, logout, forbidden, module_disabled, unauthenticated.</param>
/// <param name="Outcome">success, failure, denied.</param>
/// <param name="ReasonCode">An internal reason (bad_password, expired, the missing permission code, the module code...). Never sent to the client.
/// For <c>unauthenticated</c> (a 401 challenge, outcome <c>denied</c>, no user, no email) it is <c>missing_token</c>, <c>expired</c> or
/// <c>invalid_token</c>; the token itself is never stored. These rows are bounded per client IP (<c>Audit:UnauthenticatedRowsPerIpPerMinute</c>);
/// past the bound no row is written and <c>fugue.audit.write_failures</c> counts it with <c>reason=suppressed</c>.</param>
/// <param name="Email">The raw email attempted; only a keyed hash of its canonical form is stored.</param>
public sealed record AccessAuditEntry(string Event, string Outcome, string? ReasonCode, UserId? UserId, string? Email, ClientInfo? Client);

/// <summary>
/// Writes <c>access_audit</c> rows (append-only; <c>occurred_at</c> and the correlation id come from the clock and the request).
/// Best effort: a failing audit write is logged and never turns a login or a 403 into a 500. Grant changes use
/// <see cref="IGrantAuditWriter"/> instead, which is transactional.
/// </summary>
public interface IAccessAuditWriter
{
    Task WriteAsync(AccessAuditEntry entry, CancellationToken ct);
}

/// <summary>
/// Records a grant or revoke (role, group membership, group role, override) as a <c>grant_audit</c> row. It only STAGES the row on
/// the current unit of work's DbContext: the caller's <c>SaveChanges</c>/commit writes it in the SAME transaction as the
/// grant, so the audit row can never outlive a rolled-back change nor be missing from a committed one.
/// </summary>
public interface IGrantAuditWriter
{
    void Record(string subjectType, Guid subjectId, Guid? targetUserId, string action, string? reason, string? beforeJson = null, string? afterJson = null);
}
