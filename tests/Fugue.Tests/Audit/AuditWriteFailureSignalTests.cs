using System.Diagnostics.Metrics;
using FluentResults;
using Fugue.Application.Abstractions;
using Fugue.Application.Common;
using Fugue.Application.Common.Behaviors;
using Fugue.Application.Features.Customers;
using Fugue.Application.Features.Customers.Commands;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Results;
using Fugue.Infrastructure.Audit;
using Fugue.Infrastructure.Identity;
using Fugue.Infrastructure.Persistence;
using Fugue.Tests.Persistence;
using Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fugue.Tests.Audit;

/// <summary>Captures the measurements of the <c>Fugue.Audit</c> meter while it is open.</summary>
internal sealed class AuditMeterCapture : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly List<(long Value, Dictionary<string, object?> Tags)> _measurements = [];

    public AuditMeterCapture()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AuditMetrics.MeterName && instrument.Name == AuditMetrics.WriteFailuresName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            lock (_measurements)
            {
                _measurements.Add((value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));
            }
        });
        _listener.Start();
    }

    public long Count(string table, string reason)
    {
        lock (_measurements)
        {
            return _measurements.Where(m => (string?)m.Tags["table"] == table && (string?)m.Tags["reason"] == reason).Sum(m => m.Value);
        }
    }

    public void Dispose() => _listener.Dispose();
}

/// <summary>A failed failure-row write is counted and logged with the lost row's identifying fields, never its content (W-018).</summary>
public sealed class AuditWriteFailureSignalTests
{
    private sealed class ThrowingTrail : IAuditTrail
    {
        public Task RecordAsync(AuditEntry entry, CancellationToken ct) => Task.CompletedTask;

        public Task RecordFailureAsync(AuditFailure failure, CancellationToken ct) => throw new TimeoutException("audit store down");
    }

    private sealed class CapturingProvider : ILoggerProvider
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }

    private sealed class FixedUser : ICurrentUser
    {
        public Guid? UserId => null;

        public Guid? TenantId => null;

        public bool IsAuthenticated => false;

        public bool HasPermission(string code) => true;
    }

    private sealed class FixedCorrelation : ICorrelationContext
    {
        public string CorrelationId => "corr-signal-1";

        public string? SagaId => null;

        public void SetSaga(string sagaId)
        {
        }
    }

    [Fact]
    public async Task A_lost_failure_row_increments_the_counter_and_logs_its_identity_but_not_its_content()
    {
        var logs = new CapturingProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(logs));
        services.AddScoped<IAuditScope, AuditScope>();
        services.AddSingleton<IAuditTrail>(new ThrowingTrail());
        services.AddSingleton<ICurrentUser>(new FixedUser());
        services.AddSingleton<ICorrelationContext>(new FixedCorrelation());
        ServiceProvider provider = services.BuildServiceProvider();
        var command = new UpdateCustomerCommand(Fugue.Domain.Ids.CustomerId.New(), "Sensitive Name", "pii@example.test", null, 1);
        var behavior = new AuditBehavior<UpdateCustomerCommand, Result<CustomerDto>>(provider);
        using var capture = new AuditMeterCapture();

        Result<CustomerDto> result = await behavior.Handle(
            command, (_, _) => new ValueTask<Result<CustomerDto>>(Result.Fail<CustomerDto>(CustomerErrors.VersionMismatch())), default);

        Assert.True(result.IsFailed); // the command's own result is untouched
        Assert.Equal(1, capture.Count("audit_log", "TimeoutException"));
        (LogLevel level, string message, Exception? exception) = Assert.Single(logs.Entries, e => e.Message.StartsWith("Lost a failure audit row", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Warning, level);
        Assert.IsType<TimeoutException>(exception);
        Assert.Contains("customers.update", message);
        Assert.Contains("Customer", message);
        Assert.Contains(command.Id.ToString(), message);
        Assert.Contains("customer.version_mismatch", message);
        Assert.Contains("corr-signal-1", message);
        Assert.DoesNotContain("Sensitive Name", message); // no command payload, changes or metadata
        Assert.DoesNotContain("pii@example.test", message);
        Assert.DoesNotContain("UpdateCustomerCommand", message);
    }
}

/// <summary>The same counter for <c>access_audit</c>: the writer swallows a failed insert, so the swallow must be visible.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AccessAuditWriteFailureSignalTests(PostgresFixture db)
{
    [Fact]
    public async Task A_swallowed_access_audit_failure_increments_the_counter()
    {
        await using AsyncServiceScope scope = db.Factory.Services.CreateAsyncScope();
        var writer = new AccessAuditWriter(
            scope.ServiceProvider.GetRequiredService<SqlExecutor>(), scope.ServiceProvider.GetRequiredService<IAuditStamp>(),
            scope.ServiceProvider.GetRequiredService<ICorrelationContext>(), new ConfigurationBuilder().Build(),
            scope.ServiceProvider.GetRequiredService<ILogger<AccessAuditWriter>>());
        using var capture = new AuditMeterCapture();

        // An event longer than its varchar(32) column fails the insert (22001); the writer must swallow it and count it.
        await writer.WriteAsync(new AccessAuditEntry(new string('e', 40), "denied", null, null, null, null), default);

        Assert.Equal(1, capture.Count("access_audit", "PostgresException"));
    }
}
