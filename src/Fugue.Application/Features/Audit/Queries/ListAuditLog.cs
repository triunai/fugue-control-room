using FluentResults;
using FluentValidation;
using Fugue.Application.Common;
using Fugue.Domain.Results;
using Mediator;

namespace Fugue.Application.Features.Audit.Queries;

/// <summary>Contract. Keyset paging only: <paramref name="Cursor"/> is the opaque token from the previous page. Newest rows first.</summary>
public sealed record ListAuditLogQuery(
    string? EntityType,
    string? EntityId,
    Guid? ActorUserId,
    string? Action,
    string? CorrelationId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int? Limit,
    string? Cursor) : IQuery<Result<PagedResult<AuditLogDto>>>;

public sealed class ListAuditLogValidator : AbstractValidator<ListAuditLogQuery>
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public ListAuditLogValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, MaxLimit).When(x => x.Limit is not null);
        RuleFor(x => x.EntityType).MaximumLength(64);
        RuleFor(x => x.EntityId).MaximumLength(128);
        RuleFor(x => x.Action).MaximumLength(64);
        RuleFor(x => x.CorrelationId).MaximumLength(64);
        RuleFor(x => x.ActorUserId).Must(id => id != Guid.Empty).When(x => x.ActorUserId is not null);
        RuleFor(x => x.To).GreaterThan(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("To must be after From.");
        RuleFor(x => x.Cursor)
            .Must(c => KeysetCursor.TryDecode(c, out _))
            .When(x => !string.IsNullOrEmpty(x.Cursor))
            .WithMessage("Cursor is not valid.");
    }
}

public sealed class ListAuditLogHandler(IAuditLogReader reader)
    : IQueryHandler<ListAuditLogQuery, Result<PagedResult<AuditLogDto>>>
{
    public async ValueTask<Result<PagedResult<AuditLogDto>>> Handle(ListAuditLogQuery query, CancellationToken cancellationToken)
    {
        int limit = query.Limit ?? ListAuditLogValidator.DefaultLimit;
        KeysetCursor? after = KeysetCursor.TryDecode(query.Cursor, out KeysetCursor decoded) ? decoded : null;
        var filter = new AuditLogFilter(
            Blank(query.EntityType), Blank(query.EntityId), query.ActorUserId, Blank(query.Action), Blank(query.CorrelationId), query.From, query.To);

        IReadOnlyList<AuditLogDto> rows = await reader.ListAsync(filter, after, limit + 1, cancellationToken);
        if (rows.Count <= limit)
        {
            return Result.Ok(new PagedResult<AuditLogDto>(rows, null));
        }

        AuditLogDto last = rows[limit - 1];
        return Result.Ok(new PagedResult<AuditLogDto>(rows.Take(limit).ToList(), new KeysetCursor(last.OccurredAt, last.Id).Encode()));
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
