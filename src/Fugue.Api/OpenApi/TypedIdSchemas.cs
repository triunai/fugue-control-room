using Fugue.Domain.Ids;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Fugue.Api.OpenApi;

/// <summary>
/// Every strongly typed id (<see cref="IStronglyTypedId{TSelf}"/>) is a UUID string on the wire (its JSON converter and route binder
/// say so), not the <c>{ value }</c> object reflection would produce. Mapping them keeps the contract honest for client generators.
/// </summary>
internal static class TypedIdSchemas
{
    public static void Map(SwaggerGenOptions options)
    {
        foreach (Type type in typeof(IStronglyTypedId<>).Assembly.GetTypes().Where(IsTypedId))
        {
            options.MapType(type, () => new OpenApiSchema
            {
                Type = "string",
                Format = "uuid",
            });
        }
    }

    internal static bool IsTypedId(Type type) =>
        type is { IsValueType: true, IsGenericType: false }
        && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStronglyTypedId<>));
}
