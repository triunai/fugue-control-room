using System.Net;
using System.Net.Http.Json;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Customers;
using Fugue.Domain.Customers;
using Fugue.Domain.Identity;
using Fugue.Infrastructure.Persistence;
using Fugue.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fugue.Tests.Audit;

/// <summary>
/// audit_log capture (ADR-010) end to end: the Dapper insert path, the EF interceptor path, failure rows, masking and the
/// same-transaction guarantee, all against real Postgres through the real API host.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AuditCaptureTests(PostgresFixture db)
{
    private static string NewEmail() => $"n{Guid.NewGuid():N}@example.test";

    private static string NewCorrelation() => "audit-" + Guid.NewGuid().ToString("N");

    private static HttpRequestMessage Request(HttpMethod method, string path, string correlation, object? body = null, string? ifMatch = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Correlation-Id", correlation);
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static async Task<(CustomerDto Customer, string ETag)> CreateAsync(HttpClient client, string? email = null, string? correlation = null)
    {
        HttpResponseMessage response = await client.SendAsync(
            Request(HttpMethod.Post, "/api/v1/customers", correlation ?? NewCorrelation(), new { name = "Ada Lovelace", email = email ?? NewEmail(), phone = (string?)null }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await response.Content.ReadFromJsonAsync<CustomerDto>())!, response.Headers.ETag!.Tag);
    }

    [Fact]
    public async Task A_customer_insert_is_a_Dapper_write_and_still_emits_one_rich_audit_row()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        string correlation = NewCorrelation();
        string email = NewEmail();

        (CustomerDto customer, _) = await CreateAsync(admin, email, correlation);

        AuditRow row = Assert.Single(await AuditProbe.ByCorrelationAsync(db, correlation));
        Assert.Equal(("customers.create", "Customer", customer.Id.ToString(), "success", null), (row.Action, row.EntityType, row.EntityId, row.Outcome, row.ErrorCode));
        Assert.Equal(("user", "Dev Admin", "CUSTOMERS_INSERT"), (row.ActorType, row.ActorDisplay, row.PermissionUsed));
        Assert.Equal(await AuditProbe.UserIdAsync(db, DevUsers.Admin), row.ActorUserId);
        Assert.Equal(("POST", "api/v1/customers", correlation), (row.HttpMethod, row.RouteTemplate, row.CorrelationId));
        Assert.Matches("^[0-9a-f]{32}$", row.TraceId);
        Assert.Equal("CreateCustomerCommand", row.Metadata!.Value.GetProperty("command").GetString());
        Assert.Equal("Ada Lovelace", row.Change("name").GetProperty("new").GetString());
        Assert.False(row.Change("name").TryGetProperty("old", out _)); // an insert has no old value
        Assert.Equal("Active", row.Change("status").GetProperty("new").GetString());
        Assert.DoesNotContain("updatedAt", row.ChangedFields); // stamp columns and the xmin token are noise
        Assert.DoesNotContain("rowVersion", row.ChangedFields);
    }

    [Fact]
    public async Task An_update_records_only_the_fields_that_changed_with_old_and_new()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        (CustomerDto customer, string etag) = await CreateAsync(admin);
        string correlation = NewCorrelation();

        HttpResponseMessage response = await admin.SendAsync(
            Request(HttpMethod.Put, $"/api/v1/customers/{customer.Id}", correlation, new { name = "Ada King", email = customer.Email, phone = (string?)null }, etag));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AuditRow row = Assert.Single(await AuditProbe.ByCorrelationAsync(db, correlation));
        Assert.Equal(("customers.update", "Customer", customer.Id.ToString(), "success"), (row.Action, row.EntityType, row.EntityId, row.Outcome));
        Assert.Equal(["name"], row.ChangedFields); // email and phone were resubmitted unchanged
        Assert.Equal("Ada Lovelace", row.Change("name").GetProperty("old").GetString());
        Assert.Equal("Ada King", row.Change("name").GetProperty("new").GetString());
        Assert.Equal("CUSTOMERS_EDIT", row.PermissionUsed);
    }

    [Fact]
    public async Task A_delete_records_the_old_values()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        (CustomerDto customer, string etag) = await CreateAsync(admin);
        string correlation = NewCorrelation();

        HttpResponseMessage response = await admin.SendAsync(Request(HttpMethod.Delete, $"/api/v1/customers/{customer.Id}", correlation, ifMatch: etag));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AuditRow row = Assert.Single(await AuditProbe.ByCorrelationAsync(db, correlation));
        Assert.Equal("customers.delete", row.Action);
        Assert.Equal("Ada Lovelace", row.Change("name").GetProperty("old").GetString());
        Assert.False(row.Change("name").TryGetProperty("new", out _));
    }

    [Fact]
    public async Task A_409_business_failure_writes_exactly_one_failure_row_with_the_error_code()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        string email = NewEmail();
        await CreateAsync(admin, email);
        string correlation = NewCorrelation();

        HttpResponseMessage response = await admin.SendAsync(
            Request(HttpMethod.Post, "/api/v1/customers", correlation, new { name = "Dup", email, phone = (string?)null }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        AuditRow row = Assert.Single(await AuditProbe.ByCorrelationAsync(db, correlation)); // one row: the failed insert left no success row
        Assert.Equal(("customers.create", "failure", "customer.email_taken"), (row.Action, row.Outcome, row.ErrorCode));
        Assert.Null(row.Changes);
        Assert.Equal("CUSTOMERS_INSERT", row.PermissionUsed);
        Assert.Equal("Dev Admin", row.ActorDisplay);
    }

    [Fact]
    public async Task Validation_and_precondition_failures_are_audited_with_the_entity_id()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        (CustomerDto customer, string etag) = await CreateAsync(admin);

        string invalid = NewCorrelation();
        HttpResponseMessage bad = await admin.SendAsync(
            Request(HttpMethod.Put, $"/api/v1/customers/{customer.Id}", invalid, new { name = "", email = customer.Email, phone = (string?)null }, etag));
        string stale = NewCorrelation();
        HttpResponseMessage precondition = await admin.SendAsync(
            Request(HttpMethod.Put, $"/api/v1/customers/{customer.Id}", stale, new { name = "X Y", email = customer.Email, phone = (string?)null }, "\"1\""));

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, precondition.StatusCode);
        AuditRow validation = Assert.Single(await AuditProbe.ByCorrelationAsync(db, invalid));
        Assert.Equal(("failure", customer.Id.ToString()), (validation.Outcome, validation.EntityId));
        Assert.NotNull(validation.ErrorCode);
        AuditRow version = Assert.Single(await AuditProbe.ByCorrelationAsync(db, stale));
        Assert.Equal(("failure", "customer.version_mismatch", "customers.update"), (version.Outcome, version.ErrorCode, version.Action));
    }

    [Fact]
    public async Task A_not_found_and_a_failed_login_do_not_pollute_the_wrong_log()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        string missing = NewCorrelation();
        HttpResponseMessage response = await admin.SendAsync(
            Request(HttpMethod.Put, $"/api/v1/customers/{Guid.NewGuid()}", missing, new { name = "Ghost", email = NewEmail(), phone = (string?)null }, "\"1\""));
        string login = NewCorrelation();
        await db.Anonymous().SendAsync(Request(HttpMethod.Post, "/api/v1/auth/token", login, new { email = DevUsers.Staff, password = "wrong-wrong-wrong" }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("customer.not_found", Assert.Single(await AuditProbe.ByCorrelationAsync(db, missing)).ErrorCode);
        Assert.Empty(await AuditProbe.ByCorrelationAsync(db, login)); // authentication events belong to access_audit
    }

    [Fact]
    public async Task Secrets_never_reach_the_table_and_personal_data_is_partially_masked()
    {
        (string email, Fugue.Domain.Ids.UserId id) = await db.NewUserAsync([RoleCodes.SelfService]);

        AuditRow row = Assert.Single(await AuditProbe.ByEntityAsync(db, "User", id.ToString()));

        Assert.Equal(("users.create", "system", "system", null), (row.Action, row.ActorType, row.ActorDisplay, row.ActorUserId));
        Assert.Equal("***", row.Change("passwordHash").GetProperty("new").GetString());
        Assert.Equal($"{email[0]}***@example.test", row.Change("email").GetProperty("new").GetString());
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using var hash = new NpgsqlCommand("SELECT password_hash FROM users WHERE id = @id", connection);
        hash.Parameters.AddWithValue("id", id.Value);
        string storedHash = (string)(await hash.ExecuteScalarAsync())!;
        Assert.Equal(0L, await AuditProbe.CountContainingAsync(db, storedHash));
        Assert.Equal(0L, await AuditProbe.CountContainingAsync(db, email));
    }

    [Fact]
    public async Task A_save_that_fails_leaves_no_audit_row_because_the_audit_rows_ride_in_the_same_save()
    {
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        FugueDbContext context = scope.ServiceProvider.GetRequiredService<FugueDbContext>();
        string email = NewEmail();
        Customer first = Customer.Create("Twin A", email, null);
        Customer second = Customer.Create("Twin B", email, null); // same canonical email: the second INSERT violates the unique index
        context.Customers.AddRange(first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Empty(await AuditProbe.ByEntityAsync(db, "Customer", first.Id.ToString()));
        Assert.Empty(await AuditProbe.ByEntityAsync(db, "Customer", second.Id.ToString()));
    }

    [Fact]
    public async Task Rolling_back_a_unit_of_work_removes_the_EF_audit_row_with_the_change()
    {
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        FugueDbContext context = scope.ServiceProvider.GetRequiredService<FugueDbContext>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        Customer customer = Customer.Create("Rolled back", NewEmail(), null);

        await unitOfWork.BeginAsync(default);
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        await unitOfWork.DisposeAsync(); // never committed

        Assert.Empty(await AuditProbe.ByEntityAsync(db, "Customer", customer.Id.ToString()));
        Assert.False(await context.Customers.AsNoTracking().AnyAsync(c => c.Id == customer.Id));
    }

    [Fact]
    public async Task A_Dapper_insert_outside_http_emits_a_system_row_and_rolls_back_with_its_unit_of_work()
    {
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        ICustomerRepository customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        Customer committed = Customer.Create("Dapper kept", NewEmail(), null);
        Customer discarded = Customer.Create("Dapper dropped", NewEmail(), null);

        Assert.True((await customers.AddAsync(committed, default)).IsSuccess);
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(default);
        Assert.True((await customers.AddAsync(discarded, default)).IsSuccess);
        await unitOfWork.DisposeAsync(); // never committed

        AuditRow kept = Assert.Single(await AuditProbe.ByEntityAsync(db, "Customer", committed.Id.ToString()));
        Assert.Equal(("customers.create", "system", "success"), (kept.Action, kept.ActorType, kept.Outcome));
        Assert.Equal("Dapper kept", kept.Change("name").GetProperty("new").GetString());
        Assert.Empty(await AuditProbe.ByEntityAsync(db, "Customer", discarded.Id.ToString()));
    }

    [Fact]
    public async Task The_seeder_leaves_one_summary_row_per_run_not_one_per_customer()
    {
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        long before = await CountAsync("action = 'customers.seed'");

        await scope.ServiceProvider.GetRequiredService<Fugue.Infrastructure.Persistence.Seeding.ICustomerSeeder>().SeedAsync(default);

        Assert.Equal(before + 1, await CountAsync("action = 'customers.seed'"));
    }

    private async Task<long> CountAsync(string where)
    {
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM audit_log WHERE {where}", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
