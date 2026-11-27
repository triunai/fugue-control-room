using FluentResults;
using FluentValidation;
using Mediator;

namespace Fugue.Application.Features.Audit.Queries;

/// <summary>Contract. Full-text search over the audit index; every filter is an exact match and they combine with the text.</summary>
public sealed record SearchAuditLogQuery(
    string? Q,
    string? EntityType,
    string? Action,
    string? Outcome,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int? Limit) : IQuery<Result<AuditSearchResultDto>>;

public sealed class SearchAuditLogValidator : AbstractValidator<SearchAuditLogQuery>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public SearchAuditLogValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, MaxLimit).When(x => x.Limit is not null);
        RuleFor(x => x.Q).MaximumLength(200);
        RuleFor(x => x.EntityType).MaximumLength(64);
        RuleFor(x => x.Action).MaximumLength(64);
        RuleFor(x => x.Outcome).MaximumLength(16);
        RuleFor(x => x.To).GreaterThan(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("To must be after From.");
    }
}

public sealed class SearchAuditLogHandler(IAuditSearch search) : IQueryHandler<SearchAuditLogQuery, Result<AuditSearchResultDto>>
{
    public async ValueTask<Result<AuditSearchResultDto>> Handle(SearchAuditLogQuery query, CancellationToken cancellationToken) =>
        await search.SearchAsync(
            new AuditSearchFilter(Blank(query.Q), Blank(query.EntityType), Blank(query.Action), Blank(query.Outcome), query.From, query.To, query.Limit ?? SearchAuditLogValidator.DefaultLimit),
            cancellationToken);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
