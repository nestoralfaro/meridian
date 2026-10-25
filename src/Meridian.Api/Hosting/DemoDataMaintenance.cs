using Meridian.Application.DemoData;
using Microsoft.Extensions.Options;

namespace Meridian.Api.Hosting;

internal sealed class DemoDataOptions
{
    public const string Section = "Demo";

    /// <summary>Seed and periodically reset the synthetic portfolio. Off by default; deployments and compose opt in.</summary>
    public bool Enabled { get; set; }

    /// <summary>Demo data older than this is reset (visitors' changes don't accumulate).</summary>
    public TimeSpan ResetAfter { get; set; } = TimeSpan.FromHours(24);

    /// <summary>How often a running instance re-checks.</summary>
    public TimeSpan CheckEvery { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Seeds the demo portfolio on startup and resets it once it's older than <see cref="DemoDataOptions.ResetAfter"/>.
/// It runs whenever the app is awake, so it works with scale-to-zero and needs no external scheduler or credentials.
/// Failures (e.g. the database is briefly unreachable) are logged and retried on the next check; they never stop the app.
/// </summary>
internal sealed partial class DemoDataMaintenance(
    IServiceScopeFactory scopes,
    IOptions<DemoDataOptions> options,
    TimeProvider clock,
    ILogger<DemoDataMaintenance> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(settings.CheckEvery, clock);
        do
        {
            await RunOnceAsync(settings, stoppingToken).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task RunOnceAsync(DemoDataOptions settings, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var demo = scope.ServiceProvider.GetRequiredService<IDemoData>();

            await demo.EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
            var lastReset = await demo.LastResetAtAsync(cancellationToken).ConfigureAwait(false);
            if (lastReset is null || clock.GetUtcNow() - lastReset >= settings.ResetAfter)
            {
                await demo.ResetAsync(cancellationToken).ConfigureAwait(false);
                LogReset(logger, lastReset);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data reset (previous reset: {LastReset})")]
    private static partial void LogReset(ILogger logger, DateTimeOffset? lastReset);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Demo data maintenance failed; retrying on the next check")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
