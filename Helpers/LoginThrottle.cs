using System.Collections.Concurrent;

namespace LDHub.Helpers;

/// <summary>
/// Slows down password guessing: 5 failed logins for the same email from the same address
/// lock further attempts for 15 minutes. Kept in memory (resets when the app restarts).
/// </summary>
public class LoginThrottle
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private class Entry
    {
        public int Failures;
        public DateTime WindowStart;
        public DateTime? LockedUntil;
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public static string Key(HttpContext ctx, string? email) =>
        $"{ctx.Connection.RemoteIpAddress}|{(email ?? string.Empty).Trim().ToLowerInvariant()}";

    public bool IsLocked(string key, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (!_entries.TryGetValue(key, out var entry)) return false;

        lock (entry)
        {
            if (entry.LockedUntil is { } until && until > DateTime.UtcNow)
            {
                remaining = until - DateTime.UtcNow;
                return true;
            }
        }
        return false;
    }

    public void RegisterFailure(string key)
    {
        var now = DateTime.UtcNow;
        var entry = _entries.GetOrAdd(key, _ => new Entry { WindowStart = now });

        lock (entry)
        {
            if (now - entry.WindowStart > Window)
            {
                entry.Failures = 0;
                entry.WindowStart = now;
                entry.LockedUntil = null;
            }

            entry.Failures++;
            if (entry.Failures >= MaxFailures)
                entry.LockedUntil = now + LockDuration;
        }

        if (_entries.Count > 10000) Cleanup(now);
    }

    public void Reset(string key) => _entries.TryRemove(key, out _);

    private void Cleanup(DateTime now)
    {
        foreach (var kv in _entries)
        {
            lock (kv.Value)
            {
                var expired = now - kv.Value.WindowStart > Window
                              && (kv.Value.LockedUntil == null || kv.Value.LockedUntil < now);
                if (expired) _entries.TryRemove(kv.Key, out _);
            }
        }
    }
}
