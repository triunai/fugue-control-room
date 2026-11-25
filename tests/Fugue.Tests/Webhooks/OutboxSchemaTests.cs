using Fugue.Domain.Identity;
using Fugue.Infrastructure.Persistence;
using Fugue.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Fugue.Tests.Webhooks;

/// <summary>Migration pin tests for the outbox and webhook tables (migration-retrofit-qa step 4): schema facts, the immutability trigger, the seed, the rollback.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class OutboxSchemaTests(PostgresFixture db)
{
    private const string InsertMessage = """
        INSERT INTO outbox_messages (id, occurred_at, event_type, aggregate_type, aggregate_id, payload, saga_id, status, attempts, next_attempt_at)
        VALUES (@id, now(), 'test.thing.v1', 'thing', 'agg-1', CAST('{"a":1}' AS jsonb), 'agg-1', 'pending', 0, now())
        """;

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task<int> ExecAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using NpgsqlConnection connection = await OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync();
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

    private async Task<Guid> NewMessageAsync()
    {
        Guid id = Guid.NewGuid();
        await ExecAsync(InsertMessage, ("id", id));
        return id;
    }

    [Fact]
    public async Task The_outbox_columns_are_what_the_writers_and_the_dispatcher_depend_on()
    {
        string columns = await ScalarAsync<string>(
            "SELECT string_agg(column_name, ',' ORDER BY column_name) FROM information_schema.columns WHERE table_name = 'outbox_messages'");

        Assert.Equal(
            "aggregate_id,aggregate_type,attempts,correlation_id,dispatched_at,event_type,id,last_error,next_attempt_at,occurred_at,payload,saga_id,status,traceparent",
            columns);
        Assert.Equal("jsonb", await ScalarAsync<string>("SELECT data_type FROM information_schema.columns WHERE table_name = 'outbox_messages' AND column_name = 'payload'"));
    }

    [Fact]
    public async Task The_delivery_table_is_unique_per_message_and_subscription_and_has_the_partial_due_index()
    {
        string indexes = await ScalarAsync<string>("SELECT string_agg(indexdef, E'\\n') FROM pg_indexes WHERE tablename = 'webhook_deliveries'");

        Assert.Contains("UNIQUE INDEX ux_webhook_deliveries_message_subscription", indexes);
        Assert.Contains("WHERE ((status)::text = ANY", indexes);
        Assert.Contains("(occurred_at DESC, id DESC)", indexes);
    }

    [Fact]
    public async Task Status_may_move_but_the_payload_and_the_event_identity_are_frozen()
    {
        Guid id = await NewMessageAsync();

        Assert.Equal(1, await ExecAsync("UPDATE outbox_messages SET status = 'dispatched', attempts = 1, dispatched_at = now(), last_error = 'x' WHERE id = @id", ("id", id)));

        foreach (string tamper in new[]
        {
            "UPDATE outbox_messages SET payload = CAST('{\"a\":2}' AS jsonb) WHERE id = @id",
            "UPDATE outbox_messages SET event_type = 'other.v1' WHERE id = @id",
            "UPDATE outbox_messages SET aggregate_id = 'other' WHERE id = @id",
            "UPDATE outbox_messages SET saga_id = 'other' WHERE id = @id",
            "UPDATE outbox_messages SET occurred_at = now() + interval '1 day' WHERE id = @id",
        })
        {
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => ExecAsync(tamper, ("id", id)));
            Assert.Equal("23001", ex.SqlState);
        }

        Assert.Equal("{\"a\": 1}", await ScalarAsync<string>("SELECT payload::text FROM outbox_messages WHERE id = @id", ("id", id)));
    }

    [Fact]
    public async Task Rows_can_be_deleted_for_retention_and_take_their_deliveries_with_them()
    {
        Guid message = await NewMessageAsync();
        Guid subscription = Guid.NewGuid();
        await ExecAsync(
            "INSERT INTO webhook_subscriptions (id, name, url, event_types, is_active, secret_ciphertext, created_at, created_by, updated_at, updated_by) VALUES (@s, 'n', 'https://e.test/h', ARRAY['*'], true, 'c', now(), 'x', now(), 'x')",
            ("s", subscription));
        await ExecAsync(
            "INSERT INTO webhook_deliveries (id, message_id, subscription_id, occurred_at, status, attempts, next_attempt_at) VALUES (@d, @m, @s, now(), 'pending', 0, now())",
            ("d", Guid.NewGuid()), ("m", message), ("s", subscription));

        await ExecAsync("DELETE FROM outbox_messages WHERE id = @id", ("id", message));

        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM webhook_deliveries WHERE message_id = @m", ("m", message)));
    }

    [Fact]
    public async Task Status_values_are_constrained()
    {
        await Assert.ThrowsAsync<PostgresException>(() => ExecAsync(InsertMessage.Replace("'pending'", "'bogus'", StringComparison.Ordinal), ("id", Guid.NewGuid())));
    }

    [Fact]
    public async Task The_webhooks_module_and_permission_are_seeded_and_only_admin_holds_it()
    {
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM modules WHERE code = 'WEBHOOKS' AND is_active AND NOT is_deleted"));
        string roles = await ScalarAsync<string>(
            """
            SELECT string_agg(r.code, ',' ORDER BY r.code) FROM role_permissions rp
            JOIN permissions p ON p.id = rp.permission_id JOIN roles r ON r.id = rp.role_id WHERE p.code = 'WEBHOOKS_MANAGE'
            """);

        Assert.Equal(RoleCodes.Admin, roles);
    }
}

/// <summary>The rollback written in <c>Down()</c> runs, and the migration applies again afterwards (retrofit QA step 3).</summary>
public sealed class OutboxMigrationRollbackTests
{
    [Fact]
    public async Task Down_removes_the_tables_the_function_and_the_seed_and_up_applies_again()
    {
        await using PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await container.StartAsync();
        var builder = new DbContextOptionsBuilder<FugueDbContext>();
        PersistenceOptions.Configure(builder, container.GetConnectionString());
        await using var context = new FugueDbContext(builder.Options);
        IMigrator migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync();
        Assert.True(await ExistsAsync(container, "SELECT to_regclass('outbox_messages') IS NOT NULL"));

        await migrator.MigrateAsync("OverrideConcurrencyToken");
        foreach (string table in new[] { "outbox_messages", "webhook_subscriptions", "webhook_deliveries", "data_protection_keys" })
        {
            Assert.False(await ExistsAsync(container, $"SELECT to_regclass('{table}') IS NOT NULL"));
        }

        Assert.False(await ExistsAsync(container, "SELECT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'outbox_messages_reject_immutable_change')"));
        Assert.False(await ExistsAsync(container, "SELECT EXISTS (SELECT 1 FROM permissions WHERE code = 'WEBHOOKS_MANAGE')"));
        Assert.False(await ExistsAsync(container, "SELECT EXISTS (SELECT 1 FROM modules WHERE code = 'WEBHOOKS')"));

        await migrator.MigrateAsync();
        Assert.True(await ExistsAsync(container, "SELECT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'trg_outbox_messages_immutable')"));
    }

    private static async Task<bool> ExistsAsync(PostgreSqlContainer container, string sql)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
