using System.Text.RegularExpressions;
using Microsoft.OpenApi.Models;

namespace Fugue.Api.OpenApi;

/// <summary>
/// Tidies the text taken from XML doc comments so the document reads the same on every machine: line breaks (CRLF or LF, plus the indentation
/// of a wrapped comment) become one space, and a <c>cref</c> that Swashbuckle printed as a full name (<c>Fugue.Application.Features.Customers.Models.CustomerDto.Version</c>)
/// becomes the member name in code style.
/// </summary>
internal static partial class DescriptionText
{
    public static string? Clean(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        string flat = Whitespace().Replace(text, " ").Trim();
        return FullName().Replace(flat, "`$1`");
    }

    public static void CleanDocument(OpenApiDocument document)
    {
        var seen = new HashSet<OpenApiSchema>(ReferenceEqualityComparer.Instance);
        foreach (OpenApiSchema schema in document.Components.Schemas.Values)
        {
            CleanSchema(schema, seen);
        }

        foreach (OpenApiTag tag in document.Tags ?? [])
        {
            tag.Description = Clean(tag.Description);
        }

        foreach (OpenApiOperation operation in document.Paths.Values.SelectMany(p => p.Operations.Values))
        {
            operation.Summary = Clean(operation.Summary);
            operation.Description = Clean(operation.Description);
            foreach (OpenApiParameter parameter in operation.Parameters ?? [])
            {
                parameter.Description = Clean(parameter.Description);
            }

            foreach (OpenApiResponse response in operation.Responses.Values.Where(r => r.Reference is null))
            {
                response.Description = Clean(response.Description) ?? string.Empty;
            }
        }
    }

    private static void CleanSchema(OpenApiSchema? schema, HashSet<OpenApiSchema> seen)
    {
        if (schema is null || schema.Reference is not null || !seen.Add(schema))
        {
            return;
        }

        schema.Description = Clean(schema.Description);
        foreach (OpenApiSchema property in schema.Properties.Values)
        {
            CleanSchema(property, seen);
        }

        CleanSchema(schema.Items, seen);
        CleanSchema(schema.AdditionalProperties, seen);
    }

    [GeneratedRegex(@"\s*\r?\n\s*")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\bFugue(?:\.[A-Za-z0-9_]+)+\.([A-Za-z0-9_]+)\b")]
    private static partial Regex FullName();
}
