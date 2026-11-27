using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Claims.Helpers;
using Fugue.Application.Features.Claims.Models;
using Fugue.Application.Features.Claims.Repos;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Claims.Commands;

/// <summary>
/// Contract. Sets the internal reserve (bigint minor units in the claim's currency) while the claim is UnderReview or InfoRequested. Only the
/// assigned officer, or a manager, may. <paramref name="ExpectedVersion"/> comes from <c>If-Match</c> and is mandatory.
/// </summary>
public sealed record ReserveClaimCommand(ClaimId Id, long AmountMinor, long? ExpectedVersion) : ICommand<Result<ClaimDto>>;

public sealed class ReserveClaimValidator : AbstractValidator<ReserveClaimCommand>
{
    public ReserveClaimValidator()
    {
        RuleFor(x => x.Id).Must(id => id.Value != Guid.Empty);
        RuleFor(x => x.AmountMinor).GreaterThan(0);
    }
}

public sealed class ReserveClaimHandler(IClaimRepository claims, ICurrentUser currentUser)
    : ICommandHandler<ReserveClaimCommand, Result<ClaimDto>>
{
    public async ValueTask<Result<ClaimDto>> Handle(ReserveClaimCommand command, CancellationToken cancellationToken) =>
        await ClaimFlow.ApplyAsync(
            claims, command.Id, command.ExpectedVersion, ClaimScope.From(currentUser),
            claim => ClaimFlow.RequireAssignedOrManager(claim, currentUser) is { IsFailed: true } denied ? denied : claim.SetReserve(command.AmountMinor),
            cancellationToken);
}
