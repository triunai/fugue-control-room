using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Fugue.Api.OpenApi;

/// <summary>
/// The document-level components every operation shares: one response per problem status (always an <c>application/problem+json</c> body with a
/// stable <c>code</c>) and one entry per response header, so a status or a header is described once and referenced, not repeated per operation.
/// </summary>
internal sealed class FugueDocumentFilter : IDocumentFilter
{
    public static readonly Dictionary<string, string> Reasons = new()
    {
        ["200"] = "OK",
        ["201"] = "Created",
        ["204"] = "No Content",
        ["400"] = "Bad Request: the request is malformed or a field is invalid; `errors` maps each field to its messages.",
        ["401"] = "Unauthorized: no valid bearer token (code `auth.unauthenticated`).",
        ["403"] = "Forbidden: the caller lacks the permission (code `auth.forbidden`).",
        ["404"] = "Not Found: no such resource, or it is outside the caller's scope, or its module is disabled (code `module.disabled`).",
        ["409"] = "Conflict: the request conflicts with the current state.",
        ["413"] = "Payload Too Large: a request with an Idempotency-Key is too large to fingerprint (code `idempotency.body_too_large`).",
        ["422"] = "Unprocessable Entity: the Idempotency-Key was already used with a different request body (code `idempotency.key_reused`).",
        ["412"] = "Precondition Failed: the If-Match version is stale; read the resource again.",
        ["428"] = "Precondition Required: the If-Match header is missing.",
        ["429"] = "Too Many Requests: wait for Retry-After seconds.",
        ["503"] = "Service Unavailable: a dependency is briefly unavailable; retry after Retry-After seconds.",
    };

    /// <summary>Component id of the shared response for each problem status.</summary>
    public static readonly Dictionary<string, string> ProblemResponseIds = new()
    {
        ["400"] = "BadRequest",
        ["401"] = "Unauthorized",
        ["403"] = "Forbidden",
        ["404"] = "NotFound",
        ["412"] = "PreconditionFailed",
        ["413"] = "PayloadTooLarge",
        ["422"] = "UnprocessableEntity",
        ["428"] = "PreconditionRequired",
        ["429"] = "TooManyRequests",
        ["503"] = "ServiceUnavailable",
    };

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        OpenApiSchema problem = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository);
        OpenApiSchema validation = context.SchemaGenerator.GenerateSchema(typeof(ValidationProblemDetails), context.SchemaRepository);

        foreach ((string status, string id) in ProblemResponseIds)
        {
            swaggerDoc.Components.Responses[id] = new OpenApiResponse
            {
                Description = Reasons[status],
                Content = new Dictionary<string, OpenApiMediaType> { [ProblemResponseAttribute.ProblemContentType] = new() { Schema = status == "400" ? validation : problem } },
                Headers = SharedHeaders(status),
            };
        }

        DescriptionText.CleanDocument(swaggerDoc);

        swaggerDoc.Components.Headers["X-Correlation-Id"] = Header("The request's correlation id: echoed from the request header when valid, otherwise generated.", new OpenApiSchema { Type = "string" });
        swaggerDoc.Components.Headers["ETag"] = Header("The resource's version, quoted (for example `\"3\"`). Send it back as If-Match on the next change.", new OpenApiSchema { Type = "string" });
        swaggerDoc.Components.Headers["Location"] = Header("URL of the created resource.", new OpenApiSchema { Type = "string", Format = "uri" });
        swaggerDoc.Components.Headers["X-Next-Cursor"] = Header("Opaque cursor for the next page; absent on the last page. Pass it as the `cursor` query parameter.", new OpenApiSchema { Type = "string" });
        swaggerDoc.Components.Headers["Link"] = Header("RFC 8288 link to the next page (`rel=\"next\"`); absent on the last page.", new OpenApiSchema { Type = "string" });
        swaggerDoc.Components.Headers["Idempotent-Replayed"] = Header("`true` when this response is a replay of the first response for the same Idempotency-Key; absent on the first.", new OpenApiSchema { Type = "boolean" });
        swaggerDoc.Components.Headers["Retry-After"] = Header("Seconds to wait before retrying.", new OpenApiSchema { Type = "integer", Format = "int32" });
    }

    private static Dictionary<string, OpenApiHeader> SharedHeaders(string status)
    {
        var headers = new Dictionary<string, OpenApiHeader>
        {
            ["X-Correlation-Id"] = new() { Reference = new OpenApiReference { Type = ReferenceType.Header, Id = "X-Correlation-Id" } },
        };
        if (status is "429" or "503")
        {
            headers["Retry-After"] = new() { Reference = new OpenApiReference { Type = ReferenceType.Header, Id = "Retry-After" } };
        }

        return headers;
    }

    private static OpenApiHeader Header(string description, OpenApiSchema schema) => new() { Description = description, Schema = schema };

}
