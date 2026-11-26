using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fugue.Tests.OpenApi;

/// <summary>
/// Properties of the generated contract that the snapshot alone would let drift unnoticed (the snapshot only says "same as before"):
/// stable operation ids, one error format, typed ids and enums as strings, the headers a client relies on, and where the document is served.
/// </summary>
public sealed partial class OpenApiDocumentTests
{
    private static readonly Lazy<JsonObject> Document = new(() => OpenApiSnapshot.GenerateNode().AsObject());

    private static IEnumerable<(string Path, string Method, JsonObject Operation)> Operations() =>
        Document.Value["paths"]!.AsObject().SelectMany(p => p.Value!.AsObject()
            .Select(m => (p.Key, m.Key, m.Value!.AsObject())));

    private static JsonObject Components(string kind) => Document.Value["components"]![kind]!.AsObject();

    /// <summary>Resolves a response that is a <c>$ref</c> to a shared component.</summary>
    private static JsonObject Resolve(JsonObject response) =>
        response["$ref"]?.GetValue<string>() is { } reference ? Components("responses")[reference.Split('/')[^1]]!.AsObject() : response;

    [GeneratedRegex("^[A-Z][A-Za-z]+_[A-Z][A-Za-z]+$")]
    private static partial Regex StableOperationId();

    [Fact]
    public void Generation_is_deterministic()
    {
        Assert.Equal(OpenApiSnapshot.Render(), OpenApiSnapshot.Render());
    }

    [Fact]
    public void Operation_ids_are_unique_and_Controller_Action_shaped()
    {
        string[] ids = [.. Operations().Select(o => o.Operation["operationId"]!.GetValue<string>())];

        Assert.All(ids, id => Assert.Matches(StableOperationId(), id));
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("Customers_List", ids);
        Assert.Contains("Me_Get", ids);
    }

    [Fact]
    public void Only_the_anonymous_operations_have_no_bearer_requirement()
    {
        string[] open = [.. Operations().Where(o => o.Operation["security"] is null).Select(o => o.Operation["operationId"]!.GetValue<string>()).Order(StringComparer.Ordinal)];

        Assert.Equal(["Auth_Logout", "Auth_Refresh", "Auth_Token", "DevWebhookSink_Receive", "Diagnostics_Ping"], open);
        Assert.All(
            Operations().Where(o => o.Operation["security"] is not null),
            o => Assert.Equal("Bearer", o.Operation["security"]![0]!.AsObject().Single().Key));
        Assert.Equal("bearer", Components("securitySchemes")["Bearer"]!["scheme"]!.GetValue<string>());
    }

    [Fact]
    public void Every_operation_has_a_success_response_and_every_error_is_problem_json()
    {
        foreach ((string path, string method, JsonObject operation) in Operations())
        {
            JsonObject responses = operation["responses"]!.AsObject();
            Assert.Contains(responses, r => r.Key.StartsWith('2')); // success declared
            foreach ((string status, JsonNode? node) in responses.Where(r => !r.Key.StartsWith('2')))
            {
                JsonObject content = Resolve(node!.AsObject())["content"]!.AsObject();
                Assert.True(content.Count == 1 && content.ContainsKey("application/problem+json"), $"{method} {path} {status} is not problem+json only");
            }
        }
    }

    [Fact]
    public void No_text_plain_or_text_json_media_types_are_published()
    {
        Assert.DoesNotContain("text/plain", OpenApiSnapshot.Render(), StringComparison.Ordinal);
        Assert.DoesNotContain("text/json", OpenApiSnapshot.Render(), StringComparison.Ordinal);
    }

    [Fact]
    public void Authenticated_operations_document_401_and_permissioned_ones_403()
    {
        foreach ((string path, string method, JsonObject operation) in Operations().Where(o => o.Operation["security"] is not null))
        {
            Assert.True(operation["responses"]!.AsObject().ContainsKey("401"), $"{method} {path} lacks 401");
        }

        Assert.True(Document.Value["paths"]!["/api/v1/modules"]!["get"]!["responses"]!.AsObject().ContainsKey("403"));
        Assert.False(Document.Value["paths"]!["/api/v1/me"]!["get"]!["responses"]!.AsObject().ContainsKey("403")); // any signed-in user may call /me
    }

    [Fact]
    public void A_conditional_operation_documents_If_Match_412_428_and_returns_an_ETag()
    {
        var conditional = Operations().Where(o => o.Operation["parameters"]?.AsArray().Any(p => p!["name"]!.GetValue<string>() == "If-Match") == true).ToList();

        Assert.True(conditional.Count >= 10);
        foreach ((string path, string method, JsonObject operation) in conditional)
        {
            JsonObject responses = operation["responses"]!.AsObject();
            Assert.True(responses.ContainsKey("412") && responses.ContainsKey("428"), $"{method} {path} lacks 412 or 428");
            JsonObject success = responses.First(r => r.Key.StartsWith('2')).Value!.AsObject();
            Assert.True(success["headers"]!.AsObject().ContainsKey("ETag"), $"{method} {path} success lacks ETag");
        }

        // Create-or-change: the header is optional there (it is required only to change an existing override).
        JsonNode putOverride = Document.Value["paths"]!["/api/v1/overrides/{userId}/{permission}"]!["put"]!["parameters"]!.AsArray().Single(p => p!["name"]!.GetValue<string>() == "If-Match")!;
        Assert.Null(putOverride["required"]);
    }

    [Fact]
    public void Lists_expose_the_paging_headers_and_creates_the_location()
    {
        JsonObject list = Document.Value["paths"]!["/api/v1/customers"]!["get"]!["responses"]!["200"]!["headers"]!.AsObject();
        Assert.True(list.ContainsKey("X-Next-Cursor") && list.ContainsKey("Link") && list.ContainsKey("X-Correlation-Id"));

        JsonObject created = Document.Value["paths"]!["/api/v1/customers"]!["post"]!["responses"]!["201"]!["headers"]!.AsObject();
        Assert.True(created.ContainsKey("Location") && created.ContainsKey("ETag"));

        Assert.True(Components("headers").ContainsKey("Retry-After"));
        Assert.True(Resolve(new JsonObject { ["$ref"] = "#/components/responses/ServiceUnavailable" })["headers"]!.AsObject().ContainsKey("Retry-After"));
        Assert.True(Resolve(new JsonObject { ["$ref"] = "#/components/responses/TooManyRequests" })["headers"]!.AsObject().ContainsKey("Retry-After"));
    }

    [Fact]
    public void Typed_ids_are_uuid_strings_and_enums_are_strings()
    {
        JsonObject customer = Components("schemas")["CustomerDto"]!.AsObject();
        Assert.Equal("string", customer["properties"]!["id"]!["type"]!.GetValue<string>());
        Assert.Equal("uuid", customer["properties"]!["id"]!["format"]!.GetValue<string>());
        Assert.Equal("uuid", customer["properties"]!["ownerId"]!["format"]!.GetValue<string>());
        Assert.True(customer["properties"]!["ownerId"]!["nullable"]!.GetValue<bool>());

        JsonNode idParameter = Document.Value["paths"]!["/api/v1/customers/{id}"]!["get"]!["parameters"]![0]!["schema"]!;
        Assert.Equal("uuid", idParameter["format"]!.GetValue<string>());

        string[] typedIds = ["CustomerId", "UserId", "TenantId", "WebhookSubscriptionId", "WebhookDeliveryId"];
        Assert.All(typedIds, name => Assert.False(Components("schemas").ContainsKey(name), $"{name} leaked as an object schema"));

        JsonObject status = Components("schemas")["CustomerStatus"]!.AsObject();
        Assert.Equal("string", status["type"]!.GetValue<string>());
        Assert.Equal(["Active", "Suspended"], status["enum"]!.AsArray().Select(e => e!.GetValue<string>()));
    }

    [Fact]
    public void Non_nullable_properties_are_required_and_nullable_ones_are_not()
    {
        JsonObject customer = Components("schemas")["CustomerDto"]!.AsObject();
        string[] required = [.. customer["required"]!.AsArray().Select(r => r!.GetValue<string>())];

        Assert.Contains("id", required);
        Assert.Contains("version", required);
        Assert.DoesNotContain("phone", required);
        Assert.DoesNotContain("ownerId", required);
    }

    [Fact]
    public void Problem_bodies_carry_code_and_correlation_members()
    {
        JsonObject properties = Components("schemas")["ProblemDetails"]!["properties"]!.AsObject();

        foreach (string member in new[] { "code", "traceId", "correlationId", "title", "status", "detail", "type" })
        {
            Assert.True(properties.ContainsKey(member), member);
        }

        Assert.True(Components("schemas")["ValidationProblemDetails"]!["properties"]!.AsObject().ContainsKey("errors"));
    }

    [Fact]
    public void Only_the_development_sink_is_marked_development_only()
    {
        string[] marked = [.. Operations().Where(o => o.Operation.ContainsKey("x-development-only")).Select(o => o.Path).Distinct().Order(StringComparer.Ordinal)];

        Assert.Equal(["/api/v1/dev/webhook-sink", "/api/v1/dev/webhook-sink/secret"], marked);
    }

    [Fact]
    public void The_me_operation_returns_the_me_contract()
    {
        JsonNode schema = Document.Value["paths"]!["/api/v1/me"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!;

        Assert.Equal("#/components/schemas/MeDto", schema["$ref"]!.GetValue<string>());
    }

    [Fact]
    public async Task The_json_and_the_ui_are_served_in_Development_and_do_not_exist_anywhere_else()
    {
        using WebApplicationFactory<Program> development = OpenApiSnapshot.Host("Development");
        using WebApplicationFactory<Program> production = OpenApiSnapshot.Host("Production");
        using WebApplicationFactory<Program> staging = OpenApiSnapshot.Host("Staging");

        HttpResponseMessage json = await development.CreateClient().GetAsync("/swagger/v1/swagger.json");
        HttpResponseMessage ui = await development.CreateClient().GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, json.StatusCode);
        Assert.Contains("\"openapi\"", await json.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        foreach (WebApplicationFactory<Program> other in new[] { production, staging })
        {
            using HttpClient client = other.CreateClient();
            foreach (string path in new[] { "/swagger/v1/swagger.json", "/swagger/index.html", "/swagger" })
            {
                // An unmapped path answers like any other unknown path: 401 to an anonymous caller (the fallback policy), never the document or the UI.
                HttpResponseMessage response = await client.GetAsync(path);
                Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized, $"{path} answered {(int)response.StatusCode}");
                Assert.DoesNotContain("openapi", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
