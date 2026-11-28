using System.Net;
using System.Net.Http.Json;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Customers.Models;
using Fugue.Application.Features.Customers.Repos;
using Fugue.Domain.Customers;
using Fugue.Domain.Identity;
using Fugue.Infrastructure.Persistence;
using Fugue.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fugue.Tests.Webhooks;

/// <summary>The outbox is written in the same transaction as the change (ADR-011): EF path via the interceptor, Dapper path via <c>IOutbox</c>.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class OutboxCaptureTests(PostgresFixture db)
{
    private static string NewEmail() => $"o{Guid.NewGuid():N}@example.test";

    private static HttpRequestMessage Request(HttpMethod method, string path, string? correlation = null, string? saga = null, object? body = null, string? ifMatch = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (correlation is not null)
        {
            request.Headers.Add("X-Correlation-Id", correlation);
        }

        if (saga is not null)
        {
            request.Headers.Add("X-Saga-Id", saga);
        }

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

    private static async Task<(CustomerDto Customer, string ETag)> CreateAsync(HttpClient client, string? email = null, string? correlation = null, string? saga = null)
    {
        HttpResponseMessage response = await client.SendAsync(
            Request(HttpMethod.Post, "/api/v1/customers", correlation, saga, new { name = "Grace Hopper", email = email ?? NewEmail(), phone = "+14155550142" }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await response.Content.ReadFromJsonAsync<CustomerDto>())!, response.Headers.ETag!.Tag);
    }

    [Fact]
    public async Task A_dapper_insert_writes_one_created_event_with_masked_personal_data_in_the_same_transaction()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        string email = NewEmail();

        (CustomerDto customer, _) = await CreateAsync(admin, email, "cid-" + Guid.NewGuid().ToString("N"));

        OutboxRow row = Assert.Single(await OutboxProbe.ByAggregateAsync(db, customer.Id.ToString()));
        Assert.Equal(("customer.created.v1", "customer", "pending", 0), (row.EventType, row.AggregateType, row.Status, row.Attempts));
        Assert.Equal(customer.Id.ToString(), row.Payload.GetProperty("id").GetString());
        Assert.Equal("Active", row.Payload.GetProperty("status").GetString());
        Assert.Equal($"{email[0]}***{email[email.IndexOf('@', StringComparison.Ordinal)..]}", row.Payload.GetProperty("email").GetString()); // partially masked
        Assert.DoesNotContain(email, row.Payload.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("4155550142", row.Payload.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_update_writes_exactly_one_event_that_carries_the_correlation_and_saga_ids()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        (CustomerDto customer, string tag) = await CreateAsync(admin);
        string correlation = "corr-" + Guid.NewGuid().ToString("N");
        string saga = "saga-" + Guid.NewGuid().ToString("N");

        HttpResponseMessage updated = await admin.SendAsync(
            Request(HttpMethod.Put, $"/api/v1/customers/{customer.Id}", correlation, saga, new { name = "Grace Brewster Hopper", email = customer.Email, phone = (string?)null }, tag));

        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        List<OutboxRow> rows = await OutboxProbe.ByAggregateAsync(db, customer.Id.ToString());
        OutboxRow evt = Assert.Single(rows, r => r.EventType == "customer.updated.v1");
        Assert.Equal((correlation, saga), (evt.CorrelationId, evt.SagaId));
        Assert.Equal("Grace Brewster Hopper", evt.Payload.GetProperty("name").GetString());
        Assert.Equal(2, rows.Count); // created + updated, nothing else
    }

    [Fact]
    public async Task The_saga_id_defaults_to_the_aggregate_id()
    {
        (CustomerDto customer, _) = await CreateAsync(db.ClientAs(DevUsers.Admin));

        OutboxRow row = Assert.Single(await OutboxProbe.ByAggregateAsync(db, customer.Id.ToString()));

        Assert.Equal(customer.Id.ToString(), row.SagaId);
    }

    [Fact]
    public async Task Suspend_reactivate_and_delete_each_write_their_own_versioned_event()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        (CustomerDto customer, _) = await CreateAsync(admin);

        await admin.SendAsync(Request(HttpMethod.Post, $"/api/v1/customers/{customer.Id}/suspend"));
        HttpResponseMessage reactivated = await admin.SendAsync(Request(HttpMethod.Post, $"/api/v1/customers/{customer.Id}/reactivate"));
        HttpResponseMessage deleted = await admin.SendAsync(Request(HttpMethod.Delete, $"/api/v1/customers/{customer.Id}", ifMatch: reactivated.Headers.ETag!.Tag));

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(
            ["customer.created.v1", "customer.suspended.v1", "customer.reactivated.v1", "customer.deleted.v1"],
            (await OutboxProbe.ByAggregateAsync(db, customer.Id.ToString())).Select(r => r.EventType));
    }

    [Fact]
    public async Task A_no_op_update_and_a_failed_command_write_no_event()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);
        (CustomerDto customer, string tag) = await CreateAsync(admin);
        (CustomerDto other, _) = await CreateAsync(admin);

        // Same values: nothing changed, so no event.
        await admin.SendAsync(Request(HttpMethod.Put, $"/api/v1/customers/{customer.Id}", body: new { name = customer.Name, email = customer.Email, phone = customer.Phone }, ifMatch: tag));
        // Duplicate email: the save fails, so neither the change nor its event exists.
        HttpResponseMessage clash = await admin.SendAsync(Request(HttpMethod.Put, $"/api/v1/customers/{customer.Id}", body: new { name = "X", email = other.Email, phone = (string?)null }, ifMatch: tag));
        HttpResponseMessage duplicateCreate = await admin.PostAsJsonAsync("/api/v1/customers", new { name = "Dup", email = other.Email });

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateCreate.StatusCode);
        Assert.Equal(["customer.created.v1"], (await OutboxProbe.ByAggregateAsync(db, customer.Id.ToString())).Select(r => r.EventType));
    }

    [Fact]
    public async Task Rolling_back_a_unit_of_work_removes_the_EF_path_event_with_the_change()
    {
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        FugueDbContext context = scope.ServiceProvider.GetRequiredService<FugueDbContext>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        Customer customer = Customer.Create("Rolled back", NewEmail(), null);

        await unitOfWork.BeginAsync(default);
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        await unitOfWork.DisposeAsync(); // never committed

        Assert.Empty(await OutboxProbe.ByAggregateAsync(db, customer.Id.ToString()));
        Assert.False(await context.Customers.AsNoTracking().AnyAsync(c => c.Id == customer.Id));
    }

    [Fact]
    public async Task Rolling_back_a_unit_of_work_removes_the_Dapper_path_event_and_a_committed_one_survives()
    {
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        ICustomerRepository customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        Customer kept = Customer.Create("Kept", NewEmail(), null);
        Customer dropped = Customer.Create("Dropped", NewEmail(), null);

        Assert.True((await customers.AddAsync(kept, default)).IsSuccess);
        await unitOfWork.BeginAsync(default);
        Assert.True((await customers.AddAsync(dropped, default)).IsSuccess);
        await unitOfWork.DisposeAsync(); // never committed

        Assert.Single(await OutboxProbe.ByAggregateAsync(db, kept.Id.ToString()));
        Assert.Empty(await OutboxProbe.ByAggregateAsync(db, dropped.Id.ToString()));
    }

    [Fact]
    public async Task A_second_save_does_not_write_the_event_again()
    {
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        FugueDbContext context = scope.ServiceProvider.GetRequiredService<FugueDbContext>();
        Customer customer = Customer.Create("Twice", NewEmail(), null);
        context.Customers.Add(customer);

        await context.SaveChangesAsync();
        await context.SaveChangesAsync(); // nothing pending: must not write the event again

        Assert.Single(await OutboxProbe.ByAggregateAsync(db, customer.Id.ToString()));
    }

    [Fact]
    public async Task A_cancelled_save_does_not_leave_the_captured_event_behind_for_the_retry_to_duplicate()
    {
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        FugueDbContext context = scope.ServiceProvider.GetRequiredService<FugueDbContext>();
        Customer customer = Customer.Create("Cancelled once", NewEmail(), null);
        context.Customers.Add(customer);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.SaveChangesAsync(cancelled.Token));
        await context.SaveChangesAsync(); // the retry on the same context, with a fresh token

        Assert.Single(await OutboxProbe.ByAggregateAsync(db, customer.Id.ToString())); // one event, not two with different ids
    }
}
