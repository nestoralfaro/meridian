namespace Meridian.Worker;

/// <summary>
/// Host for background jobs. Jobs (price alerts, scheduled content publishing) are added in later sprints.
/// </summary>
internal sealed partial class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Meridian worker started; no jobs registered yet")]
    private static partial void LogStarted(ILogger logger);
}
