using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;

namespace WebApiThrottle
{
    /// <summary>
    /// Stors throttle metrics in runtime cache, intented for owin self host.
    /// </summary>
    public class MemoryCacheRepository : IThrottleRepository
    {
        readonly MemoryCache memCache = MemoryCache.Default;
        private static readonly object ProcessLocker = new object();

        public IDictionary<string, object> Data
        {
            get { return memCache.ToDictionary(o => o.Key, o => o.Value); }
        }

        /// <summary>
        /// Insert or update
        /// </summary>
        public void Save(string id, ThrottleCounter throttleCounter, TimeSpan expirationTime)
        {
            if (memCache[id] != null)
            {
                memCache[id] = throttleCounter;
            }
            else
            {
                memCache.Add(
                    id,
                    throttleCounter, new CacheItemPolicy()
                    {
                        SlidingExpiration = expirationTime
                    });
            }
        }

        public bool Any(string id)
        {
            return memCache[id] != null;
        }

        public ThrottleCounter? FirstOrDefault(string id)
        {
            return (ThrottleCounter?)memCache[id];
        }

        public void Remove(string id)
        {
            memCache.Remove(id);
        }

        public void Clear()
        {
            var cacheKeys = memCache.Where(kvp => kvp.Value is ThrottleCounter).Select(kvp => kvp.Key).ToList();
            foreach (string cacheKey in cacheKeys)
            {
                memCache.Remove(cacheKey);
            }
        }

        public Task<ThrottleCounter> IncAsync(string id, TimeSpan expirationTime)
        {
            return Task.Run(() =>
            {
                lock (ProcessLocker)
                {
                    var throttleCounter = new ThrottleCounter()
                    {
                        Timestamp = DateTime.UtcNow,
                        TotalRequests = 1
                    };

                    if (memCache.Contains(id))
                    {
                        throttleCounter = (ThrottleCounter)memCache[id];
                        throttleCounter.TotalRequests++;
                        memCache[id] = throttleCounter;
                    }
                    else
                    {
                        memCache.Add(
                            id,
                            throttleCounter,
                            new CacheItemPolicy()
                            {

                                SlidingExpiration = expirationTime
                            });
                    }

                    return throttleCounter;
                }
            });
        }
    }
}
