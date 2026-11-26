using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Webhooks;

/// <summary>
/// Runs <see cref="OutboxDispatcher"/> in a loop until the host stops (ADR-011). It polls again at once while there is work and sleeps
/// <c>PollIntervalMs</c> otherwise; an unexpected error is logged and the loop continues. On shutdown the stopping token cancels in-flight sends and
/// the cycle exits: claimed deliveries keep their lease and are retried by whichever instance sees the lease expire, so nothing is lost and
/// nothing is abandoned.
/// </summary>
internal sealed partial class OutboxDispatcherService(OutboxDispatcher dispatcher, IOptions<WebhookOptions> options, TimeProvider time, ILogger<OutboxDispatcherService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan poll = TimeSpan.FromMilliseconds(options.Value.Dispatcher.PollIntervalMs);
        while (!stoppingToken.IsCancellationRequested)
        {
            bool idle = true;
            try
            {
                idle = (await dispatcher.RunOnceAsync(stoppingToken)).Work == 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogCycleFailed(ex);
            }

            if (idle)
            {
                try
                {
                    await Task.Delay(poll, time, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The outbox dispatcher cycle failed; retrying after the poll interval")]
    private partial void LogCycleFailed(Exception exception);
}
