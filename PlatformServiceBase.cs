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

    public virtual Task WaitForSystemReadyAsync(TimeSpan timeout, ILogger logger, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
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
