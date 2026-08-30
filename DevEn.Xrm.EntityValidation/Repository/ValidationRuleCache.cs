using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Repository
{
    /// <summary>
    /// Static, thread-safe, in-memory cache of already-queried validation rules. Rules change far less
    /// often than how frequently Create/Update plugins run: querying the configuration table on every
    /// execution would waste performance. Only plain data is stored (never an <c>IOrganizationService</c>
    /// or any other resource tied to a single execution), so sharing across later executions/threads is safe.
    /// </summary>
    internal static class ValidationRuleCache
    {
        private static readonly ConcurrentDictionary<string, Lazy<CacheEntry>> Entries =
            new ConcurrentDictionary<string, Lazy<CacheEntry>>();

        public static IReadOnlyList<ValidationRuleDefinition> GetOrCreate(
            string cacheKey,
            TimeSpan duration,
            Func<IReadOnlyList<ValidationRuleDefinition>> factory)
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
                    // The factory faulted (e.g. invalid configuration): evict the poisoned entry so the
                    // next call retries from scratch instead of getting the same cached exception forever.
                    ((ICollection<KeyValuePair<string, Lazy<CacheEntry>>>)Entries)
                        .Remove(new KeyValuePair<string, Lazy<CacheEntry>>(cacheKey, lazyEntry));
                    throw;
                }

                if (entry.ExpiresAtUtc > DateTime.UtcNow)
                {
                    return entry.Rules;
                }

                // Expired entry: only remove it if it's still the same instance this thread inserted, so we
                // don't accidentally evict a fresher entry published meanwhile by another thread.
                ((ICollection<KeyValuePair<string, Lazy<CacheEntry>>>)Entries)
                    .Remove(new KeyValuePair<string, Lazy<CacheEntry>>(cacheKey, lazyEntry));
            }
        }

        private sealed class CacheEntry
        {
            public CacheEntry(IReadOnlyList<ValidationRuleDefinition> rules, DateTime expiresAtUtc)
            {
                Rules = rules;
                ExpiresAtUtc = expiresAtUtc;
            }

            public IReadOnlyList<ValidationRuleDefinition> Rules { get; }

            public DateTime ExpiresAtUtc { get; }
        }
    }
}
