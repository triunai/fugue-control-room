using Fugue.Application.Common;
using Fugue.Application.Features.Audit.Models;

namespace Fugue.Application.Features.Audit.Repos;

/// <summary>Read side of <c>audit_log</c> (EF, no tracking). The audit entity itself never leaves Infrastructure.</summary>
public interface IAuditLogReader
{
    /// <summary>
    /// Newest first, keyset ordered by <c>(occurred_at, id)</c> descending. Returns up to <paramref name="take"/> rows strictly after
    /// <paramref name="after"/> in that order; the caller asks for one extra row to learn whether another page exists.
    /// </summary>
    Task<IReadOnlyList<AuditLogDto>> ListAsync(AuditLogFilter filter, KeysetCursor? after, int take, CancellationToken ct);
}
