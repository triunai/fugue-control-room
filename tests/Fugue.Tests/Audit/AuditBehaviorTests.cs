using FluentResults;
using Fugue.Api.Auditing;
using Fugue.Application.Abstractions;
using Fugue.Application.Common;
using Fugue.Application.Common.Behaviors;
using Fugue.Application.Features.Auth.Commands;
using Fugue.Application.Features.Customers;
using Fugue.Application.Features.Customers.Commands;
using Fugue.Application.Features.Customers.Queries;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Results;
using Fugue.Infrastructure.Audit;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fugue.Tests.Audit;

/// <summary>The failure-audit behavior in isolation: one failure row, no success row, no effect on the result, queries and exempt commands untouched.</summary>
public sealed class AuditBehaviorTests
{
    private sealed class RecordingTrail : IAuditTrail
    {
        public List<AuditFailure> Failures { get; } = [];

        public List<AuditEntry> Entries { get; } = [];

        public bool Throw { get; set; }

        public string? ActionSeenInScope { get; set; }

        public IAuditScope? Scope { get; set; }

        public Task RecordAsync(AuditEntry entry, CancellationToken ct)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task RecordFailureAsync(AuditFailure failure, CancellationToken ct)
        {
            ActionSeenInScope = Scope?.Action;
            if (Throw)
            {
                throw new InvalidOperationException("audit store down");
            }

            Failures.Add(failure);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedUser(Guid? id) : ICurrentUser
    {
        public Guid? UserId => id;

        public Guid? TenantId => null;

        public bool IsAuthenticated => id is not null;

        public bool HasPermission(string code) => true;
    }

    private sealed class FixedDirectory : IActorDirectory
    {
        public Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken ct) => Task.FromResult<string?>("Ada");
    }

    private static (IServiceProvider Services, RecordingTrail Trail) Build(Guid? userId = null)
    {
        var trail = new RecordingTrail();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IAuditScope, AuditScope>();
        services.AddSingleton<IAuditTrail>(trail);
        services.AddSingleton<ICurrentUser>(new FixedUser(userId));
        services.AddSingleton<IActorDirectory>(new FixedDirectory());
        ServiceProvider provider = services.BuildServiceProvider();
        trail.Scope = provider.GetRequiredService<IAuditScope>();
        return (provider, trail);
    }

    private static MessageHandlerDelegate<TMessage, TResponse> Returns<TMessage, TResponse>(TResponse response) where TMessage : IMessage =>
        (_, _) => new ValueTask<TResponse>(response);

    private static readonly string[] OneMessage = ["m"];

    private static readonly UpdateCustomerCommand Update = new(Fugue.Domain.Ids.CustomerId.New(), "n", "e@example.test", null, 1);

    [Fact]
    public async Task A_failing_command_writes_one_failure_row_named_after_the_command_with_its_entity_id_and_error_code()
    {
        (IServiceProvider services, RecordingTrail trail) = Build(Guid.NewGuid());
        var behavior = new AuditBehavior<UpdateCustomerCommand, Result<CustomerDto>>(services);

        Result<CustomerDto> result = await behavior.Handle(
            Update, Returns<UpdateCustomerCommand, Result<CustomerDto>>(Result.Fail<CustomerDto>(CustomerErrors.VersionMismatch())), default);

        Assert.True(result.IsFailed); // the result is returned untouched
        AuditFailure failure = Assert.Single(trail.Failures);
        Assert.Equal(("customers.update", "Customer", Update.Id.ToString(), "customer.version_mismatch"), (failure.Action, failure.EntityType, failure.EntityId, failure.ErrorCode));
        Assert.Equal("UpdateCustomerCommand", failure.Metadata!["command"]);
        Assert.Equal(("PreconditionFailed", null), (failure.ErrorType, failure.ErrorMessage)); // the handler's error type; its message text is never stored
        Assert.True(failure.DurationMs >= 0);
        Assert.Equal("customers.update", trail.ActionSeenInScope); // the ambient scope was open while the row was written
        Assert.Null(services.GetRequiredService<IAuditScope>().Action); // and is closed afterwards
    }

    [Fact]
    public async Task A_successful_command_writes_nothing_because_the_interceptor_records_successes()
    {
        (IServiceProvider services, RecordingTrail trail) = Build(Guid.NewGuid());
        var behavior = new AuditBehavior<UpdateCustomerCommand, Result<CustomerDto>>(services);

        await behavior.Handle(Update, Returns<UpdateCustomerCommand, Result<CustomerDto>>(Result.Ok(default(CustomerDto)!)), default);

        Assert.Empty(trail.Failures);
        Assert.Empty(trail.Entries);
    }

    [Fact]
    public async Task An_unhandled_exception_is_audited_as_unhandled_and_rethrown()
    {
        (IServiceProvider services, RecordingTrail trail) = Build();
        var behavior = new AuditBehavior<UpdateCustomerCommand, Result<CustomerDto>>(services);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(Update, (_, _) => throw new InvalidOperationException("boom"), default));

        AuditFailure failure = Assert.Single(trail.Failures);
        Assert.Equal(("unhandled", "Unhandled", "System.InvalidOperationException"), (failure.ErrorCode, failure.ErrorType, failure.ErrorMessage));
        Assert.DoesNotContain("boom", failure.ErrorMessage); // the exception's own text is never stored
    }

    [Fact]
    public async Task A_business_error_message_is_never_stored_even_when_it_interpolates_a_request_value()
    {
        (IServiceProvider services, RecordingTrail trail) = Build();
        var behavior = new AuditBehavior<UpdateCustomerCommand, Result<CustomerDto>>(services);
        const string Sentinel = "ali.sentinel@example.com";

        await behavior.Handle(
            Update, Returns<UpdateCustomerCommand, Result<CustomerDto>>(Result.Fail<CustomerDto>(Fugue.Application.Features.Groups.GroupErrors.NotFound(Sentinel))), default);
        await behavior.Handle(Update, Returns<UpdateCustomerCommand, Result<CustomerDto>>(Result.Fail<CustomerDto>("plain failure")), default);

        Assert.All(trail.Failures, f => Assert.Null(f.ErrorMessage));
        Assert.Equal("group.not_found", trail.Failures[0].ErrorCode);
        Assert.Equal(("unexpected", "Failure"), (trail.Failures[1].ErrorCode, trail.Failures[1].ErrorType));
    }

    [Fact]
    public async Task A_validation_failure_stores_the_sorted_field_names_only_and_never_the_messages()
    {
        (IServiceProvider services, RecordingTrail trail) = Build();
        var behavior = new AuditBehavior<UpdateCustomerCommand, Result<CustomerDto>>(services);
        var fields = new Dictionary<string, string[]> { ["Name"] = ["bad value ali.sentinel@example.com"], ["Email"] = ["x"] };

        await behavior.Handle(
            Update, Returns<UpdateCustomerCommand, Result<CustomerDto>>(Result.Fail<CustomerDto>(FugueErrors.Validation(fields))), default);

        Assert.Equal("invalid: Email,Name", Assert.Single(trail.Failures).ErrorMessage);
    }

    [Fact]
    public async Task A_validation_summary_is_capped_at_2000_characters()
    {
        (IServiceProvider services, RecordingTrail trail) = Build();
        var behavior = new AuditBehavior<UpdateCustomerCommand, Result<CustomerDto>>(services);
        Dictionary<string, string[]> fields = Enumerable.Range(0, 400).ToDictionary(i => "Field" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), _ => OneMessage);

        await behavior.Handle(
            Update, Returns<UpdateCustomerCommand, Result<CustomerDto>>(Result.Fail<CustomerDto>(FugueErrors.Validation(fields))), default);

        Assert.Equal(2000, Assert.Single(trail.Failures).ErrorMessage!.Length);
    }

    [Fact]
    public async Task A_failing_audit_write_never_changes_the_commands_result()
    {
        (IServiceProvider services, RecordingTrail trail) = Build();
        trail.Throw = true;
        var behavior = new AuditBehavior<UpdateCustomerCommand, Result<CustomerDto>>(services);

        Result<CustomerDto> result = await behavior.Handle(
            Update, Returns<UpdateCustomerCommand, Result<CustomerDto>>(Result.Fail<CustomerDto>(CustomerErrors.VersionMismatch())), default);

        Assert.Equal("customer.version_mismatch", ((FugueError)result.Errors[0]).Code);
    }

    [Fact]
    public async Task Queries_and_no_audit_commands_pass_through_without_touching_any_audit_service()
    {
        var untouchable = new ServiceCollection().BuildServiceProvider(); // resolving anything would throw
        var query = new GetCustomerByIdQuery(Fugue.Domain.Ids.CustomerId.New());
        var login = new LoginCommand("a@example.test", "pw", new ClientInfo(null, null));

        Result<CustomerDto> fromQuery = await new AuditBehavior<GetCustomerByIdQuery, Result<CustomerDto>>(untouchable).Handle(
            query, Returns<GetCustomerByIdQuery, Result<CustomerDto>>(Result.Fail<CustomerDto>(CustomerErrors.NotFound(query.Id))), default);
        Result<Fugue.Application.Features.Auth.TokenDto> fromLogin = await new AuditBehavior<LoginCommand, Result<Fugue.Application.Features.Auth.TokenDto>>(untouchable).Handle(
            login, Returns<LoginCommand, Result<Fugue.Application.Features.Auth.TokenDto>>(Result.Fail<Fugue.Application.Features.Auth.TokenDto>(Fugue.Application.Features.Auth.AuthErrors.InvalidCredentials())), default);

        Assert.True(fromQuery.IsFailed);
        Assert.True(fromLogin.IsFailed);
    }
}

public sealed class AuditOptionsValidatorTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "test";

        public string ContentRootPath { get; set; } = ".";

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Theory]
    [InlineData("Production", false, false)]
    [InlineData("Staging", false, false)]
    [InlineData("Production", true, true)]
    [InlineData("Development", false, true)]
    public void Audit_can_be_disabled_only_in_development(string environment, bool enabled, bool valid)
    {
        ValidateOptionsResult result = new AuditOptionsValidator(new Env(environment)).Validate(null, new AuditOptions { Enabled = enabled });

        Assert.Equal(valid, result.Succeeded);
    }

    [Fact]
    public void A_negative_unauthenticated_row_cap_is_refused_and_zero_is_allowed()
    {
        var validator = new AuditOptionsValidator(new Env("Development"));

        Assert.False(validator.Validate(null, new AuditOptions { UnauthenticatedRowsPerIpPerMinute = -1 }).Succeeded);
        Assert.True(validator.Validate(null, new AuditOptions { UnauthenticatedRowsPerIpPerMinute = 0 }).Succeeded);
    }

    [Fact]
    public void A_size_bound_too_small_to_hold_a_field_is_refused()
    {
        Assert.False(new AuditOptionsValidator(new Env("Development")).Validate(null, new AuditOptions { MaxChangesBytes = 10 }).Succeeded);
    }
}
