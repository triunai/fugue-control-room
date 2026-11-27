using FluentResults;
using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Claims.Helpers;
using Fugue.Application.Features.Claims.Models;
using Fugue.Application.Features.Claims.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Claims;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;
using Mediator;

namespace Fugue.Application.Features.Letters.Queries;

/// <summary>
/// Contract. The sample letter of a decided claim as a PDF. Same visibility as <c>GET /claims/{id}</c> (the claimant who submitted it, or
/// <c>CLAIMS_ALL</c>; anything else is 404). An undecided claim is 409 <c>claim.letter_not_available</c>. Nothing is stored: the letter is made per request.
/// </summary>
public sealed record GetClaimLetterQuery(ClaimId Id) : IQuery<Result<ClaimLetterFile>>;

/// <summary>A generated letter: the download name and the PDF bytes.</summary>
public sealed record ClaimLetterFile(string FileName, byte[] Content)
{
    public const string ContentType = "application/pdf";
}

public sealed class GetClaimLetterValidator : AbstractValidator<GetClaimLetterQuery>
{
    public GetClaimLetterValidator() => RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
}

public sealed class GetClaimLetterHandler(IClaimRepository claims, ICurrentUser currentUser, ICorrelationContext correlation, TimeProvider time)
    : IQueryHandler<GetClaimLetterQuery, Result<ClaimLetterFile>>
{
    public async ValueTask<Result<ClaimLetterFile>> Handle(GetClaimLetterQuery query, CancellationToken cancellationToken)
    {
        ClaimScope scope = ClaimScope.From(currentUser);
        ClaimDto? claim = await claims.GetAsync(query.Id, scope, cancellationToken);
        if (claim is null)
        {
            return Result.Fail<ClaimLetterFile>(ClaimErrors.NotFound(query.Id));
        }

        if (!ClaimLetter.IsDecided(claim.Status))
        {
            return Result.Fail<ClaimLetterFile>(NotAvailable(claim.Status));
        }

        IReadOnlyList<ClaimTimelineEntryDto> timeline = await claims.GetTimelineAsync(query.Id, scope, cancellationToken) ?? [];
        ClaimLetter letter = ClaimLetter.From(claim, timeline, correlation.CorrelationId, time.GetUtcNow());
        return Result.Ok(new ClaimLetterFile(letter.FileName, ClaimLetterPdf.Render(letter)));
    }

    private static FugueError NotAvailable(ClaimStatus status) =>
        FugueErrors.Conflict(
            "claim.letter_not_available", $"A letter exists once a claim is decided (Approved, Rejected or Settled); this claim is {status}.");
}
