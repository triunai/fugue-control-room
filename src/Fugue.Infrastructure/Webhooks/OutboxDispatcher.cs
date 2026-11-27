using System.Security.Cryptography;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Webhooks.Repos;
using Fugue.Domain.Ids;
using Fugue.Domain.Webhooks;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Webhooks;

/// <summary>What one dispatcher cycle did, for the service loop (poll again at once when there was work) and for tests.</summary>
internal sealed record DispatchSummary(int Relayed, int Claimed, int Delivered, int Failed, int DeadLettered)
{
    public int Work => Relayed + Claimed;
}

/// <summary>
/// The outbox dispatcher (ADR-011, D-039). One cycle, safe to run in many processes at once:
/// <list type="number">
/// <item><b>Relay</b>: in ONE transaction, claim pending outbox messages with <c>FOR UPDATE SKIP LOCKED</c> (a second instance skips rows the first holds),
/// hand each to every <see cref="IEventPublisher"/> (the webhook adapter inserts the deliveries) and mark it <c>dispatched</c>. A savepoint per message
/// isolates a failing publish, which is then retried with backoff and finally marked <c>dead</c>.</item>
/// <item><b>Deliver</b>: claim due deliveries with a lease (<c>UPDATE ... WHERE id IN (SELECT ... FOR UPDATE SKIP LOCKED) RETURNING</c>), counting the attempt AT
/// CLAIM, then send over HTTP with NO transaction open (a slow receiver must not pin a connection or a row lock) and write the result, fenced by a
/// per-claim token (the attempt number restarts on redeliver, so it cannot be the fence) so a worker whose lease expired cannot overwrite a newer attempt. An expired lease is reclaimed, so a crash costs one counted attempt.</item>
/// </list>
/// The outbox is the single retry authority: the resilience handler does not retry a POST, so every attempt is counted, backed off and visible.
/// </summary>
internal sealed partial class OutboxDispatcher(IServiceScopeFactory scopes, TimeProvider time, IOptions<WebhookOptions> options, ILogger<OutboxDispatcher> logger)
{
    private static readonly string ClaimMessagesSql = SqlScripts.Get("webhooks/claim-messages");

    private static readonly string ClaimDeliveriesSql = SqlScripts.Get("webhooks/claim-deliveries");

    // A lease that expired on the FINAL attempt means the worker crashed or lost its result write; the budget is spent, so it dies instead of being sent again.
    private static readonly string ExpireSpentLeasesSql = SqlScripts.Get("webhooks/expire-spent-leases");

    // The lease ran out before this queued delivery was reached: give the claim back without costing an attempt, so another worker owns it cleanly.
    private static readonly string ReleaseSql = SqlScripts.Get("webhooks/release-delivery");

    private static readonly string MarkSucceededSql = SqlScripts.Get("webhooks/mark-succeeded");

    private static readonly string MarkFailedSql = SqlScripts.Get("webhooks/mark-failed");

    private static readonly TimeSpan ResultWriteBudget = TimeSpan.FromSeconds(10);

    public async Task<DispatchSummary> RunOnceAsync(CancellationToken ct)
    {
        int relayed = await RelayAsync(ct);
        (int claimed, int delivered, int failed, int dead) = await DeliverAsync(ct);
        return new DispatchSummary(relayed, claimed, delivered, failed, dead);
    }

    private async Task<int> RelayAsync(CancellationToken ct)
    {
        WebhookOptions opts = options.Value;
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        var sql = scope.ServiceProvider.GetRequiredService<SqlExecutor>();
        IReadOnlyList<IEventPublisher> publishers = scope.ServiceProvider.GetServices<IEventPublisher>().ToList();
        await using IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        DateTimeOffset now = time.GetUtcNow();

        await unitOfWork.BeginAsync(ct);
        IReadOnlyList<ClaimedMessage> messages = await sql.QueryAsync<ClaimedMessage>(ClaimMessagesSql, new { Now = now, Batch = opts.Dispatcher.BatchSize }, ct);
        foreach (ClaimedMessage message in messages)
        {
            await sql.ExecuteAsync(SqlScripts.Get("webhooks/relay-savepoint"), null, ct);
            try
            {
                foreach (IEventPublisher publisher in publishers)
                {
                    await publisher.PublishAsync(message.ToEvent(), ct);
                }

                await sql.ExecuteAsync(
                    SqlScripts.Get("outbox/mark-dispatched"),
                    new { Now = now, message.Id }, ct);
                await sql.ExecuteAsync(SqlScripts.Get("webhooks/relay-release-savepoint"), null, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await sql.ExecuteAsync(SqlScripts.Get("webhooks/relay-rollback-savepoint"), null, ct);
                int failed = message.Attempts + 1;
                bool dead = failed >= opts.MaxAttempts;
                await sql.ExecuteAsync(
                    SqlScripts.Get("outbox/mark-failed"),
                    new
                    {
                        Status = dead ? OutboxStatuses.Dead : OutboxStatuses.Pending,
                        Attempts = failed,
                        Next = now + Backoff.Delay(failed, opts, Random.Shared.NextDouble()),
                        Error = Clip($"{ex.GetType().Name}: {ex.Message}"),
                        message.Id,
                    },
                    ct);
                await sql.ExecuteAsync(SqlScripts.Get("webhooks/relay-release-savepoint"), null, ct);
                LogRelayFailed(message.Id, message.EventType, failed, dead, ex);
            }
        }

        await unitOfWork.CommitAsync(ct);
        return messages.Count;
    }

    private async Task<(int Claimed, int Delivered, int Failed, int Dead)> DeliverAsync(CancellationToken ct)
    {
        WebhookOptions opts = options.Value;
        IReadOnlyList<ClaimedDelivery> claimed;
        DateTimeOffset leaseUntil;
        await using (AsyncServiceScope scope = scopes.CreateAsyncScope())
        {
            DateTimeOffset now = time.GetUtcNow();
            var sql = scope.ServiceProvider.GetRequiredService<SqlExecutor>();
            await sql.ExecuteAsync(ExpireSpentLeasesSql, new { Now = now, opts.MaxAttempts }, ct);
            leaseUntil = now.AddSeconds(opts.LeaseSeconds);
            claimed = await sql.QueryAsync<ClaimedDelivery>(ClaimDeliveriesSql, new { Now = now, LeaseUntil = leaseUntil, Batch = opts.Dispatcher.BatchSize }, ct);
        }

        int delivered = 0, failed = 0, dead = 0;
        await Parallel.ForEachAsync(
            claimed,
            new ParallelOptions { MaxDegreeOfParallelism = opts.Dispatcher.MaxConcurrentSends, CancellationToken = ct },
            async (delivery, token) =>
            {
                switch (await DeliverOneAsync(delivery, leaseUntil, token))
                {
                    case Outcome.Delivered: Interlocked.Increment(ref delivered); break;
                    case Outcome.Failed: Interlocked.Increment(ref failed); break;
                    case Outcome.DeadLettered: Interlocked.Increment(ref dead); break;
                }
            });

        return (claimed.Count, delivered, failed, dead);
    }

    private async Task<Outcome> DeliverOneAsync(ClaimedDelivery delivery, DateTimeOffset leaseUntil, CancellationToken ct)
    {
        WebhookOptions opts = options.Value;
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        FugueDbContext db = scope.ServiceProvider.GetRequiredService<FugueDbContext>();
        var sql = scope.ServiceProvider.GetRequiredService<SqlExecutor>();

        // The whole batch is leased up front but sent a few at a time, so a queued delivery can wait out its own lease. Do not start an attempt that
        // cannot finish inside the lease: another worker may already hold the row.
        if (time.GetUtcNow().AddSeconds(opts.AttemptTimeoutSeconds) >= leaseUntil)
        {
            await sql.ExecuteAsync(ReleaseSql, new { delivery.Id, delivery.ClaimToken }, ct);
            return Outcome.Skipped;
        }

        OutboxMessage? message = await db.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == delivery.MessageId, ct);
        WebhookSubscription? subscription = await db.WebhookSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == delivery.SubscriptionId, ct);
        if (message is null || subscription is null)
        {
            return Outcome.Skipped; // the row was deleted (retention or a deleted subscription): the cascade took the delivery with it
        }

        SendResult result;
        if (!subscription.IsActive)
        {
            result = new SendResult(false, null, "The subscription is inactive.", 0);
            return await RecordAsync(sql, delivery, result, deadNow: true);
        }

        string secret;
        try
        {
            secret = scope.ServiceProvider.GetRequiredService<ISecretProtector>().Unprotect(subscription.SecretCiphertext);
        }
        catch (CryptographicException)
        {
            return await RecordAsync(sql, delivery, new SendResult(false, null, "The stored secret cannot be decrypted (key ring changed); rotate it.", 0), deadNow: true);
        }

        result = await scope.ServiceProvider.GetRequiredService<WebhookSender>().SendAsync(new Uri(subscription.Url), secret, ToEvent(message), ct);
        return await RecordAsync(sql, delivery, result, deadNow: false);
    }

    /// <summary>Writes the outcome. Uses its own short budget, not the (possibly cancelled) host token: a send that finished must be recorded.</summary>
    private async Task<Outcome> RecordAsync(SqlExecutor sql, ClaimedDelivery delivery, SendResult result, bool deadNow)
    {
        WebhookOptions opts = options.Value;
        using var budget = new CancellationTokenSource(ResultWriteBudget);
        DateTimeOffset now = time.GetUtcNow();

        if (result.Success)
        {
            await sql.ExecuteAsync(MarkSucceededSql, new { Now = now, result.StatusCode, result.DurationMs, delivery.Id, delivery.ClaimToken }, budget.Token);
            return Outcome.Delivered;
        }

        bool dead = deadNow || delivery.Attempts >= opts.MaxAttempts;
        await sql.ExecuteAsync(
            MarkFailedSql,
            new
            {
                Status = dead ? WebhookDeliveryStatuses.Dead : WebhookDeliveryStatuses.Pending,
                NextAttemptAt = dead ? now : now + Backoff.Delay(delivery.Attempts, opts, Random.Shared.NextDouble()),
                result.StatusCode,
                Error = Clip(result.Error),
                result.DurationMs,
                DeadLetteredAt = dead ? now : (DateTimeOffset?)null,
                delivery.Id,
                delivery.ClaimToken,
            },
            budget.Token);
        if (dead)
        {
            LogDeadLettered(delivery.Id, delivery.SubscriptionId, delivery.Attempts);
        }

        return dead ? Outcome.DeadLettered : Outcome.Failed;
    }

    private static OutboxEvent ToEvent(OutboxMessage m) =>
        new(m.Id.Value, m.OccurredAt, m.EventType, m.AggregateType, m.AggregateId, m.Payload, m.CorrelationId, m.SagaId, m.Traceparent);

    private static string? Clip(string? text) => text is null || text.Length <= 1000 ? text : text[..1000];

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} ({EventType}) failed to publish (attempt {Attempts}, dead: {Dead})")]
    private partial void LogRelayFailed(Guid messageId, string eventType, int attempts, bool dead, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook delivery {DeliveryId} to subscription {SubscriptionId} dead-lettered after {Attempts} attempt(s)")]
    private partial void LogDeadLettered(WebhookDeliveryId deliveryId, WebhookSubscriptionId subscriptionId, int attempts);

    private enum Outcome
    {
        Skipped,
        Delivered,
        Failed,
        DeadLettered,
    }

    private sealed record ClaimedMessage(
        Guid Id, DateTime OccurredAt, string EventType, string AggregateType, string AggregateId, string Payload, string? CorrelationId, string SagaId,
        string? Traceparent, int Attempts)
    {
        public OutboxEvent ToEvent() => new(Id, new DateTimeOffset(DateTime.SpecifyKind(OccurredAt, DateTimeKind.Utc)), EventType, AggregateType, AggregateId, Payload, CorrelationId, SagaId, Traceparent);
    }

    private sealed record ClaimedDelivery(WebhookDeliveryId Id, OutboxMessageId MessageId, WebhookSubscriptionId SubscriptionId, int Attempts, Guid ClaimToken);
}
