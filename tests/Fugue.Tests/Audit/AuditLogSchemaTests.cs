using Fugue.Domain.Identity;
using Fugue.Infrastructure.Persistence;
using Fugue.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Fugue.Tests.Audit;

/// <summary>Migration pin tests for <c>audit_log</c> (migration-retrofit-qa step 4): schema facts, the append-only trigger, the seeded permission, the rollback.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AuditLogSchemaTests(PostgresFixture db)
{
    private const string InsertSql =
        "INSERT INTO audit_log (id, occurred_at, actor_type, action, outcome) VALUES (@id, now(), 'system', 'test.insert', 'success')";

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task<Guid> InsertRowAsync()
    {
        await using NpgsqlConnection connection = await OpenAsync();
        Guid id = Guid.NewGuid();
        await using NpgsqlCommand insert = new(InsertSql, connection);
        insert.Parameters.AddWithValue("id", id);
        await insert.ExecuteNonQueryAsync();
        return id;
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using NpgsqlConnection connection = await OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task The_table_has_the_columns_the_writers_and_the_read_api_depend_on()
    {
        string[] expected =
        [
            "action", "actor_display", "actor_type", "actor_user_id", "changes", "client_ip", "correlation_id", "duration_ms", "entity_id", "entity_type",
            "error_code", "error_message", "error_type", "http_method", "id", "metadata", "occurred_at", "outcome", "permission_used", "route_template", "saga_id",
            "tenant_id", "trace_id", "user_agent",
        ];

        await using NpgsqlConnection connection = await OpenAsync();
        await using NpgsqlCommand command = new("SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'audit_log' ORDER BY column_name", connection);
        var columns = new Dictionary<string, string>();
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                columns[reader.GetString(0)] = reader.GetString(1);
            }
        }

        Assert.Equal(expected, columns.Keys.Order().ToArray());
        Assert.Equal("jsonb", columns["changes"]);
        Assert.Equal("jsonb", columns["metadata"]);
        Assert.Equal("timestamp with time zone", columns["occurred_at"]);
        Assert.Equal(("text", "text", "integer"), (columns["error_type"], columns["error_message"], columns["duration_ms"]));
    }

    [Fact]
    public async Task The_failure_detail_columns_are_nullable_and_the_trigger_still_blocks_changes_to_a_row_that_has_them()
    {
        Guid id = Guid.NewGuid();
        await using (NpgsqlConnection connection = await OpenAsync())
        {
            await using NpgsqlCommand insert = new(
                "INSERT INTO audit_log (id, occurred_at, actor_type, action, outcome, error_type, error_message, duration_ms) VALUES (@id, now(), 'system', 'test.detail', 'failure', 'Conflict', 'taken', 12)",
                connection);
            insert.Parameters.AddWithValue("id", id);
            await insert.ExecuteNonQueryAsync();
        }

        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM audit_log WHERE action = 'test.detail' AND duration_ms = 12 AND error_type = 'Conflict'"));
        Guid bare = await InsertRowAsync(); // the old insert shape (no detail columns) still works
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM audit_log WHERE id = @id AND error_type IS NULL AND error_message IS NULL AND duration_ms IS NULL", ("id", bare)));

        await using NpgsqlConnection other = await OpenAsync();
        await using NpgsqlCommand update = new("UPDATE audit_log SET duration_ms = 0 WHERE id = @id", other);
        update.Parameters.AddWithValue("id", id);
        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());
        Assert.Equal("23001", ex.SqlState);
    }

    [Fact]
    public async Task The_query_indexes_exist_and_are_ordered_newest_first_for_keyset_paging()
    {
        string definitions = await ScalarAsync<string>("SELECT string_agg(indexdef, E'\\n' ORDER BY indexname) FROM pg_indexes WHERE tablename = 'audit_log'");

        Assert.Contains("(occurred_at DESC, id DESC)", definitions);
        Assert.Contains("(entity_type, entity_id, occurred_at DESC, id DESC)", definitions);
        Assert.Contains("(actor_user_id, occurred_at DESC, id DESC)", definitions);
        Assert.Contains("(action, occurred_at DESC, id DESC)", definitions);
        Assert.Contains("(correlation_id)", definitions);
    }

    [Fact]
    public async Task An_update_is_rejected_by_the_append_only_trigger()
    {
        Guid id = await InsertRowAsync();
        await using NpgsqlConnection connection = await OpenAsync();
        await using NpgsqlCommand update = new("UPDATE audit_log SET action = 'tampered' WHERE id = @id", connection);
        update.Parameters.AddWithValue("id", id);

        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());

        Assert.Equal("23001", ex.SqlState);
        Assert.Contains("append-only", ex.MessageText);
        Assert.Equal("test.insert", await ScalarAsync<string>("SELECT action FROM audit_log WHERE id = @id", ("id", id)));
    }

    [Fact]
    public async Task A_delete_is_rejected_by_the_append_only_trigger()
    {
        Guid id = await InsertRowAsync();
        await using NpgsqlConnection connection = await OpenAsync();
        await using NpgsqlCommand delete = new("DELETE FROM audit_log WHERE id = @id", connection);
        delete.Parameters.AddWithValue("id", id);

        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync());

        Assert.Equal("23001", ex.SqlState);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM audit_log WHERE id = @id", ("id", id)));
    }

    [Fact]
    public async Task A_truncate_is_rejected_by_the_append_only_trigger()
    {
        await InsertRowAsync();
        await using NpgsqlConnection connection = await OpenAsync();
        await using NpgsqlCommand truncate = new("TRUNCATE audit_log", connection);

        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => truncate.ExecuteNonQueryAsync());

        Assert.Equal("23001", ex.SqlState);
        Assert.True(await ScalarAsync<long>("SELECT count(*) FROM audit_log") > 0);
    }

    [Fact]
    public async Task Actor_type_and_outcome_are_constrained()
    {
        await using NpgsqlConnection connection = await OpenAsync();
        await using NpgsqlCommand bad = new("INSERT INTO audit_log (id, occurred_at, actor_type, action, outcome) VALUES (@id, now(), 'robot', 'x.y', 'maybe')", connection);
        bad.Parameters.AddWithValue("id", Guid.NewGuid());

        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => bad.ExecuteNonQueryAsync());

        Assert.Equal("23514", ex.SqlState);
    }

    [Fact]
    public async Task The_audit_module_and_permission_are_seeded_and_only_admin_holds_it()
    {
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM modules WHERE code = 'AUDIT' AND is_active AND NOT is_deleted"));
        string roles = await ScalarAsync<string>(
            """
            SELECT string_agg(r.code, ',' ORDER BY r.code) FROM role_permissions rp
            JOIN permissions p ON p.id = rp.permission_id JOIN roles r ON r.id = rp.role_id WHERE p.code = 'AUDIT_READ'
            """);

        Assert.Equal(RoleCodes.Admin, roles);
        Assert.Equal(Permissions.All.Count, (int)await ScalarAsync<long>("SELECT count(*) FROM permissions"));
    }
}

/// <summary>The rollback written in <c>Down()</c> actually runs, and the migration applies again afterwards (retrofit QA step 3).</summary>
public sealed class AuditLogMigrationRollbackTests
{
    [Fact]
    public async Task Down_removes_the_table_the_function_and_the_seed_and_up_applies_again()
    {
        await using PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await container.StartAsync();
        var builder = new DbContextOptionsBuilder<FugueDbContext>();
        PersistenceOptions.Configure(builder, container.GetConnectionString());
        await using var context = new FugueDbContext(builder.Options);
        IMigrator migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync();
        Assert.True(await ExistsAsync(container, "SELECT to_regclass('audit_log') IS NOT NULL"));

        await migrator.MigrateAsync("AddDeliveryClaimToken");
        Assert.False(await ExistsAsync(container, "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'audit_log' AND column_name IN ('error_type', 'error_message', 'duration_ms'))"));
        Assert.True(await ExistsAsync(container, "SELECT to_regclass('audit_log') IS NOT NULL"));

        await migrator.MigrateAsync("AddIdentity");
        Assert.False(await ExistsAsync(container, "SELECT to_regclass('audit_log') IS NOT NULL"));
        Assert.False(await ExistsAsync(container, "SELECT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'audit_log_reject_change')"));
        Assert.False(await ExistsAsync(container, "SELECT EXISTS (SELECT 1 FROM permissions WHERE code = 'AUDIT_READ')"));
        Assert.False(await ExistsAsync(container, "SELECT EXISTS (SELECT 1 FROM modules WHERE code = 'AUDIT')"));

        await migrator.MigrateAsync();
        Assert.True(await ExistsAsync(container, "SELECT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'trg_audit_log_no_truncate')"));
    }

    private static async Task<bool> ExistsAsync(PostgreSqlContainer container, string sql)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
