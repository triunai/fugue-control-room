using FluentResults;
using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Claims.Commands;

/// <summary>Contract. Submitted to UnderReview; the caller becomes the assigned officer. <paramref name="ExpectedVersion"/> comes from <c>If-Match</c> and is mandatory.</summary>
public sealed record PickupClaimCommand(ClaimId Id, long? ExpectedVersion) : ICommand<Result<ClaimDto>>;

public sealed class PickupClaimValidator : AbstractValidator<PickupClaimCommand>
{
    public PickupClaimValidator() => RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
}

public sealed class PickupClaimHandler(IClaimRepository claims, ICurrentUser currentUser, IAuditStamp stamp, IActorDirectory actors)
    : ICommandHandler<PickupClaimCommand, Result<ClaimDto>>
{
    public async ValueTask<Result<ClaimDto>> Handle(PickupClaimCommand command, CancellationToken cancellationToken)
    {
        if (await ClaimFlow.ActorAsync(currentUser, actors, cancellationToken) is not { } officer)
        {
            return Result.Fail<ClaimDto>(ClaimErrors.Unauthenticated());
        }

        return await ClaimFlow.ApplyAsync(
            claims, command.Id, command.ExpectedVersion, ClaimScope.From(currentUser),
            claim => claim.Pickup(officer.Id, officer.Display, stamp.Capture().Now), cancellationToken);
    }
}
