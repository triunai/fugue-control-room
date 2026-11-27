using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Claims.Commands;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Mediator;

namespace Fugue.Application.Features.Import.Queries;

/// <summary>
/// Contract. Checks drafts without creating anything: the Report an incident rules (the submit validator itself), whether the claimant account exists
/// and may own claims, and whether the item was imported before. Answers 200 with one verdict per draft, in order, even when every draft has errors.
/// </summary>
public sealed record CheckImportDraftsQuery(IReadOnlyList<ImportDraftInput> Drafts) : IQuery<Result<IReadOnlyList<ImportCheckDto>>>;

public sealed class CheckImportDraftsValidator : AbstractValidator<CheckImportDraftsQuery>
{
    public const int MaxDrafts = 500;

    public CheckImportDraftsValidator()
    {
        RuleFor(x => x.Drafts).NotNull().Must(d => d is { Count: > 0 and <= MaxDrafts }).WithMessage($"Send 1 to {MaxDrafts} drafts.");
        RuleForEach(x => x.Drafts).ChildRules(d => d.RuleFor(x => x.Key).NotEmpty().MaximumLength(64));
    }
}

public sealed class CheckImportDraftsHandler(IImportStore imports, IValidator<SubmitClaimCommand> submit)
    : IQueryHandler<CheckImportDraftsQuery, Result<IReadOnlyList<ImportCheckDto>>>
{
    public async ValueTask<Result<IReadOnlyList<ImportCheckDto>>> Handle(CheckImportDraftsQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, ImportedRef> imported = await imports.FindImportedAsync([.. query.Drafts.Select(d => d.Key).Distinct()], cancellationToken);
        var claimants = new Dictionary<string, ImportClaimant?>(StringComparer.Ordinal);
        foreach (string email in query.Drafts.Select(d => d.ClaimantEmail).Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => EmailKey.Canonicalize(e!)).Distinct())
        {
            claimants[email] = await imports.FindClaimantAsync(email, cancellationToken);
        }

        List<ImportCheckDto> verdicts = [];
        foreach (ImportDraftInput draft in query.Drafts)
        {
            Dictionary<string, string[]> errors = ImportRules.Errors(submit, draft, out Market? market, out LineOfBusiness? line, out DateOnly? date);
            ImportClaimant? claimant = string.IsNullOrWhiteSpace(draft.ClaimantEmail) ? null : claimants[EmailKey.Canonicalize(draft.ClaimantEmail)];
            if (!string.IsNullOrWhiteSpace(draft.ClaimantEmail) && ClaimantError(claimant) is { } problem)
            {
                errors["claimantEmail"] = [problem];
            }

            ImportedRefDto? done = imported.TryGetValue(draft.Key, out ImportedRef? r) ? new ImportedRefDto(r.ClaimId, r.ClaimNumber) : null;
            verdicts.Add(new ImportCheckDto(
                draft.Key, errors.Count == 0 && done is null, market, line, date, errors,
                claimant is { CanOwnClaims: true } ? new ImportClaimantDto(claimant.Id, claimant.DisplayName) : null, done));
        }

        return Result.Ok<IReadOnlyList<ImportCheckDto>>(verdicts);
    }

    /// <summary>Null when the account may own the claim, else why not (shown on the claimant email field).</summary>
    internal static string? ClaimantError(ImportClaimant? claimant) => claimant switch
    {
        null => "No live account has this email. The claimant needs an account before a claim can be filed for them.",
        { CanOwnClaims: false } => "This account cannot own claims (it does not hold CLAIMS_SUBMIT).",
        _ => null,
    };
}
