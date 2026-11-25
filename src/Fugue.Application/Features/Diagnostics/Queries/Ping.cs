using FluentResults;
using FluentValidation;
using Mediator;

namespace Fugue.Application.Features.Diagnostics.Queries;

public sealed record PingResponse(string Message, int Count);

/// <summary>Contract. A health-style query that exercises the whole pipeline; it touches no storage.</summary>
public sealed record PingQuery(int Count) : IQuery<Result<PingResponse>>;

public sealed class PingValidator : AbstractValidator<PingQuery>
{
    public PingValidator() => RuleFor(x => x.Count).InclusiveBetween(0, 100).WithMessage("Count must be between 0 and 100.");
}

public sealed class PingHandler : IQueryHandler<PingQuery, Result<PingResponse>>
{
    public ValueTask<Result<PingResponse>> Handle(PingQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Ok(new PingResponse("pong", query.Count)));
}
