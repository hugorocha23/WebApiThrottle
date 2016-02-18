using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace WebApiThrottle
{
    /// <summary>
    /// Stores throttle metrics in a thread safe dictionary, has no clean-up mechanism, expired counters are deleted on renewal
    /// </summary>
    public class ConcurrentDictionaryRepository : IThrottleRepository
    {
        private static ConcurrentDictionary<string, ThrottleCounterWrapper> cache = new ConcurrentDictionary<string, ThrottleCounterWrapper>();

        public bool Any(string id)
        {
            return cache.ContainsKey(id);
        }

        /// <summary>
        /// Insert or update
        /// </summary>
        /// <param name="id">
        /// The id.
        /// </param>
        /// <returns>
        /// The <see cref="ThrottleCounter"/>.
        /// </returns>
        public ThrottleCounter? FirstOrDefault(string id)
        {
            var entry = new ThrottleCounterWrapper();

            if (cache.TryGetValue(id, out entry))
            {
                // remove expired entry
                if (entry.Timestamp + entry.ExpirationTime < DateTime.UtcNow)
                {
                    cache.TryRemove(id, out entry);
                    return null;
                }
            }

            if (!Any(id))
            {
                return null;
            }

            return new ThrottleCounter
            {
                Timestamp = entry.Timestamp,
                TotalRequests = entry.TotalRequests
            };
        }

        public void Save(string id, ThrottleCounter throttleCounter, TimeSpan expirationTime)
        {
            var entry = new ThrottleCounterWrapper
            {
                ExpirationTime = expirationTime,
                Timestamp = throttleCounter.Timestamp,
                TotalRequests = throttleCounter.TotalRequests
            };

            cache.AddOrUpdate(id, entry, (k, e) => entry);
        }

        public void Remove(string id)
        {
            var entry = new ThrottleCounterWrapper();
            cache.TryRemove(id, out entry);
        }

        public void Clear()
        {
            cache.Clear();
        }

        public Task<ThrottleCounter> IncAsync(string id, TimeSpan expirationTime)
        {
            return Task.Run(() =>
            {
                var now = DateTime.UtcNow;
                var entry = new ThrottleCounterWrapper
                {
                    ExpirationTime = expirationTime,
                    Timestamp = now,
                    TotalRequests = 1
                };

                cache.AddOrUpdate(id, entry, (key, old) =>
                {
                    if (old.Timestamp + expirationTime < now)
                    {
                        old.Timestamp = now;
                        old.TotalRequests = 1;
                    }
                    else
                    {
                        old.TotalRequests++;
                    }
                    entry = old;
                    return old;
                });

                return new ThrottleCounter
                {
                    Timestamp = entry.Timestamp,
                    TotalRequests = entry.TotalRequests
                };
            });
        }

        [Serializable]
        internal struct ThrottleCounterWrapper
        {
            public DateTime Timestamp { get; set; }

            public long TotalRequests { get; set; }

            public TimeSpan ExpirationTime { get; set; }
        }
    }
}
