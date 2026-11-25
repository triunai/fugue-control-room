using Fugue.Domain.Results;
using Mediator;

namespace Fugue.Tests.Api;

/// <summary>
/// Test double for <see cref="ISender"/>. The production Mediator source generator only sees handlers in
/// the assembly it runs in (Fugue.Api), so test-only requests would not be found by the real mediator.
/// Replacing <see cref="ISender"/> keeps the tests deterministic: each request type maps to a handler
/// delegate in <see cref="ProbeHandlers"/>, defined in this assembly.
/// </summary>
public sealed class FakeSender : ISender
{
    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (!ProbeHandlers.Table.TryGetValue(request.GetType(), out Func<object, object>? handler))
        {
            throw new InvalidOperationException($"No fake handler registered for {request.GetType().Name}.");
        }

        return ValueTask.FromResult((TResponse)handler(request));
    }

    public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<object?> Send(object message, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object message, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
