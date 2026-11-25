using System.Linq.Expressions;
using Dapper;
using Fugue.Domain.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Fugue.Infrastructure.Persistence.Ids;

/// <summary>
/// Finds every <see cref="IStronglyTypedId{TSelf}"/> in the Domain assembly and wires it for EF and Dapper, so
/// adding the next id needs no persistence change (see CLAUDE.md, "Adding a strongly-typed ID").
/// </summary>
internal static class TypedIds
{
    private static readonly Type[] All = typeof(IStronglyTypedId<>).Assembly.GetTypes()
        .Where(t => t is { IsValueType: true, IsAbstract: false }
            && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStronglyTypedId<>)))
        .ToArray();

    public static void ConfigureEfConventions(ModelConfigurationBuilder builder)
    {
        foreach (Type id in All)
        {
            builder.Properties(id).HaveConversion(typeof(GuidIdValueConverter<>).MakeGenericType(id));
        }
    }

    public static void RegisterDapperHandlers()
    {
        foreach (Type id in All)
        {
            SqlMapper.AddTypeHandler(id, (SqlMapper.ITypeHandler)Activator.CreateInstance(typeof(GuidIdTypeHandler<>).MakeGenericType(id))!);
        }
    }
}

/// <summary>EF Core converter: typed id to <see cref="Guid"/> (Postgres <c>uuid</c>).</summary>
internal sealed class GuidIdValueConverter<TId>() : ValueConverter<TId, Guid>(id => id.Value, FromGuid())
    where TId : struct, IStronglyTypedId<TId>
{
    // Expression trees cannot call a static abstract member, so the id's own From(Guid) is bound by reflection once.
    private static Expression<Func<Guid, TId>> FromGuid()
    {
        ParameterExpression guid = Expression.Parameter(typeof(Guid), "guid");
        System.Reflection.MethodInfo from = typeof(TId).GetMethod(nameof(IStronglyTypedId<TId>.From), [typeof(Guid)])!;
        return Expression.Lambda<Func<Guid, TId>>(Expression.Call(from, guid), guid);
    }
}
