using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fugue.Api.OpenApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using Swashbuckle.AspNetCore.Swagger;

namespace Fugue.Tests.OpenApi;

/// <summary>
/// Builds the OpenAPI document in memory (no database is touched: the host starts, nothing queries) and renders it deterministically:
/// every object's keys sorted ordinally, two-space indent, LF line ends, one trailing newline. The result is what
/// <c>docs/api/openapi.v1.json</c> holds, byte for byte.
/// </summary>
internal static class OpenApiSnapshot
{
    public const string RelativePath = "docs/api/openapi.v1.json";

    public static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Fugue.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Fugue.sln not found above " + AppContext.BaseDirectory);
    }

    public static string SnapshotPath() => Path.Combine(RepoRoot(), RelativePath.Replace('/', Path.DirectorySeparatorChar));

    public static WebApplicationFactory<Program> Host(string? environment = null, params (string Key, string Value)[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            if (environment is not null)
            {
                builder.UseEnvironment(environment);
            }

            // A well-formed connection string that is never opened: generating the document does not query.
            builder.UseSetting("ConnectionStrings:Postgres", "Host=localhost;Database=unused;Username=unused;Password=unused");
            TestAuth.Apply(builder, TestAuth.NewKey());
            foreach ((string key, string value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    public static OpenApiDocument Generate()
    {
        using WebApplicationFactory<Program> host = Host();
        return host.Services.GetRequiredService<ISwaggerProvider>().GetSwagger(OpenApiExtensions.DocumentName);
    }

    public static JsonNode GenerateNode() => Canonicalize(ToNode(Generate()));

    public static string Render() => ToText(GenerateNode());

    public static JsonNode ToNode(OpenApiDocument document)
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        document.SerializeAsV3(new OpenApiJsonWriter(text));
        return JsonNode.Parse(text.ToString())!;
    }

    public static string ToText(JsonNode node)
    {
        string json = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        return json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    /// <summary>Sorts every object's keys ordinally and the string lists <c>required</c> and <c>tags</c>, so the text does not depend on reflection order.</summary>
    public static JsonNode Canonicalize(JsonNode node, string? key = null)
    {
        switch (node)
        {
            case JsonObject obj:
                var sorted = new JsonObject();
                foreach (KeyValuePair<string, JsonNode?> pair in obj.OrderBy(p => p.Key, StringComparer.Ordinal).ToList())
                {
                    JsonNode? value = pair.Value;
                    obj.Remove(pair.Key);
                    sorted[pair.Key] = value is null ? null : Canonicalize(value, pair.Key);
                }

                return sorted;
            case JsonArray array:
                List<JsonNode?> items = [.. array.Select(i => i is null ? null : Canonicalize(i))];
                array.Clear();
                if (key is "required" or "tags" && items.All(i => i is JsonValue))
                {
                    items = [.. items.OrderBy(i => i!.GetValue<string>(), StringComparer.Ordinal)];
                }

                foreach (JsonNode? item in items)
                {
                    array.Add(item);
                }

                return array;
            default:
                return node;
        }
    }
}
