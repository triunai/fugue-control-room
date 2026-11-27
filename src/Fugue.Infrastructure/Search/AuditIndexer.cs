using Fugue.Domain.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Search;

/// <summary>
/// Copies <c>audit_log</c> into the search index (D-058). Reads keyset batches after the checkpoint held in the engine, upserts them by id and
/// then moves the checkpoint, so a crash between the two repeats a batch (harmless, upserts are idempotent) and never skips one. The first cycle runs
/// at startup; a failed cycle is logged and retried at the next interval. The table stays the source of truth: dropping the index rebuilds it.
/// </summary>
internal sealed partial class AuditIndexer(IServiceScopeFactory scopes, MeilisearchAuditSearch search, IOptions<MeilisearchOptions> options, TimeProvider time, ILogger<AuditIndexer> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.IndexerIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await IndexOnceAsync(stoppingToken);
                await Task.Delay(interval, time, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogCycleFailed(ex);
                try
                {
                    await Task.Delay(interval, time, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>Indexes batches until caught up. Returns the number of rows sent.</summary>
    internal async Task<int> IndexOnceAsync(CancellationToken ct)
    {
        int batch = options.Value.IndexerBatchSize;
        int total = 0;
        (DateTimeOffset OccurredAt, Guid Id)? checkpoint = await search.ReadCheckpointAsync(ct);
        while (true)
        {
            IReadOnlyList<AuditLog> rows;
            await using (AsyncServiceScope scope = scopes.CreateAsyncScope())
            {
                rows = await scope.ServiceProvider.GetRequiredService<AuditIndexSource>().NextBatchAsync(checkpoint?.OccurredAt, checkpoint?.Id, batch, ct);
            }

            if (rows.Count == 0)
            {
                return total;
            }

            AuditLog last = rows[^1];
            checkpoint = (last.OccurredAt, last.Id.Value);
            await search.UpsertAsync([.. rows.Select(AuditDocument.From)], checkpoint.Value, ct);
            total += rows.Count;
            if (rows.Count < batch)
            {
                return total;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The audit index cycle failed; retrying at the next interval")]
    private partial void LogCycleFailed(Exception exception);
}
