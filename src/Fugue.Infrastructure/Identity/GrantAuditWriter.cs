using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;

namespace Fugue.Infrastructure.Identity;

/// <summary>Stages <c>grant_audit</c> rows on the scoped DbContext; they commit with the caller's SaveChanges (same transaction).</summary>
internal sealed class GrantAuditWriter(FugueDbContext db, IAuditStamp stamp, ICorrelationContext correlation) : IGrantAuditWriter
{
    public void Record(string subjectType, Guid subjectId, Guid? targetUserId, string action, string? reason, string? beforeJson = null, string? afterJson = null)
    {
        AuditStampValue now = stamp.Capture();
        db.GrantAudits.Add(new GrantAudit
        {
            Id = GrantAuditId.New(),
            OccurredAt = now.Now,
            Actor = now.Actor,
            SubjectType = subjectType,
            SubjectId = subjectId,
            TargetUserId = targetUserId,
            Action = action,
            Reason = reason,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            CorrelationId = correlation.CorrelationId,
            SagaId = correlation.SagaId,
        });
    }
}
