using FluentResults;
using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Claims.Commands;

/// <summary>
/// Contract. UnderReview to Approved or Rejected. Approving needs <paramref name="AmountMinor"/> (bigint minor units) with <c>0 &lt; amount &lt;= reserve</c>
/// and a reserve already set; rejecting carries no amount. The reason is shown to the claimant. Only the assigned officer, or a manager, may.
/// <paramref name="ExpectedVersion"/> comes from <c>If-Match</c> and is mandatory.
/// </summary>
public sealed record DecideClaimCommand(ClaimId Id, ClaimOutcome Outcome, long? AmountMinor, string Reason, long? ExpectedVersion)
    : ICommand<Result<ClaimDto>>;

public sealed class DecideClaimValidator : AbstractValidator<DecideClaimCommand>
{
    public DecideClaimValidator()
    {
        RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Outcome).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.AmountMinor).NotNull().GreaterThan(0).When(x => x.Outcome == ClaimOutcome.Approve)
            .WithMessage("An approval needs an amount greater than zero.");
        RuleFor(x => x.AmountMinor).Null().When(x => x.Outcome == ClaimOutcome.Reject)
            .WithMessage("A rejection carries no amount.");
    }
}

public sealed class DecideClaimHandler(IClaimRepository claims, ICurrentUser currentUser, IAuditStamp stamp, IActorDirectory actors)
    : ICommandHandler<DecideClaimCommand, Result<ClaimDto>>
{
    public async ValueTask<Result<ClaimDto>> Handle(DecideClaimCommand command, CancellationToken cancellationToken)
    {
        if (await ClaimFlow.ActorAsync(currentUser, actors, cancellationToken) is not { } officer)
        {
            return Result.Fail<ClaimDto>(ClaimErrors.Unauthenticated());
        }

        return await ClaimFlow.ApplyAsync(
            claims, command.Id, command.ExpectedVersion, ClaimScope.From(currentUser),
            claim =>
            {
                if (ClaimFlow.RequireAssignedOrManager(claim, currentUser) is { IsFailed: true } denied)
                {
                    return denied;
                }

                DateTimeOffset now = stamp.Capture().Now;
                return command.Outcome == ClaimOutcome.Approve
                    ? claim.Approve(command.AmountMinor!.Value, command.Reason, officer.Id, officer.Display, now)
                    : claim.Reject(command.Reason, officer.Id, officer.Display, now);
            },
            cancellationToken);
    }
}
