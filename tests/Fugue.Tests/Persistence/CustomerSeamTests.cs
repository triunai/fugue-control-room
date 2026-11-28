using FluentResults;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Customers.Commands;
using Fugue.Application.Features.Customers.Helpers;
using Fugue.Application.Features.Customers.Models;
using Fugue.Application.Features.Customers.Repos;
using Fugue.Domain.Customers;
using Fugue.Domain.Results;
using Fugue.Infrastructure.Persistence.Seeding;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fugue.Tests.Persistence;

/// <summary>
/// The same failure logic as the HTTP tests, asserted on the Result so it is verified independently of
/// <c>MapStatus</c> (which is the owner's), plus the unit-of-work and seeder seams.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class CustomerSeamTests(PostgresFixture db)
{
    private AsyncServiceScope NewScope() => db.Factory.Services.CreateAsyncScope();

    [Fact]
    public async Task Duplicate_email_maps_unique_violation_to_conflict()
    {
        await using AsyncServiceScope scope = NewScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        string email = Email();
        Assert.True((await sender.Send(new CreateCustomerCommand("A", email, null))).IsSuccess);

        Result<CustomerDto> duplicate = await sender.Send(new CreateCustomerCommand("B", email.ToUpperInvariant(), null));

        AssertError(duplicate, "customer.email_taken", ErrorType.Conflict);
    }

    [Fact]
    public async Task Stale_version_is_precondition_failed_and_missing_version_is_precondition_required()
    {
        await using AsyncServiceScope scope = NewScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        CustomerDto customer = (await sender.Send(new CreateCustomerCommand("A", Email(), null))).Value;

        Result<CustomerDto> ok = await sender.Send(new UpdateCustomerCommand(customer.Id, "B", customer.Email, null, customer.Version));
        Result<CustomerDto> stale = await sender.Send(new UpdateCustomerCommand(customer.Id, "C", customer.Email, null, customer.Version));
        Result<CustomerDto> missing = await sender.Send(new UpdateCustomerCommand(customer.Id, "C", customer.Email, null, null));
        Result missingDelete = await sender.Send(new DeleteCustomerCommand(customer.Id, null));

        Assert.True(ok.IsSuccess);
        Assert.NotEqual(customer.Version, ok.Value.Version);
        AssertError(stale, "customer.version_mismatch", ErrorType.PreconditionFailed);
        AssertError(missing, "customer.if_match_required", ErrorType.PreconditionRequired);
        AssertError(missingDelete, "customer.if_match_required", ErrorType.PreconditionRequired);
    }

    [Fact]
    public async Task Invalid_transitions_are_conflicts_and_unknown_ids_are_not_found()
    {
        await using AsyncServiceScope scope = NewScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        CustomerDto customer = (await sender.Send(new CreateCustomerCommand("A", Email(), null))).Value;

        Result<CustomerDto> reactivateActive = await sender.Send(new ReactivateCustomerCommand(customer.Id, null));
        Assert.True((await sender.Send(new SuspendCustomerCommand(customer.Id, null))).IsSuccess);
        Result<CustomerDto> suspendAgain = await sender.Send(new SuspendCustomerCommand(customer.Id, null));
        Result<CustomerDto> unknown = await sender.Send(new SuspendCustomerCommand(Fugue.Domain.Ids.CustomerId.New(), null));

        AssertError(reactivateActive, "customer.invalid_transition", ErrorType.Conflict);
        AssertError(suspendAgain, "customer.invalid_transition", ErrorType.Conflict);
        AssertError(unknown, "customer.not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task Unit_of_work_makes_dapper_and_ef_writes_atomic()
    {
        string existingEmail = Email();
        string insertedEmail = Email();
        CustomerDto existing;
        await using (AsyncServiceScope seed = NewScope())
        {
            existing = (await seed.ServiceProvider.GetRequiredService<ISender>().Send(new CreateCustomerCommand("Before", existingEmail, null))).Value;
        }

        // Rolled back: a Dapper insert and an EF update in one unit of work, never committed.
        await using (AsyncServiceScope scope = NewScope())
        {
            IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            ICustomerRepository repository = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
            await using (unitOfWork)
            {
                await unitOfWork.BeginAsync(default);
                Assert.True((await repository.AddAsync(Customer.Create("Inserted", insertedEmail, null), default)).IsSuccess);
                Customer tracked = (await repository.FindForUpdateAsync(existing.Id, new OwnerScope(true, null), default))!;
                tracked.Update("After", existingEmail, null);
                Assert.True((await repository.SaveAsync(tracked, existing.Version, default)).IsSuccess);
            }
        }

        Assert.Equal(0L, await Count("SELECT count(*) FROM customers WHERE email = @v", insertedEmail));
        Assert.Equal(1L, await Count("SELECT count(*) FROM customers WHERE email = @v AND name = 'Before'", existingEmail));

        // Committed: both land.
        await using (AsyncServiceScope scope = NewScope())
        {
            IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            ICustomerRepository repository = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
            await using (unitOfWork)
            {
                await unitOfWork.BeginAsync(default);
                await repository.AddAsync(Customer.Create("Inserted", insertedEmail, null), default);
                Customer tracked = (await repository.FindForUpdateAsync(existing.Id, new OwnerScope(true, null), default))!;
                tracked.Update("After", existingEmail, null);
                await repository.SaveAsync(tracked, existing.Version, default);
                await unitOfWork.CommitAsync(default);
            }
        }

        Assert.Equal(1L, await Count("SELECT count(*) FROM customers WHERE email = @v", insertedEmail));
        Assert.Equal(1L, await Count("SELECT count(*) FROM customers WHERE email = @v AND name = 'After'", existingEmail));
    }

    [Fact]
    public async Task Seeder_is_idempotent_upsert()
    {
        for (int run = 0; run < 2; run++)
        {
            await using AsyncServiceScope scope = NewScope();
            Assert.Equal(25, await scope.ServiceProvider.GetRequiredService<ICustomerSeeder>().SeedAsync(default));
        }

        Assert.Equal(25L, await Count("SELECT count(*) FROM customers WHERE email LIKE @v", "seed.customer%@example.test"));
    }

    private async Task<long> Count(string sql, string value)
    {
        await using NpgsqlConnection connection = new(db.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("v", value);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static void AssertError(ResultBase result, string code, ErrorType type)
    {
        Assert.True(result.IsFailed);
        FugueError error = Assert.IsType<FugueError>(result.Errors[0]);
        Assert.Equal(code, error.Code);
        Assert.Equal(type, error.Type);
    }

    private static string Email() => $"{Guid.NewGuid():N}@example.test";
}
