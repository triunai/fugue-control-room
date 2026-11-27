using FluentResults;
using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Claims.Commands;
using Fugue.Application.Features.Claims.Helpers;
using Fugue.Application.Features.Claims.Models;
using Fugue.Application.Features.Claims.Repos;
using Fugue.Application.Features.Import.Parsing;
using Fugue.Application.Features.Import.Queries;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;
using Mediator;

namespace Fugue.Application.Features.Import.Commands;

/// <summary>
/// Contract. A person who reviewed a draft creates the claim from it, on behalf of the claimant account named by <see cref="ClaimantEmail"/>. The claim
/// goes the normal submit way: the same validator, the same <c>Claim.Submit</c>, the same insert (timeline, audit, outbox <c>claim.submitted</c>).
/// <see cref="Key"/> is the item's key from the parse response; a key already in the ledger is <c>import.already_imported</c> (409), so the same row,
/// email or chat never becomes two claims.
/// </summary>
public sealed record CreateImportedClaimCommand(
    ImportSource Source,
    string Key,
    string FileName,
    string FileSha256,
    string Item,
    Market Market,
    LineOfBusiness Line,
    string PolicyNumber,
    DateOnly IncidentDate,
    string IncidentDescription,
    string? IncidentLocation,
    string ClaimantEmail,
    string? Note) : ICommand<Result<ClaimDto>>
{
    internal SubmitClaimCommand ToSubmit() => new(Market, Line, PolicyNumber, IncidentDate, IncidentDescription, IncidentLocation);
}

public sealed class CreateImportedClaimValidator : AbstractValidator<CreateImportedClaimCommand>
{
    private const string Sha256Hex = "^[0-9a-f]{64}$";

    public CreateImportedClaimValidator(IValidator<SubmitClaimCommand> submit)
    {
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.Key).NotEmpty().Matches(Sha256Hex).WithMessage("The key must be the 64-character key from the parse response.");
        RuleFor(x => x.FileSha256).NotEmpty().Matches(Sha256Hex);
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Item).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ClaimantEmail).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.Note).MaximumLength(ImportLimits.MaxNoteChars);

        // Report an incident's rules, unchanged: the submit validator runs on the same fields and its failures are reported as they are.
        RuleFor(x => x).Custom((command, context) =>
        {
            foreach (FluentValidation.Results.ValidationFailure failure in submit.Validate(command.ToSubmit()).Errors)
            {
                context.AddFailure(failure);
            }
        });
    }
}

public sealed class CreateImportedClaimHandler(
    IClaimRepository claims, IImportStore imports, IUnitOfWork unitOfWork, ICurrentUser currentUser, IActorDirectory actors, IAuditStamp stamp)
    : ICommandHandler<CreateImportedClaimCommand, Result<ClaimDto>>
{
    public async ValueTask<Result<ClaimDto>> Handle(CreateImportedClaimCommand command, CancellationToken cancellationToken)
    {
        if (await ClaimFlow.ActorAsync(currentUser, actors, cancellationToken) is not { } staff)
        {
            return Result.Fail<ClaimDto>(ClaimErrors.Unauthenticated());
        }

        ImportClaimant? claimant = await imports.FindClaimantAsync(EmailKey.Canonicalize(command.ClaimantEmail), cancellationToken);
        if (CheckImportDraftsHandler.ClaimantError(claimant) is { } problem)
        {
            return Result.Fail<ClaimDto>(FugueErrors.Validation(
                claimant is null ? "import.claimant_unknown" : "import.claimant_cannot_own_claims", problem,
                new Dictionary<string, string[]> { ["claimantEmail"] = [problem] }));
        }

        if ((await imports.FindImportedAsync([command.Key], cancellationToken)).TryGetValue(command.Key, out ImportedRef? earlier))
        {
            return Result.Fail<ClaimDto>(ImportErrors.AlreadyImported(earlier.ClaimNumber));
        }

        DateTimeOffset now = stamp.Capture().Now;
        await unitOfWork.BeginAsync(cancellationToken);
        long sequence = await claims.NextNumberAsync(cancellationToken);
        string number = ClaimNumber.Format(command.Market, Markets.LocalToday(command.Market, now).Year, sequence);
        Claim claim = Claim.Submit(
            number, command.Market, command.Line, claimant!.Id, claimant.DisplayName, command.PolicyNumber, command.IncidentDate,
            command.IncidentDescription, command.IncidentLocation, now);
        claim.RecordImport(staff.Id, staff.Display, ImportRules.ProvenanceNote(command.Source, Path.GetFileName(command.FileName), command.Item, command.Note), now);

        Result added = await claims.AddAsync(claim, cancellationToken);
        if (added.IsFailed)
        {
            return Result.Fail<ClaimDto>(added.Errors); // the unit of work is disposed uncommitted: nothing is kept
        }

        Result recorded = await imports.AddAsync(
            ClaimImport.Record(command.Key, claim.Id, command.Source, Path.GetFileName(command.FileName), command.FileSha256, command.Item, staff.Id), cancellationToken);
        if (recorded.IsFailed)
        {
            return Result.Fail<ClaimDto>(recorded.Errors); // a concurrent import of the same key won: this claim rolls back
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return await claims.GetAsync(claim.Id, ClaimScope.From(currentUser), cancellationToken) is { } dto
            ? Result.Ok(dto)
            : Result.Fail<ClaimDto>(ClaimErrors.NotFound(claim.Id));
    }
}

public static class ImportErrors
{
    public const string AlreadyImportedCode = "import.already_imported";

    public static FugueError AlreadyImported(string claimNumber) =>
        FugueErrors.Conflict(AlreadyImportedCode, $"This item was already imported as claim {claimNumber}; it was not created again.");
}
