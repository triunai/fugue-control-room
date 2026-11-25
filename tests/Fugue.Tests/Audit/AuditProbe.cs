using System.Text.Json;
using Fugue.Tests.Persistence;
using Npgsql;

namespace Fugue.Tests.Audit;

/// <summary>One <c>audit_log</c> row as the tests read it straight from Postgres (not through the API).</summary>
public sealed record AuditRow(
    Guid Id, DateTimeOffset OccurredAt, Guid? ActorUserId, string ActorType, string? ActorDisplay, string? PermissionUsed, string Action, string? EntityType,
    string? EntityId, string Outcome, string? ErrorCode, JsonElement? Changes, string? CorrelationId, string? TraceId, string? HttpMethod,
    string? RouteTemplate, string? ClientIp, string? UserAgent, JsonElement? Metadata)
{
    public JsonElement Change(string field) => Changes!.Value.GetProperty(field);

    public string[] ChangedFields => Changes is { } c ? c.EnumerateObject().Select(p => p.Name).Order().ToArray() : [];
}

internal static class AuditProbe
{
    private const string Columns =
        "id, occurred_at, actor_user_id, actor_type, actor_display, permission_used, action, entity_type, entity_id, outcome, error_code, changes::text, " +
        "correlation_id, trace_id, http_method, route_template, client_ip, user_agent, metadata::text";

    public static Task<List<AuditRow>> ByCorrelationAsync(PostgresFixture db, string correlationId) =>
        QueryAsync(db, "correlation_id = @v", correlationId);

    public static Task<List<AuditRow>> ByEntityAsync(PostgresFixture db, string entityType, string entityId) =>
        QueryAsync(db, "entity_type = @t AND entity_id = @v", entityId, ("t", entityType));

    private static async Task<List<AuditRow>> QueryAsync(PostgresFixture db, string where, string value, params (string Name, object Value)[] extra)
    {
        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new($"SELECT {Columns} FROM audit_log WHERE {where} ORDER BY occurred_at, id", connection);
        command.Parameters.AddWithValue("v", value);
        foreach ((string name, object v) in extra)
        {
            command.Parameters.AddWithValue(name, v);
        }

        var rows = new List<AuditRow>();
        await using NpgsqlDataReader r = await command.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(new AuditRow(
                r.GetGuid(0), r.GetFieldValue<DateTimeOffset>(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), Str(r, 4), Str(r, 5), r.GetString(6), Str(r, 7),
                Str(r, 8), r.GetString(9), Str(r, 10), Json(r, 11), Str(r, 12), Str(r, 13), Str(r, 14), Str(r, 15), Str(r, 16), Str(r, 17), Json(r, 18)));
        }

        return rows;
    }

    public static async Task<long> CountContainingAsync(PostgresFixture db, string fragment)
    {
        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new("SELECT count(*) FROM audit_log a WHERE a::text ILIKE @f", connection);
        command.Parameters.AddWithValue("f", "%" + fragment + "%");
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public static async Task<Guid> UserIdAsync(PostgresFixture db, string email)
    {
        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new("SELECT id FROM users WHERE email = @e", connection);
        command.Parameters.AddWithValue("e", email.ToLowerInvariant());
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static string? Str(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

    private static JsonElement? Json(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : JsonDocument.Parse(r.GetString(i)).RootElement.Clone();
}
