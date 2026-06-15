using System.Collections.Concurrent;

namespace Pokemon2.Server.Llm;

public sealed class LlmRequestLimiter
{
    private readonly int _permitLimit;
    private readonly ConcurrentDictionary<string, WindowState> _windows = new(StringComparer.Ordinal);

    public LlmRequestLimiter(LlmOptions options)
    {
        _permitLimit = Math.Max(1, options.RateLimitPerMinute);
    }

    public bool TryAcquire(string partitionKey, out TimeSpan retryAfter)
    {
        var now = DateTimeOffset.UtcNow;
        var key = string.IsNullOrWhiteSpace(partitionKey) ? "anonymous" : partitionKey.Trim();

        while (true)
        {
            var current = _windows.GetOrAdd(key, _ => new WindowState(now, 0));
            var elapsed = now - current.WindowStartedAt;
            if (elapsed >= TimeSpan.FromMinutes(1))
            {
                var reset = new WindowState(now, 1);
                if (_windows.TryUpdate(key, reset, current))
                {
                    retryAfter = TimeSpan.Zero;
                    return true;
                }

                continue;
            }

            if (current.Count >= _permitLimit)
            {
                retryAfter = TimeSpan.FromMinutes(1) - elapsed;
                return false;
            }

            var next = current with { Count = current.Count + 1 };
            if (_windows.TryUpdate(key, next, current))
            {
                retryAfter = TimeSpan.Zero;
                return true;
            }
        }
    }

    private sealed record WindowState(DateTimeOffset WindowStartedAt, int Count);
}
