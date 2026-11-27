using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Search;

/// <summary>
/// Copies <c>claims</c> into the search index (D-063), the same shape as <see cref="AuditIndexer"/> with one difference: claims change, so the cursor is
/// the last change <c>(updated_at, id)</c> and a changed claim is upserted again by id (idempotent). The checkpoint moves only after the engine has
/// applied the batch, so a crash repeats a batch and never skips one. The table stays the source of truth: dropping the index rebuilds it.
/// </summary>
internal sealed partial class ClaimIndexer(IServiceScopeFactory scopes, MeilisearchClaimSearch search, IOptions<MeilisearchOptions> options, TimeProvider time, ILogger<ClaimIndexer> logger)
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
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogCycleFailed(ex);
            }

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

    /// <summary>Indexes batches until caught up. Returns the number of rows sent.</summary>
    internal async Task<int> IndexOnceAsync(CancellationToken ct)
    {
        int batch = options.Value.IndexerBatchSize;
        int total = 0;
        (DateTimeOffset UpdatedAt, Guid Id)? checkpoint = await search.ReadCheckpointAsync(ct);
        while (true)
        {
            IReadOnlyList<ClaimIndexRow> rows;
            await using (AsyncServiceScope scope = scopes.CreateAsyncScope())
            {
                rows = await scope.ServiceProvider.GetRequiredService<ClaimIndexSource>().NextBatchAsync(checkpoint?.UpdatedAt, checkpoint?.Id, batch, ct);
            }

            if (rows.Count == 0)
            {
                return total;
            }

            ClaimIndexRow last = rows[^1];
            checkpoint = (last.Claim.UpdatedAt, last.Claim.Id.Value);
            await search.UpsertAsync([.. rows.Select(ClaimDocument.From)], checkpoint.Value, ct);
            total += rows.Count;
            if (rows.Count < batch)
            {
                return total;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The claim index cycle failed; retrying at the next interval")]
    private partial void LogCycleFailed(Exception exception);
}
