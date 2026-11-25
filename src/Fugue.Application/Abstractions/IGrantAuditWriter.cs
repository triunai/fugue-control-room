namespace Fugue.Application.Abstractions;

/// <summary>
/// Records a grant or revoke (role, group membership, group role, override) as a <c>grant_audit</c> row. It only STAGES the row on
/// the current unit of work's DbContext: the caller's <c>SaveChanges</c>/commit writes it in the SAME transaction as the
/// grant, so the audit row can never outlive a rolled-back change nor be missing from a committed one.
/// </summary>
public interface IGrantAuditWriter
{
    void Record(string subjectType, Guid subjectId, Guid? targetUserId, string action, string? reason, string? beforeJson = null, string? afterJson = null);
}
