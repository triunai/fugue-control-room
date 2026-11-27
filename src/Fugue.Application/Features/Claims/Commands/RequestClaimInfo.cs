using FluentResults;
using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Claims.Helpers;
using Fugue.Application.Features.Claims.Models;
using Fugue.Application.Features.Claims.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Claims.Commands;

/// <summary>
/// Contract. UnderReview to InfoRequested, with the question the claimant must answer. Only the assigned officer, or a manager, may.
/// <paramref name="ExpectedVersion"/> comes from <c>If-Match</c> and is mandatory.
/// </summary>
public sealed record RequestClaimInfoCommand(ClaimId Id, string Question, long? ExpectedVersion) : ICommand<Result<ClaimDto>>;

public sealed class RequestClaimInfoValidator : AbstractValidator<RequestClaimInfoCommand>
{
    public RequestClaimInfoValidator()
    {
        RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.Question).NotEmpty().MaximumLength(2000);
    }
}

public sealed class RequestClaimInfoHandler(IClaimRepository claims, ICurrentUser currentUser, IAuditStamp stamp, IActorDirectory actors)
    : ICommandHandler<RequestClaimInfoCommand, Result<ClaimDto>>
{
    public async ValueTask<Result<ClaimDto>> Handle(RequestClaimInfoCommand command, CancellationToken cancellationToken)
    {
        if (await ClaimFlow.ActorAsync(currentUser, actors, cancellationToken) is not { } officer)
        {
            return Result.Fail<ClaimDto>(ClaimErrors.Unauthenticated());
        }

        return await ClaimFlow.ApplyAsync(
            claims, command.Id, command.ExpectedVersion, ClaimScope.From(currentUser),
            claim => ClaimFlow.RequireAssignedOrManager(claim, currentUser) is { IsFailed: true } denied
                ? denied
                : claim.RequestInfo(command.Question, officer.Id, officer.Display, stamp.Capture().Now),
            cancellationToken);
    }
}
