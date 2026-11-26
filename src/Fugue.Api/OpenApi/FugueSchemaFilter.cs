using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Fugue.Api.OpenApi;

/// <summary>
/// Two corrections to the generated schemas. (1) A property is <c>required</c> when its CLR type is non-nullable (a value type that is
/// not <c>Nullable&lt;T&gt;</c>, or a reference type annotated non-nullable): the server always writes it, and MVC rejects a missing
/// non-nullable reference in a request, so generated clients get <c>string</c> rather than <c>string | undefined</c>. (2) The problem
/// body gets the extension members every error carries (<c>code</c>, <c>traceId</c>, <c>correlationId</c>, <c>tenantId</c>); <c>code</c> is not required because a framework-generated problem (an unmatched route, a bad method) carries none.
/// </summary>
internal sealed class FugueSchemaFilter : ISchemaFilter
{
    private static readonly NullabilityInfoContext Nullability = new();

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        Type type = context.Type;
        if (type == typeof(ProblemDetails) || type == typeof(ValidationProblemDetails))
        {
            AddProblemExtensions(schema);
            return;
        }

        if (schema.Properties is not { Count: > 0 } || type.IsInterface || type == typeof(object))
        {
            return;
        }

        foreach (string name in schema.Properties.Keys)
        {
            PropertyInfo? property = type.GetProperties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (property is not null && IsNonNullable(property))
            {
                schema.Required.Add(name);
            }
        }
    }

    private static bool IsNonNullable(PropertyInfo property)
    {
        if (property.PropertyType.IsValueType)
        {
            return Nullable.GetUnderlyingType(property.PropertyType) is null;
        }

        lock (Nullability)
        {
            return Nullability.Create(property).ReadState == NullabilityState.NotNull;
        }
    }

    private static void AddProblemExtensions(OpenApiSchema schema)
    {
        schema.Properties["code"] = new OpenApiSchema { Type = "string", Description = "Stable machine-readable error code, such as customer.not_found. Clients branch on this, never on the title." };
        schema.Properties["traceId"] = new OpenApiSchema { Type = "string", Description = "W3C trace id of the request, for finding it in the logs." };
        schema.Properties["correlationId"] = new OpenApiSchema { Type = "string", Description = "The request's X-Correlation-Id (echoed, or generated when the client sent none)." };
        schema.Properties["tenantId"] = new OpenApiSchema { Type = "string", Format = "uuid", Nullable = true, Description = "The caller's tenant, present only when tenancy is on and the caller is authenticated." };
        schema.AdditionalPropertiesAllowed = true;
    }
}
