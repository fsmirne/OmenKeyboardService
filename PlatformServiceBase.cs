using Microsoft.Extensions.Logging;

namespace OmenKeyboardService;

/// <summary>
/// Base class for platform services providing shared deduplication logic for color reapply requests
/// </summary>
public abstract class PlatformServiceBase : IPlatformService
{
    protected readonly ILogger _logger;

    // Deduplication system to prevent rapid successive reapplications
    private DateTime _lastReapplyTime = DateTime.MinValue;
    private const int DeduplicationWindowMs = 3000;
    private readonly object _reapplyLock = new();

    public abstract string PlatformName { get; }

    public event EventHandler<ColorReapplyEventArgs>? ColorReapplyRequested;

    protected PlatformServiceBase(ILogger logger)
    {
        _logger = logger;
    }

    public abstract void Initialize();

    /// <summary>
    /// Detects resume from suspend by watching for a wall-clock jump. The system clock does not
    /// advance while suspended, so if substantially more wall-clock time elapses than the poll
    /// interval, the machine was asleep in between. Used on platforms (Linux, macOS) without a
    /// managed API for suspend/resume notifications.
    ///
    /// This is deliberately not implemented via a native power-notification API (e.g. polling
    /// /sys/power/wakeup_count on Linux, which is NOT a resume counter — the kernel increments it
    /// on every wakeup-capable device event during normal operation, causing spurious reapplies).
    /// </summary>
    protected async Task MonitorSuspendResumeAsync(CancellationToken cancellationToken)
    {
        const int PollIntervalMs = 10000;
        // A suspend is inferred when elapsed wall-clock time exceeds the poll interval
        // by more than this slack, which absorbs scheduler jitter under load.
        const int SuspendThresholdMs = 5000;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var expectedWake = DateTime.UtcNow.AddMilliseconds(PollIntervalMs);
                await Task.Delay(PollIntervalMs, cancellationToken);

                var driftMs = (DateTime.UtcNow - expectedWake).TotalMilliseconds;
                if (driftMs > SuspendThresholdMs)
                {
                    _logger.LogInformation("System resumed from suspend (slept ~{Seconds}s)", (int)(driftMs / 1000));
                    RequestColorReapply("System resumed from suspend", 2000, 5);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when service stops
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in suspend/resume monitor");
        }
    }

    /// <summary>
    /// Requests a color reapplication with deduplication to prevent rapid successive triggers
    /// </summary>
    protected void RequestColorReapply(string reason, int delayMs, int retryCount)
    {
        lock (_reapplyLock)
        {
            var now = DateTime.UtcNow;
            var timeSinceLastReapply = (now - _lastReapplyTime).TotalMilliseconds;

            if (timeSinceLastReapply < DeduplicationWindowMs)
            {
                _logger.LogInformation("Skipping color reapply request (reason: {Reason}). Last reapply was {TimeSinceLastReapply}ms ago (within {Window}ms deduplication window)", reason, (int)timeSinceLastReapply, DeduplicationWindowMs);
                return;
            }

            _logger.LogInformation("Processing color reapply request: {Reason}", reason);
            _lastReapplyTime = now;

            ColorReapplyRequested?.Invoke(this, new ColorReapplyEventArgs { Reason = reason, DelayMs = delayMs, RetryCount = retryCount });
        }
    }

    public abstract void Dispose();
}
