using System.Globalization;
using Fugue.Api.Correlation;
using Fugue.Api.Idempotency;
using Fugue.Api.Modules;
using Fugue.Domain.Identity;
using Fugue.Domain.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Auth = Fugue.Api.Auth;

namespace Fugue.Api.OpenApi;

/// <summary>
/// Completes each operation with what the pipeline does around the action, so the declarations on the action stay short:
/// security (a bearer requirement unless the action is anonymous), the 400, 401, 403, 404 (module gate), 412, 428, 429 and 503 problems that
/// follow from attributes, the <c>If-Match</c> parameter, the response headers (<c>ETag</c>, <c>Location</c>, <c>Link</c>, <c>X-Next-Cursor</c>,
/// <c>X-Correlation-Id</c>, <c>Retry-After</c>), and one JSON media type per body instead of the formatters' four. Problem responses and
/// headers are shared components (<see cref="FugueDocumentFilter"/>); an action's own 404 or 409 keeps an inline response that names its codes.
/// </summary>
internal sealed class FugueOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        IList<object> metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        bool anonymous = metadata.OfType<IAllowAnonymous>().Any();
        IfMatchAttribute? ifMatchAttribute = context.MethodInfo.GetCustomAttributes(typeof(IfMatchAttribute), inherit: false).OfType<IfMatchAttribute>().FirstOrDefault();
        bool ifMatch = ifMatchAttribute is not null;
        bool idempotent = context.MethodInfo.GetCustomAttributes(typeof(IdempotentAttribute), inherit: false).Length > 0;

        // SYSTEM is the one module that can never be switched off, so its endpoints have no module gate to answer 404.
        bool gated = metadata.OfType<ModuleAttribute>().Any(m => m.Code != ModuleCodes.System);

        // Declared problem statuses first: they keep an inline response that lists the codes the action sends.
        foreach (ProblemResponseAttribute declared in context.MethodInfo.GetCustomAttributes(typeof(ProblemResponseAttribute), inherit: false).OfType<ProblemResponseAttribute>())
        {
            string key = declared.StatusCode.ToString(CultureInfo.InvariantCulture);
            IEnumerable<string> codes = declared.StatusCode == 404 && gated ? declared.Codes.Append("module.disabled") : declared.Codes;
            if (operation.Responses.TryGetValue(key, out OpenApiResponse? response) && declared.Codes.Count > 0)
            {
                response.Description = $"{FugueDocumentFilter.Reasons[key].Split(':')[0]}. Codes: {string.Join(", ", codes.Select(c => $"`{c}`"))}.";
            }
        }

        if (!anonymous)
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = OpenApiExtensions.SecuritySchemeName } }] = [],
            });
            Share(operation, 401);
        }

        if (operation.Parameters?.Any(p => p.In != ParameterLocation.Header) == true || operation.RequestBody is not null)
        {
            Share(operation, 400); // model binding, validation, a malformed id
        }

        if (metadata.OfType<Auth.HasPermissionAttribute>().Any())
        {
            Share(operation, 403);
        }

        if (gated)
        {
            Share(operation, 404);
        }

        if (ifMatch)
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "If-Match",
                In = ParameterLocation.Header,
                Required = ifMatchAttribute!.Required,
                Description = ifMatchAttribute.Required
                    ? "The version you last read, as the ETag value (for example `\"3\"`). A stale version is answered 412, a missing one 428."
                    : "The version you last read, as the ETag value (for example `\"3\"`). Required to change an existing resource (stale: 412, missing: 428); omit it when creating.",
                Schema = new OpenApiSchema { Type = "string" },
            });
            Share(operation, 412);
            Share(operation, 428);
        }

        if (idempotent)
        {
            AddIdempotency(operation, context);
        }

        if (metadata.OfType<EnableRateLimitingAttribute>().Any())
        {
            Share(operation, 429);
        }

        Share(operation, 503);

        bool paged = operation.Parameters?.Any(p => p.Name == "cursor" && p.In == ParameterLocation.Query) == true;
        foreach ((string status, OpenApiResponse response) in operation.Responses.ToList())
        {
            if (response.Reference is not null)
            {
                continue;
            }

            if (FugueDocumentFilter.Reasons.TryGetValue(status, out string? text) && response.Description is null or "" or "Success" or "Client Error" or "Server Error")
            {
                response.Description = text;
            }

            AddHeaders(context, response, status, ifMatch, paged, idempotent);
            TrimMediaTypes(response.Content);
        }

        if (operation.RequestBody is not null)
        {
            TrimMediaTypes(operation.RequestBody.Content);
        }

        if (context.MethodInfo.DeclaringType!.GetCustomAttributes(typeof(DevelopmentOnlyAttribute), inherit: true).Length > 0
            || context.MethodInfo.GetCustomAttributes(typeof(DevelopmentOnlyAttribute), inherit: true).Length > 0)
        {
            operation.Extensions["x-development-only"] = new OpenApiBoolean(true);
            operation.Description = (operation.Description is { Length: > 0 } d ? d + "\n\n" : string.Empty)
                + "Exists only when the API runs in the Development environment; any other environment answers 404.";
        }
    }

    /// <summary>Adds the shared problem response for <paramref name="status"/> unless the action declared its own.</summary>
    private static void Share(OpenApiOperation operation, int status)
    {
        string key = status.ToString(CultureInfo.InvariantCulture);
        if (!operation.Responses.ContainsKey(key))
        {
            operation.Responses[key] = new OpenApiResponse { Reference = new OpenApiReference { Type = ReferenceType.Response, Id = FugueDocumentFilter.ProblemResponseIds[key] } };
        }
    }

    private static void AddHeaders(OperationFilterContext context, OpenApiResponse response, string status, bool ifMatch, bool paged, bool idempotent)
    {
        response.Headers ??= new Dictionary<string, OpenApiHeader>();
        response.Headers[CorrelationHeaders.CorrelationId] = HeaderRef(CorrelationHeaders.CorrelationId);

        if (status.StartsWith('2') && (ifMatch || ReturnsVersioned(context, status)))
        {
            response.Headers["ETag"] = HeaderRef("ETag");
        }

        if (status == "201")
        {
            response.Headers["Location"] = HeaderRef("Location");
        }

        if (status == "200" && paged)
        {
            response.Headers["X-Next-Cursor"] = HeaderRef("X-Next-Cursor");
            response.Headers["Link"] = HeaderRef("Link");
        }

        if (status is "429" or "503" || (idempotent && status == "409"))
        {
            response.Headers["Retry-After"] = HeaderRef("Retry-After");
        }

        if (idempotent && status.StartsWith('2'))
        {
            response.Headers[IdempotencyFilter.ReplayedHeader] = HeaderRef(IdempotencyFilter.ReplayedHeader);
        }
    }

    /// <summary>
    /// An <c>[Idempotent]</c> action (ADR-013): the optional <c>Idempotency-Key</c> header, 413 and 422 as shared problems, and a 409 that lists the idempotency
    /// codes next to the action's own (for customers, <c>customer.email_taken</c>). <c>Retry-After</c> and <c>Idempotent-Replayed</c> are added with the headers.
    /// </summary>
    private static void AddIdempotency(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = IdempotencyKeyHeader.Name,
            In = ParameterLocation.Header,
            Required = context.MethodInfo.GetCustomAttributes(typeof(IdempotentAttribute), inherit: false).OfType<IdempotentAttribute>().Any(a => a.Required),
            Description = "Makes the request safe to retry. One quoted string (`\"order-7f3a\"`, with `\\\"` and `\\\\` escapes) or a plain token of letters, digits, `.`, `_`, `:` and `-`, 1 to 255 characters once decoded (the encoded header may be longer because of the quotes and escapes); "
                + "anything else is 400 `idempotency.key_invalid`. The same key with the same body returns the first response (with `Idempotent-Replayed: true`) for 24 hours instead of creating again; "
                + "the same key with a different body is 422. A request that failed with a 4xx frees the key; one whose outcome is unknown keeps it (409). Without the header the request is not idempotent.",
            Schema = new OpenApiSchema { Type = "string" },
        });

        string[] codes = [.. context.MethodInfo.GetCustomAttributes(typeof(ProblemResponseAttribute), inherit: false).OfType<ProblemResponseAttribute>()
            .Where(a => a.StatusCode == StatusCodes.Status409Conflict).SelectMany(a => a.Codes), "idempotency.in_progress", "idempotency.outcome_unknown"];
        if (!operation.Responses.TryGetValue("409", out OpenApiResponse? conflict))
        {
            OpenApiSchema problem = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository);
            conflict = new OpenApiResponse { Content = new Dictionary<string, OpenApiMediaType> { [ProblemResponseAttribute.ProblemContentType] = new() { Schema = problem } } };
            operation.Responses["409"] = conflict;
        }

        conflict.Description = $"{FugueDocumentFilter.Reasons["409"].Split(':')[0]}. Codes: {string.Join(", ", codes.Select(c => $"`{c}`"))}.";
        Share(operation, 413);
        Share(operation, 422);
    }

    private static OpenApiHeader HeaderRef(string id) =>
        new() { Reference = new OpenApiReference { Type = ReferenceType.Header, Id = id } };

    private static bool ReturnsVersioned(OperationFilterContext context, string status) =>
        context.ApiDescription.SupportedResponseTypes
            .Any(r => r.StatusCode.ToString(CultureInfo.InvariantCulture) == status && r.Type is { } t && typeof(IVersioned).IsAssignableFrom(t));

    private static void TrimMediaTypes(IDictionary<string, OpenApiMediaType>? content)
    {
        if (content is null)
        {
            return;
        }

        foreach (string key in content.Keys.ToList())
        {
            if (key is "text/plain" or "text/json" || (key.StartsWith("application/", StringComparison.Ordinal) && key.EndsWith("+json", StringComparison.Ordinal) && key != ProblemResponseAttribute.ProblemContentType))
            {
                content.Remove(key);
            }
        }
    }
}
