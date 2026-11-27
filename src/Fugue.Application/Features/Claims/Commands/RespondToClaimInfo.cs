using FluentResults;
using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Claims.Commands;

/// <summary>
/// Contract. The claimant answers the open question: InfoRequested to UnderReview. The claim must be the caller's own, even for a caller who can
/// see every claim. <paramref name="ExpectedVersion"/> comes from <c>If-Match</c> and is mandatory.
/// </summary>
public sealed record RespondToClaimInfoCommand(ClaimId Id, ClaimInfoRequestId RequestId, string Answer, long? ExpectedVersion)
    : ICommand<Result<ClaimDto>>;

public sealed class RespondToClaimInfoValidator : AbstractValidator<RespondToClaimInfoCommand>
{
    public RespondToClaimInfoValidator()
    {
        RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.RequestId).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Answer).NotEmpty().MaximumLength(4000);
    }
}

public sealed class RespondToClaimInfoHandler(IClaimRepository claims, ICurrentUser currentUser, IAuditStamp stamp, IActorDirectory actors)
    : ICommandHandler<RespondToClaimInfoCommand, Result<ClaimDto>>
{
    public async ValueTask<Result<ClaimDto>> Handle(RespondToClaimInfoCommand command, CancellationToken cancellationToken)
    {
        if (await ClaimFlow.ActorAsync(currentUser, actors, cancellationToken) is not { } claimant)
        {
            return Result.Fail<ClaimDto>(ClaimErrors.Unauthenticated());
        }

        return await ClaimFlow.ApplyAsync(
            claims, command.Id, command.ExpectedVersion, ClaimScope.Own(currentUser),
            claim => claim.ProvideInfo(command.RequestId, command.Answer, claimant.Id, claimant.Display, stamp.Capture().Now), cancellationToken);
    }
}
