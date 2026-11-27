using FluentResults;
using FluentValidation;
using Fugue.Application.Common;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Claims;
using Fugue.Domain.Results;
using Mediator;

namespace Fugue.Application.Features.Claims.Queries;

/// <summary>
/// Contract. Keyset paging only: <paramref name="Cursor"/> is the opaque token from the previous page. Order is <c>(submitted_at, id)</c>: ASCENDING (oldest
/// first, the work queue) for a caller with <c>CLAIMS_ALL</c>, DESCENDING (newest first) for a claimant, who only ever sees their own claims.
/// <paramref name="Status"/> repeats (<c>?status=Submitted&amp;status=UnderReview</c>) or is comma separated; <paramref name="Assigned"/> is <c>me</c>, <c>none</c> or <c>any</c>;
/// <paramref name="Search"/> matches the claim number or the policy number.
/// </summary>
public sealed record ListClaimsQuery(
    int? Limit, IReadOnlyList<string>? Status, string? Assigned, string? Market, string? Line, string? Search, string? Cursor)
    : IQuery<Result<PagedResult<ClaimDto>>>;

public sealed class ListClaimsValidator : AbstractValidator<ListClaimsQuery>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public ListClaimsValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, MaxLimit).When(x => x.Limit is not null);
        RuleFor(x => x.Status)
            .Must(statuses => ListClaimsHandler.SplitStatuses(statuses).All(s => Enum.TryParse(s, ignoreCase: true, out ClaimStatus v) && Enum.IsDefined(v)))
            .When(x => x.Status is { Count: > 0 })
            .WithMessage("Status must be one of Submitted, UnderReview, InfoRequested, Approved, Rejected, Settled.");
        RuleFor(x => x.Assigned)
            .Must(a => Enum.TryParse(a, ignoreCase: true, out ClaimAssignment v) && Enum.IsDefined(v))
            .When(x => !string.IsNullOrEmpty(x.Assigned))
            .WithMessage("Assigned must be 'me', 'none' or 'any'.");
        RuleFor(x => x.Market)
            .Must(m => Enum.TryParse(m, ignoreCase: true, out Market v) && Enum.IsDefined(v))
            .When(x => !string.IsNullOrEmpty(x.Market))
            .WithMessage("Market must be one of SG, MY, TH, HK, ID, PH.");
        RuleFor(x => x.Line)
            .Must(l => Enum.TryParse(l, ignoreCase: true, out LineOfBusiness v) && Enum.IsDefined(v))
            .When(x => !string.IsNullOrEmpty(x.Line))
            .WithMessage("Line must be 'Motor' or 'Property'.");
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Cursor)
            .Must(c => KeysetCursor.TryDecode(c, out _))
            .When(x => !string.IsNullOrEmpty(x.Cursor))
            .WithMessage("Cursor is not valid.");
    }
}

public sealed class ListClaimsHandler(IClaimRepository claims, ICurrentUser currentUser)
    : IQueryHandler<ListClaimsQuery, Result<PagedResult<ClaimDto>>>
{
    internal static IEnumerable<string> SplitStatuses(IReadOnlyList<string>? raw) =>
        (raw ?? []).SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public async ValueTask<Result<PagedResult<ClaimDto>>> Handle(ListClaimsQuery query, CancellationToken cancellationToken)
    {
        int limit = query.Limit ?? ListClaimsValidator.DefaultLimit;
        KeysetCursor? after = KeysetCursor.TryDecode(query.Cursor, out KeysetCursor decoded) ? decoded : null;
        var filter = new ClaimFilter(
            [.. SplitStatuses(query.Status).Select(s => Enum.Parse<ClaimStatus>(s, ignoreCase: true)).Distinct()],
            Enum.TryParse(query.Assigned, ignoreCase: true, out ClaimAssignment assigned) ? assigned : ClaimAssignment.Any,
            Enum.TryParse(query.Market, ignoreCase: true, out Market market) ? market : null,
            Enum.TryParse(query.Line, ignoreCase: true, out LineOfBusiness line) ? line : null,
            string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim());

        IReadOnlyList<ClaimDto> rows = await claims.ListAsync(filter, after, limit + 1, ClaimScope.From(currentUser), cancellationToken);

        if (rows.Count <= limit)
        {
            return Result.Ok(new PagedResult<ClaimDto>(rows, null));
        }

        ClaimDto last = rows[limit - 1];
        return Result.Ok(new PagedResult<ClaimDto>(rows.Take(limit).ToList(), new KeysetCursor(last.SubmittedAt, last.Id.Value).Encode()));
    }
}
