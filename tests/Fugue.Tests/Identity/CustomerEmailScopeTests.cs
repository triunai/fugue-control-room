using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Fugue.Infrastructure.Persistence;
using Fugue.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Fugue.Tests.Identity;

/// <summary>
/// D-034: customer email is unique per owner (and among unowned rows), so the create path is not a cross-owner existence oracle.
/// "Restricted staff" (STAFF with an explicit CUSTOMERS_ALL deny) is the self-service creator: it owns what it creates.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class CustomerEmailScopeTests(PostgresFixture db)
{
    private static string Email() => $"{Guid.NewGuid():N}@example.test";

    private async Task<HttpClient> Restricted()
    {
        (string email, _) = await db.NewUserAsync([RoleCodes.Staff], [(Permissions.CustomersAll, false, null)]);
        return db.ClientAs(email);
    }

    [Fact]
    public async Task Two_owners_can_both_create_the_same_email()
    {
        HttpClient a = await Restricted();
        HttpClient b = await Restricted();
        string email = Email();

        HttpResponseMessage first = await a.PostAsJsonAsync("/api/v1/customers", new { name = "A", email });
        HttpResponseMessage second = await b.PostAsJsonAsync("/api/v1/customers", new { name = "B", email = email.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode); // no cross-owner oracle: B cannot tell A holds this email
    }

    [Fact]
    public async Task The_same_owner_creating_the_email_twice_is_409()
    {
        HttpClient a = await Restricted();
        string email = Email();
        Assert.Equal(HttpStatusCode.Created, (await a.PostAsJsonAsync("/api/v1/customers", new { name = "A", email })).StatusCode);

        HttpResponseMessage again = await a.PostAsJsonAsync("/api/v1/customers", new { name = "A2", email });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await again.Content.ReadAsStringAsync());
        Assert.Equal("customer.email_taken", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unowned_staff_created_rows_are_unique_among_themselves()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        string email = Email();
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/v1/customers", new { name = "U1", email })).StatusCode);

        HttpResponseMessage again = await admin.PostAsJsonAsync("/api/v1/customers", new { name = "U2", email });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task An_unowned_row_does_not_block_an_owner_and_a_conflict_only_fires_inside_the_target_owners_scope()
    {
        using HttpClient admin = db.ClientAs(DevUsers.Admin);
        (string ownerEmail, UserId owner) = await db.NewUserAsync([RoleCodes.Staff], [(Permissions.CustomersAll, false, null)]);
        HttpClient ownerClient = db.ClientAs(ownerEmail);
        string email = Email();
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/v1/customers", new { name = "Unowned", email })).StatusCode);

        // The owner is unaffected by the unowned row; admin (sees everything) assigning the same email to that owner collides only with the owner's own row.
        Assert.Equal(HttpStatusCode.Created, (await ownerClient.PostAsJsonAsync("/api/v1/customers", new { name = "Mine", email })).StatusCode);
        HttpResponseMessage forOwner = await admin.PostAsJsonAsync("/api/v1/customers", new { name = "Dup", email, userId = owner.Value });
        Assert.Equal(HttpStatusCode.Conflict, forOwner.StatusCode);
    }
}

/// <summary>Pin and rollback test for the <c>CustomerEmailPerOwner</c> migration (retrofit QA steps 3 and 4), on a throwaway container.</summary>
public sealed class CustomerEmailIndexMigrationTests
{
    [Fact]
    public async Task Up_swaps_the_global_index_for_the_per_owner_pair_and_down_restores_it()
    {
        await using PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await container.StartAsync();
        var builder = new DbContextOptionsBuilder<FugueDbContext>();
        PersistenceOptions.Configure(builder, container.GetConnectionString());
        await using var context = new FugueDbContext(builder.Options);
        IMigrator migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync();
        Assert.Equal(["ux_customers_owner_email", "ux_customers_unowned_email"], await Indexes(container));
        Assert.Contains("WHERE (user_id IS NULL)", await Scalar(container, "SELECT indexdef FROM pg_indexes WHERE indexname = 'ux_customers_unowned_email'"));

        await migrator.MigrateAsync("AddAuditLog");
        Assert.Equal(["ux_customers_email"], await Indexes(container));

        await migrator.MigrateAsync();
        Assert.Equal(["ux_customers_owner_email", "ux_customers_unowned_email"], await Indexes(container));
    }

    private static async Task<string[]> Indexes(PostgreSqlContainer container)
    {
        string list = await Scalar(container, "SELECT string_agg(indexname, ',' ORDER BY indexname) FROM pg_indexes WHERE tablename = 'customers' AND indexname LIKE 'ux_customers%'");
        return list.Split(',');
    }

    private static async Task<string> Scalar(PostgreSqlContainer container, string sql)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
