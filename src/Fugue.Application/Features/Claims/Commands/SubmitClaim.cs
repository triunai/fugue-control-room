using FluentResults;
using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Claims;
using Fugue.Domain.Ids;
using Mediator;

namespace Fugue.Application.Features.Claims.Commands;

/// <summary>
/// Contract. The claimant is the caller, never a field. The incident date must be neither in the future nor more than a year back, counted in the
/// market's own calendar day. The currency follows from the market.
/// </summary>
public sealed record SubmitClaimCommand(
    Market Market, LineOfBusiness Line, string PolicyNumber, DateOnly IncidentDate, string IncidentDescription, string? IncidentLocation)
    : ICommand<Result<ClaimDto>>;

public sealed class SubmitClaimValidator : AbstractValidator<SubmitClaimCommand>
{
    public SubmitClaimValidator(TimeProvider clock)
    {
        RuleFor(x => x.Market).IsInEnum();
        RuleFor(x => x.Line).IsInEnum();
        RuleFor(x => x.PolicyNumber).NotEmpty().MaximumLength(64);
        RuleFor(x => x.IncidentDescription).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.IncidentLocation).MaximumLength(200);
        RuleFor(x => x.IncidentDate)
            .Must((command, date) => date <= Markets.LocalToday(command.Market, clock.GetUtcNow()))
            .WithMessage("The incident date cannot be in the future.")
            .Must((command, date) => date >= Markets.LocalToday(command.Market, clock.GetUtcNow()).AddYears(-1))
            .WithMessage("The incident date cannot be more than a year ago.")
            .When(x => Enum.IsDefined(x.Market));
    }
}

public sealed class SubmitClaimHandler(IClaimRepository claims, ICurrentUser currentUser, IAuditStamp stamp, IActorDirectory actors)
    : ICommandHandler<SubmitClaimCommand, Result<ClaimDto>>
{
    public async ValueTask<Result<ClaimDto>> Handle(SubmitClaimCommand command, CancellationToken cancellationToken)
    {
        if (await ClaimFlow.ActorAsync(currentUser, actors, cancellationToken) is not { } claimant)
        {
            return Result.Fail<ClaimDto>(ClaimErrors.Unauthenticated());
        }

        DateTimeOffset now = stamp.Capture().Now;
        long sequence = await claims.NextNumberAsync(cancellationToken);
        string number = ClaimNumber.Format(command.Market, Markets.LocalToday(command.Market, now).Year, sequence);
        Claim claim = Claim.Submit(
            number, command.Market, command.Line, claimant.Id, claimant.Display, command.PolicyNumber, command.IncidentDate,
            command.IncidentDescription, command.IncidentLocation, now);

        Result added = await claims.AddAsync(claim, cancellationToken);
        if (added.IsFailed)
        {
            return Result.Fail<ClaimDto>(added.Errors);
        }

        return await claims.GetAsync(claim.Id, ClaimScope.From(currentUser), cancellationToken) is { } dto
            ? Result.Ok(dto)
            : Result.Fail<ClaimDto>(ClaimErrors.NotFound(claim.Id));
    }
}
