using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Import.Models;
using Fugue.Application.Features.Import.Repos;
using Mediator;

namespace Fugue.Application.Features.Import.Queries;

/// <summary>Contract. The most recent imports, newest first (a short list for the Import page, not a paged feed; the audit log is the full record).</summary>
public sealed record ListImportedClaimsQuery(int? Limit) : IQuery<Result<IReadOnlyList<ImportedClaimDto>>>;

public sealed class ListImportedClaimsValidator : AbstractValidator<ListImportedClaimsQuery>
{
    public ListImportedClaimsValidator() => RuleFor(x => x.Limit).InclusiveBetween(1, 50);
}

public sealed class ListImportedClaimsHandler(IImportStore imports) : IQueryHandler<ListImportedClaimsQuery, Result<IReadOnlyList<ImportedClaimDto>>>
{
    public async ValueTask<Result<IReadOnlyList<ImportedClaimDto>>> Handle(ListImportedClaimsQuery query, CancellationToken cancellationToken) =>
        Result.Ok(await imports.ListRecentAsync(query.Limit ?? 20, cancellationToken));
}
