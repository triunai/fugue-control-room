using FluentResults;
using Fugue.Application.Common.Behaviors;
using Fugue.Domain.Results;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Fugue.Tests.Application;

public sealed class LoggingBehaviorTests
{
    public sealed record RegisterUser(string Email) : ICommand<Result>;

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Text)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private static async Task<List<(LogLevel Level, string Text)>> Run(Result outcome)
    {
        var logger = new CapturingLogger<LoggingBehavior<RegisterUser, Result>>();
        var behavior = new LoggingBehavior<RegisterUser, Result>(logger);

        await behavior.Handle(
            new RegisterUser("person@example.com"), (_, _) => ValueTask.FromResult(outcome), CancellationToken.None);
        return logger.Entries;
    }

    [Fact]
    public async Task Success_is_logged_at_information_with_name_and_elapsed_but_not_the_payload()
    {
        var entry = Assert.Single(await Run(Result.Ok()));

        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("RegisterUser", entry.Text, StringComparison.Ordinal);
        Assert.Contains("ms", entry.Text, StringComparison.Ordinal);
        Assert.Contains("success", entry.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("person@example.com", entry.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_is_logged_at_warning_with_error_codes()
    {
        var entry = Assert.Single(await Run(Result.Fail(FugueErrors.Conflict("user.exists", "exists"))));

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("user.exists", entry.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("person@example.com", entry.Text, StringComparison.Ordinal);
    }
}
