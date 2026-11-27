using FluentResults;

namespace Fugue.Application.Features.Audit;

/// <summary>
/// Full-text search over the audit trail (D-058). The index is a rebuildable copy of <c>audit_log</c> (values already masked when the row was written),
/// so it is eventually consistent. With no search engine configured every call fails with <c>search.unavailable</c>.
/// </summary>
public interface IAuditSearch
{
    Task<Result<AuditSearchResultDto>> SearchAsync(AuditSearchFilter filter, CancellationToken ct);
}

/// <summary>Search text plus exact-match filters. <c>From</c> is inclusive and <c>To</c> exclusive. Results are newest first.</summary>
public sealed record AuditSearchFilter(
    string? Query, string? EntityType, string? Action, string? Outcome, DateTimeOffset? From, DateTimeOffset? To, int Limit);

/// <summary>
/// One search hit. <paramref name="Highlights"/> maps a field name to its text with matches wrapped in <c>&lt;em&gt;</c>; the rest of the text
/// is NOT HTML-escaped, so a console must escape everything except the <c>em</c> tags before rendering. Null when the query was empty.
/// </summary>
public sealed record AuditSearchHitDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid? ActorUserId,
    string? ActorDisplay,
    string Action,
    string? EntityType,
    string? EntityId,
    string Outcome,
    string? ErrorCode,
    string? ErrorMessage,
    string? RouteTemplate,
    IReadOnlyDictionary<string, string>? Highlights);

/// <summary><paramref name="EstimatedTotalHits"/> is the engine's estimate of all matches, not the page size.</summary>
public sealed record AuditSearchResultDto(IReadOnlyList<AuditSearchHitDto> Hits, int EstimatedTotalHits);
