using FluentResults;
using Fugue.Domain.Results;
using Mediator;

namespace Fugue.Tests.Api;

public sealed record Thing(int Id, string Name, long Version) : IVersioned;

public sealed record Echo(long? IfMatch, string? IdempotencyKey);

public sealed record GetThing(int Id) : IRequest<Result<Thing>>;

public sealed record CreateThing(string Name) : IRequest<Result<Thing>>;

public sealed record DeleteThing(int Id) : IRequest<Result>;

public sealed record ListThings(int? Limit) : IRequest<Result<PagedResult<string>>>;

public sealed record EchoHeaders(long? IfMatch, string? IdempotencyKey) : IRequest<Result<Echo>>;

public sealed record FailWith(FugueError Error) : IRequest<Result<Thing>>;

/// <summary>Fake handlers for the probe requests, keyed by request type.</summary>
public static class ProbeHandlers
{
    public static IReadOnlyDictionary<Type, Func<object, object>> Table { get; } =
        new Dictionary<Type, Func<object, object>>
        {
            [typeof(GetThing)] = r => Result.Ok(new Thing(((GetThing)r).Id, "widget", Version: 3)),
            [typeof(CreateThing)] = r => Result.Ok(new Thing(42, ((CreateThing)r).Name, Version: 1)),
            [typeof(DeleteThing)] = _ => Result.Ok(),
            [typeof(ListThings)] = _ => Result.Ok(new PagedResult<string>(["a", "b"], "abc 1")),
            [typeof(EchoHeaders)] = r => Result.Ok(new Echo(((EchoHeaders)r).IfMatch, ((EchoHeaders)r).IdempotencyKey)),
            [typeof(FailWith)] = r => Result.Fail<Thing>(((FailWith)r).Error),
        };
}
