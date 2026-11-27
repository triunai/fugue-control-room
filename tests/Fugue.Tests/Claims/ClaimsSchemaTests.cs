using Fugue.Domain.Identity;
using Fugue.Infrastructure.Persistence;
using Fugue.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Fugue.Tests.Claims;

/// <summary>Migration pin tests for the claims tables (migration-retrofit-qa step 4): the append-only timeline, the seeded catalog, the constraints and indexes.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ClaimsSchemaTests(PostgresFixture db)
{
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

    private async Task<(Guid ClaimId, Guid UserId)> InsertClaimAsync(long? reserve = null, string status = "Submitted")
    {
        (string _, Fugue.Domain.Ids.UserId user) = await db.NewUserAsync();
        Guid id = Guid.NewGuid();
        await ExecAsync(
            """
            INSERT INTO claims (id, number, market, currency, line, status, claimant_user_id, policy_number, incident_date, incident_description,
                                submitted_at, reserve_minor, created_at, created_by, updated_at, updated_by)
            VALUES (@id, @number, 'SG', 'SGD', 'Motor', @status, @user, 'POL-1', DATE '2030-01-01', 'dent', now(), @reserve, now(), 'test', now(), 'test')
            """,
            ("id", id), ("number", $"CLM-SG-2030-{id:N}"[..32]), ("status", status), ("user", user.Value), ("reserve", (object?)reserve ?? DBNull.Value));
        return (id, user.Value);
    }

    private async Task InsertTimelineAsync(Guid claimId, Guid userId, Guid id) =>
        await ExecAsync(
            """
            INSERT INTO claim_timeline (id, claim_id, occurred_at, from_status, to_status, actor_user_id, actor_display, note, created_at, created_by, updated_at, updated_by)
            VALUES (@id, @claim, now(), NULL, 'Submitted', @user, 'Someone', 'Claim submitted.', now(), 'test', now(), 'test')
            """,
            ("id", id), ("claim", claimId), ("user", userId));

    [Fact]
    public async Task An_update_to_the_timeline_is_rejected_by_the_append_only_trigger()
    {
        (Guid claim, Guid user) = await InsertClaimAsync();
        Guid entry = Guid.NewGuid();
        await InsertTimelineAsync(claim, user, entry);

        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => ExecAsync("UPDATE claim_timeline SET note = 'tampered' WHERE id = @id", ("id", entry)));

        Assert.Equal("23001", ex.SqlState);
        Assert.Contains("append-only", ex.MessageText);
        Assert.Equal("Claim submitted.", await ScalarAsync<string>("SELECT note FROM claim_timeline WHERE id = @id", ("id", entry)));
    }

    [Fact]
    public async Task A_delete_from_the_timeline_is_rejected_by_the_append_only_trigger()
    {
        (Guid claim, Guid user) = await InsertClaimAsync();
        Guid entry = Guid.NewGuid();
        await InsertTimelineAsync(claim, user, entry);

        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => ExecAsync("DELETE FROM claim_timeline WHERE id = @id", ("id", entry)));

        Assert.Equal("23001", ex.SqlState);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM claim_timeline WHERE id = @id", ("id", entry)));
    }

    [Fact]
    public async Task A_truncate_of_the_timeline_is_rejected_by_the_append_only_trigger()
    {
        (Guid claim, Guid user) = await InsertClaimAsync();
        await InsertTimelineAsync(claim, user, Guid.NewGuid());

        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => ExecAsync("TRUNCATE claim_timeline"));

        Assert.Equal("23001", ex.SqlState);
        Assert.True(await ScalarAsync<long>("SELECT count(*) FROM claim_timeline") > 0);
    }

    [Fact]
    public async Task The_claims_module_resource_and_all_eight_permissions_are_seeded()
    {
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM modules WHERE code = 'CLAIMS' AND is_active AND NOT is_deleted"));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM resources WHERE code = 'CLAIMS'"));
        string[] expected =
        [
            Permissions.ClaimsAll, Permissions.ClaimsExposure, Permissions.ClaimsHandle, Permissions.ClaimsRead,
            Permissions.ClaimsRespond, Permissions.ClaimsSettle, Permissions.ClaimsSubmit, Permissions.ClaimsWorkload,
        ];

        string codes = await ScalarAsync<string>("SELECT string_agg(code, ',' ORDER BY code) FROM permissions WHERE code LIKE 'CLAIMS\\_%'");

        Assert.Equal(string.Join(',', expected.Order(StringComparer.Ordinal)), codes);
        Assert.Equal(Permissions.All.Count, (int)await ScalarAsync<long>("SELECT count(*) FROM permissions"));
    }

    [Theory]
    [InlineData(RoleCodes.SelfService, "CLAIMS_READ,CLAIMS_RESPOND,CLAIMS_SUBMIT")]
    [InlineData(RoleCodes.Staff, "CLAIMS_ALL,CLAIMS_HANDLE,CLAIMS_READ,CLAIMS_WORKLOAD")]
    [InlineData(RoleCodes.Manager, "CLAIMS_ALL,CLAIMS_EXPOSURE,CLAIMS_HANDLE,CLAIMS_READ,CLAIMS_SETTLE,CLAIMS_WORKLOAD")]
    [InlineData(RoleCodes.Admin, "CLAIMS_ALL,CLAIMS_EXPOSURE,CLAIMS_HANDLE,CLAIMS_READ,CLAIMS_RESPOND,CLAIMS_SETTLE,CLAIMS_SUBMIT,CLAIMS_WORKLOAD")]
    public async Task Each_role_holds_exactly_the_claims_grants_of_decision_G(string role, string expected)
    {
        string granted = await ScalarAsync<string>(
            """
            SELECT string_agg(p.code, ',' ORDER BY p.code) FROM role_permissions rp
            JOIN permissions p ON p.id = rp.permission_id JOIN roles r ON r.id = rp.role_id
            WHERE r.code = @role AND p.code LIKE 'CLAIMS\_%'
            """,
            ("role", role));

        Assert.Equal(expected, granted);
    }

    [Fact]
    public async Task The_manager_role_exists_is_a_system_role_and_also_holds_the_staff_customer_permissions()
    {
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM roles WHERE code = 'MANAGER' AND is_system AND is_active"));
        Assert.Equal(
            await ScalarAsync<long>("SELECT count(*) FROM role_permissions rp JOIN roles r ON r.id = rp.role_id JOIN permissions p ON p.id = rp.permission_id WHERE r.code = 'STAFF' AND p.code LIKE 'CUSTOMERS\\_%'"),
            await ScalarAsync<long>("SELECT count(*) FROM role_permissions rp JOIN roles r ON r.id = rp.role_id JOIN permissions p ON p.id = rp.permission_id WHERE r.code = 'MANAGER' AND p.code LIKE 'CUSTOMERS\\_%'"));
    }

    [Fact]
    public async Task The_queue_claimant_and_exposure_indexes_exist_in_keyset_order()
    {
        string definitions = await ScalarAsync<string>("SELECT string_agg(indexdef, E'\\n' ORDER BY indexname) FROM pg_indexes WHERE tablename = 'claims'");

        Assert.Contains("(status, assigned_officer_id, submitted_at, id)", definitions);
        Assert.Contains("(claimant_user_id, submitted_at DESC, id DESC)", definitions);
        Assert.Contains("(status, market, line)", definitions);
        Assert.Contains("UNIQUE INDEX ux_claims_number", definitions);
    }

    [Fact]
    public async Task A_duplicate_claim_number_is_refused()
    {
        (Guid first, Guid user) = await InsertClaimAsync();
        string number = await ScalarAsync<string>("SELECT number FROM claims WHERE id = @id", ("id", first));

        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => ExecAsync(
            """
            INSERT INTO claims (id, number, market, currency, line, status, claimant_user_id, policy_number, incident_date, incident_description,
                                submitted_at, created_at, created_by, updated_at, updated_by)
            VALUES (@id, @number, 'SG', 'SGD', 'Motor', 'Submitted', @user, 'P', DATE '2030-01-01', 'x', now(), now(), 't', now(), 't')
            """,
            ("id", Guid.NewGuid()), ("number", number), ("user", user)));

        Assert.Equal("23505", ex.SqlState);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task A_non_positive_reserve_is_refused_by_the_check_constraint(long reserve)
    {
        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => InsertClaimAsync(reserve));

        Assert.Equal("23514", ex.SqlState);
    }

    [Fact]
    public async Task A_claim_can_have_only_one_open_info_request_but_many_answered_ones()
    {
        (Guid claim, Guid user) = await InsertClaimAsync();
        const string insert = """
            INSERT INTO claim_info_requests (id, claim_id, question, requested_by_user_id, requested_at, response, responded_at, created_at, created_by, updated_at, updated_by)
            VALUES (@id, @claim, 'Q?', @user, now(), @response, @respondedAt, now(), 't', now(), 't')
            """;
        (string, object)[] Args(object? response, object? respondedAt) =>
            [("id", Guid.NewGuid()), ("claim", claim), ("user", user), ("response", response ?? DBNull.Value), ("respondedAt", respondedAt ?? DBNull.Value)];

        await ExecAsync(insert, Args("A", DateTimeOffset.UtcNow));
        await ExecAsync(insert, Args("B", DateTimeOffset.UtcNow));
        await ExecAsync(insert, Args(null, null));

        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => ExecAsync(insert, Args(null, null)));

        Assert.Equal("23505", ex.SqlState);
        Assert.Contains("ux_claim_info_requests_open", ex.ConstraintName ?? ex.MessageText);
    }

    [Fact]
    public async Task The_number_sequence_hands_out_distinct_increasing_values_under_concurrency()
    {
        long[] values = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => ScalarAsync<long>("SELECT nextval('claims_number_seq')")));

        Assert.Equal(20, values.Distinct().Count());
    }
}

/// <summary>The rollback written in <c>Down()</c> actually runs, and the migration applies again afterwards (retrofit QA step 3).</summary>
public sealed class ClaimsMigrationRollbackTests
{
    [Fact]
    public async Task Down_removes_the_tables_the_function_the_sequence_and_the_seed_and_up_applies_again()
    {
        await using PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await container.StartAsync();
        var builder = new DbContextOptionsBuilder<FugueDbContext>();
        PersistenceOptions.Configure(builder, container.GetConnectionString());
        await using var context = new FugueDbContext(builder.Options);
        IMigrator migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync();
        Assert.True(await ExistsAsync(container, "SELECT to_regclass('claim_timeline') IS NOT NULL AND to_regclass('claims_number_seq') IS NOT NULL"));

        await migrator.MigrateAsync("AddIdempotencyKeys");
        Assert.False(await ExistsAsync(container, "SELECT to_regclass('claims') IS NOT NULL OR to_regclass('claim_timeline') IS NOT NULL OR to_regclass('claim_info_requests') IS NOT NULL"));
        Assert.False(await ExistsAsync(container, "SELECT to_regclass('claims_number_seq') IS NOT NULL"));
        Assert.False(await ExistsAsync(container, "SELECT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'claim_timeline_reject_change')"));
        Assert.False(await ExistsAsync(container, "SELECT EXISTS (SELECT 1 FROM permissions WHERE code LIKE 'CLAIMS\\_%') OR EXISTS (SELECT 1 FROM roles WHERE code = 'MANAGER') OR EXISTS (SELECT 1 FROM modules WHERE code = 'CLAIMS')"));
        Assert.True(await ExistsAsync(container, "SELECT EXISTS (SELECT 1 FROM roles WHERE code = 'STAFF')")); // the earlier catalog is untouched

        await migrator.MigrateAsync();
        Assert.True(await ExistsAsync(container, "SELECT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'trg_claim_timeline_no_truncate')"));
        Assert.True(await ExistsAsync(container, "SELECT EXISTS (SELECT 1 FROM roles WHERE code = 'MANAGER')"));
    }

    private static async Task<bool> ExistsAsync(PostgreSqlContainer container, string sql)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
