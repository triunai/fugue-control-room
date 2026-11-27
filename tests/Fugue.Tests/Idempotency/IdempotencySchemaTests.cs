using Fugue.Tests.Persistence;
using Npgsql;

namespace Fugue.Tests.Idempotency;

/// <summary>Migration pin tests for <c>idempotency_keys</c> (migration-retrofit-qa step 4): the columns the store depends on, the scope uniqueness and the status CHECK.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class IdempotencySchemaTests(PostgresFixture db)
{
    private const string Insert = """
        INSERT INTO idempotency_keys (id, actor_id, endpoint, key, request_hash, claim_token, status, expires_at, created_at, created_by, updated_at, updated_by)
        VALUES (@id, @actor, @endpoint, @key, @hash, @token, @status, now(), now(), 'test', now(), 'test')
        """;

    private async Task<int> ExecAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync();
    }

    private async Task<string> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private Task<int> InsertAsync(Guid actor, string key, string status = "pending") =>
        ExecAsync(Insert, ("id", Guid.NewGuid()), ("actor", actor), ("endpoint", "POST api/v1/customers"), ("key", key), ("hash", new byte[32]), ("token", Guid.NewGuid()), ("status", status));

    [Fact]
    public async Task The_columns_and_types_are_what_the_store_depends_on()
    {
        string columns = await ScalarAsync(
            "SELECT string_agg(column_name || ':' || data_type || ':' || is_nullable, ',' ORDER BY column_name) FROM information_schema.columns WHERE table_name = 'idempotency_keys'");

        Assert.Equal(
            "actor_id:uuid:NO,claim_token:uuid:NO,created_at:timestamp with time zone:NO,created_by:character varying:NO,endpoint:character varying:NO,"
            + "expires_at:timestamp with time zone:NO,id:uuid:NO,key:character varying:NO,request_hash:bytea:NO,response_body:bytea:YES,response_headers:jsonb:YES,"
            + "response_status:integer:YES,status:character varying:NO,updated_at:timestamp with time zone:NO,updated_by:character varying:NO",
            columns);
    }

    [Fact]
    public async Task A_scope_is_unique_per_actor_endpoint_and_key()
    {
        Guid actor = Guid.NewGuid();
        string key = Guid.NewGuid().ToString("N");
        await InsertAsync(actor, key);

        PostgresException duplicate = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(actor, key));
        Assert.Equal("23505", duplicate.SqlState);
        Assert.Equal(1, await InsertAsync(Guid.NewGuid(), key)); // another actor, same key: independent
    }

    [Fact]
    public async Task Status_is_pending_or_completed_only()
    {
        PostgresException bad = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(Guid.NewGuid(), Guid.NewGuid().ToString("N"), "failed"));

        Assert.Equal("23514", bad.SqlState);
    }

    [Fact]
    public async Task The_expiry_index_and_the_scope_index_exist()
    {
        string indexes = await ScalarAsync("SELECT string_agg(indexname, ',' ORDER BY indexname) FROM pg_indexes WHERE tablename = 'idempotency_keys'");

        Assert.Equal("ix_idempotency_keys_expires_at,pk_idempotency_keys,ux_idempotency_keys_scope", indexes);
    }
}
