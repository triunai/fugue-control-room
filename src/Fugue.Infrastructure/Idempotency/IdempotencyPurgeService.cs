using Fugue.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Idempotency;

/// <summary>
/// Deletes expired <c>idempotency_keys</c> rows in batches every <c>PurgeIntervalMinutes</c> (ADR-013). The first cycle waits one interval, so a host
/// that is still migrating never races it. A failed cycle is logged and retried at the next interval.
/// </summary>
internal sealed partial class IdempotencyPurgeService(IServiceScopeFactory scopes, IOptions<IdempotencyOptions> options, TimeProvider time, ILogger<IdempotencyPurgeService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(options.Value.PurgeIntervalMinutes);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, time, stoppingToken);
                await PurgeOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogCycleFailed(ex);
            }
        }
    }

    /// <summary>One purge pass: batches until a batch comes back short. Returns the rows removed.</summary>
    internal async Task<int> PurgeOnceAsync(CancellationToken ct)
    {
        int batch = options.Value.PurgeBatchSize;
        int total = 0;
        int removed;
        do
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            removed = await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>().PurgeExpiredAsync(batch, ct);
            total += removed;
        }
        while (removed == batch);

        return total;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The idempotency purge cycle failed; retrying at the next interval")]
    private partial void LogCycleFailed(Exception exception);
}
