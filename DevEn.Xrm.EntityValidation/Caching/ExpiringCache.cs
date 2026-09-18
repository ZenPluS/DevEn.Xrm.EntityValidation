using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace DevEn.Xrm.EntityValidation.Caching
{
    /// <summary>
    /// Static, thread-safe, in-memory cache with a per-entry expiry, shared by everything the plugin reads
    /// from Dataverse but that changes far less often than the plugin runs (validation rules, entity
    /// metadata). Only plain data is stored - never an <c>IOrganizationService</c> or any other resource
    /// tied to a single execution - so sharing entries across later executions and threads is safe.
    /// Callers must include the organization id in the key: one sandbox worker process can serve several
    /// organizations.
    /// </summary>
    internal static class ExpiringCache
    {
        private static readonly ConcurrentDictionary<string, Lazy<CacheEntry>> Entries =
            new ConcurrentDictionary<string, Lazy<CacheEntry>>();

        public static T GetOrCreate<T>(string cacheKey, TimeSpan duration, Func<T> factory)
        {
            while (true)
            {
                var lazyEntry = Entries.GetOrAdd(
                    cacheKey,
                    _ => new Lazy<CacheEntry>(
                        () => new CacheEntry(factory(), DateTime.UtcNow.Add(duration)),
                        LazyThreadSafetyMode.ExecutionAndPublication));

                CacheEntry entry;
                try
                {
                    entry = lazyEntry.Value;
                }
                catch
                {
                    // The factory faulted (invalid configuration, transient Dataverse error...): evict the
                    // poisoned entry so the next call retries instead of replaying the same exception.
                    Remove(cacheKey, lazyEntry);
                    throw;
                }

                if (entry.ExpiresAtUtc > DateTime.UtcNow)
                {
                    return (T)entry.Value;
                }

                Remove(cacheKey, lazyEntry);
            }
        }

        private static void Remove(string cacheKey, Lazy<CacheEntry> expected)
        {
            // Only remove if it's still the same instance this thread saw, so a fresher entry published
            // meanwhile by another thread isn't evicted by accident.
            ((ICollection<KeyValuePair<string, Lazy<CacheEntry>>>)Entries)
                .Remove(new KeyValuePair<string, Lazy<CacheEntry>>(cacheKey, expected));
        }

        private sealed class CacheEntry
        {
            public CacheEntry(object value, DateTime expiresAtUtc)
            {
                Value = value;
                ExpiresAtUtc = expiresAtUtc;
            }

            public object Value { get; }

            public DateTime ExpiresAtUtc { get; }
        }
    }
}
