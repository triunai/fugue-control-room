using FluentResults;
using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Claims.Commands;

/// <summary>Contract. Approved to Settled. Needs <c>CLAIMS_SETTLE</c> (checked at the endpoint). <paramref name="ExpectedVersion"/> comes from <c>If-Match</c> and is mandatory.</summary>
public sealed record SettleClaimCommand(ClaimId Id, long? ExpectedVersion) : ICommand<Result<ClaimDto>>;

public sealed class SettleClaimValidator : AbstractValidator<SettleClaimCommand>
{
    public SettleClaimValidator() => RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
}

public sealed class SettleClaimHandler(IClaimRepository claims, ICurrentUser currentUser, IAuditStamp stamp, IActorDirectory actors)
    : ICommandHandler<SettleClaimCommand, Result<ClaimDto>>
{
    public async ValueTask<Result<ClaimDto>> Handle(SettleClaimCommand command, CancellationToken cancellationToken)
    {
        if (await ClaimFlow.ActorAsync(currentUser, actors, cancellationToken) is not { } actor)
        {
            return Result.Fail<ClaimDto>(ClaimErrors.Unauthenticated());
        }

        return await ClaimFlow.ApplyAsync(
            claims, command.Id, command.ExpectedVersion, ClaimScope.From(currentUser),
            claim => claim.Settle(actor.Id, actor.Display, stamp.Capture().Now), cancellationToken);
    }
}
