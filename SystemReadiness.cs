#if LINUX
using Microsoft.Extensions.Logging;

namespace OmenKeyboardService;

/// <summary>
/// Waits for the system to settle (low CPU load) before proceeding.
/// Prevents the service from competing for resources during boot or heavy operations like apt upgrades.
/// </summary>
public static class SystemReadiness
{
    private const string LoadAvgPath = "/proc/loadavg";
    private const int PollIntervalMs = 3000;

    /// <summary>
    /// Blocks until the 1-minute load average drops below the threshold, or the timeout expires.
    /// Returns true if the system settled, false if timed out.
    /// </summary>
    public static async Task<bool> WaitForLowLoadAsync(double maxLoadAverage, TimeSpan timeout, ILogger logger, CancellationToken cancellationToken)
    {
        if (!File.Exists(LoadAvgPath))
        {
            logger.LogDebug("/proc/loadavg not available, skipping readiness check");
            return true;
        }

        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double load = ReadLoadAverage();
            if (load < 0)
            {
                logger.LogDebug("Could not read load average, proceeding anyway");
                return true;
            }

            if (load <= maxLoadAverage)
            {
                logger.LogInformation("System load is {Load:F2} (threshold {Threshold:F1}), proceeding", load, maxLoadAverage);
                return true;
            }

            logger.LogInformation("System load is {Load:F2} (threshold {Threshold:F1}), waiting for system to settle...", load, maxLoadAverage);
            await Task.Delay(PollIntervalMs, cancellationToken);
        }

        logger.LogWarning("Timed out waiting for system load to drop below {Threshold:F1}. Proceeding anyway.", maxLoadAverage);
        return false;
    }

    private static double ReadLoadAverage()
    {
        try
        {
            var content = File.ReadAllText(LoadAvgPath);
            // /proc/loadavg format: "0.52 0.34 0.28 1/234 5678"
            var firstField = content.Split(' ')[0];
            if (double.TryParse(firstField, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double load))
                return load;
        }
        catch
        {
            // Swallow — caller handles negative return
        }

        return -1;
    }
}
#endif
