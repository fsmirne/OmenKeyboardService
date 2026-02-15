using Microsoft.Extensions.Logging;

namespace OmenKeyboardService;

/// <summary>
/// Options for configuring retry behavior
/// </summary>
public record RetryOptions(int MaxRetries, int InitialDelayMs, int MaxDelayMs, string OperationName);

/// <summary>
/// Static helper for executing operations with exponential backoff retry logic
/// </summary>
public static class RetryPolicy
{
    public static async Task ExecuteWithRetryAsync(Func<Task> action, RetryOptions options, ILogger logger, string? contextNote = null)
    {
        int attempt = 0;
        Exception? lastException = null;
        int delayMs = options.InitialDelayMs;

        while (attempt < options.MaxRetries)
        {
            try
            {
                attempt++;
                await action();

                if (attempt > 1)
                {
                    logger.LogInformation("Successfully completed {Operation} on attempt {Attempt}{ContextNote}", options.OperationName, attempt, contextNote ?? "");
                }

                return;
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt < options.MaxRetries)
                {
                    logger.LogWarning(ex, "Failed {Operation} (attempt {Attempt}/{MaxRetries}). Retrying in {DelayMs}ms...{ContextNote}", options.OperationName, attempt, options.MaxRetries, delayMs, contextNote ?? "");
                    await Task.Delay(delayMs);

                    // Exponential backoff with cap
                    delayMs = Math.Min(delayMs * 2, options.MaxDelayMs);
                }
            }
        }

        throw new RetryExhaustedException($"{options.OperationName} failed after {options.MaxRetries} attempts.", lastException!);
    }
}

/// <summary>
/// Exception thrown when all retry attempts have been exhausted
/// </summary>
public class RetryExhaustedException : Exception
{
    public RetryExhaustedException(string message, Exception innerException) : base(message, innerException) { }
}
