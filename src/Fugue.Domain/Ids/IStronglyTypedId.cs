namespace Fugue.Domain.Ids;

/// <summary>
/// A <see cref="Guid"/>-backed identifier that is its own type, so a <c>CustomerId</c> can never be passed where
/// another aggregate's id is expected. Implementers are <c>readonly record struct</c>s; EF, Dapper, JSON and
/// MVC binding support is wired generically (see CLAUDE.md, "Adding a strongly-typed ID").
/// </summary>
public interface IStronglyTypedId<TSelf>
    where TSelf : struct, IStronglyTypedId<TSelf>
{
    Guid Value { get; }

    static abstract TSelf From(Guid value);
}
