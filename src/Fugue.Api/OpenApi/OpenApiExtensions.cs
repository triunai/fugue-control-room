using Fugue.Api.Modules;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Fugue.Api.OpenApi;

/// <summary>
/// The OpenAPI document (<c>v1</c>) is the API's contract with its clients (D-042): generated from the controllers, the XML doc comments
/// and the attributes, and pinned by a committed snapshot (<c>docs/api/openapi.v1.json</c>) that a test compares on every run.
/// The document is always buildable; the Swagger UI and the live JSON route exist only in Development.
/// </summary>
public static class OpenApiExtensions
{
    public const string DocumentName = "v1";
    public const string SecuritySchemeName = "Bearer";

    public static IServiceCollection AddFugueOpenApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(DocumentName, new OpenApiInfo
            {
                Title = "Fugue API",
                Version = DocumentName,
                Description = "REST API of Fugue. Every error is an RFC 9457 `application/problem+json` body with a stable `code`. "
                    + "Mutations of a versioned resource need `If-Match` with the ETag of the last read. Lists are keyset paged: follow `X-Next-Cursor`. "
                    + "Send `X-Correlation-Id` (and optionally `X-Saga-Id`) to correlate calls; both are echoed. "
                    + "Authenticate with `POST /api/v1/auth/token` and send the access token as a Bearer token.",
            });

            options.CustomOperationIds(api =>
                api.ActionDescriptor is ControllerActionDescriptor action ? $"{action.ControllerName}_{action.ActionName}" : null);

            options.AddSecurityDefinition(SecuritySchemeName, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access token from `POST /api/v1/auth/token`. Permissions are looked up per request, so a role change takes effect immediately.",
            });
            options.SupportNonNullableReferenceTypes();
            TypedIdSchemas.Map(options);
            options.SchemaFilter<FugueSchemaFilter>();
            options.OperationFilter<FugueOperationFilter>();

            foreach (string xml in Directory.EnumerateFiles(AppContext.BaseDirectory, "Fugue.*.xml").Order(StringComparer.Ordinal))
            {
                options.IncludeXmlComments(xml, includeControllerXmlComments: true);
            }

            // Last: it tidies the text the XML-comment filters just added.
            options.DocumentFilter<FugueDocumentFilter>();
        });
        return services;
    }

    /// <summary>Swagger UI at <c>/swagger</c> and the JSON at <c>/swagger/v1/swagger.json</c>; Development only (anywhere else the routes do not exist).</summary>
    public static WebApplication UseFugueOpenApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(ui =>
            {
                ui.SwaggerEndpoint($"/swagger/{DocumentName}/swagger.json", "Fugue API v1");
                ui.RoutePrefix = "swagger";
                ui.DisplayOperationId();
            });
        }

        return app;
    }
}
