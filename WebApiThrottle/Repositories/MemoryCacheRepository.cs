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
        readonly ObjectCache memCache = MemoryCache.Default;
        private static readonly object ProcessLocker = new object();

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
                    ThrottleCounter throttleCounter;

                    if (memCache.Contains(id))
                    {
                        throttleCounter = (ThrottleCounter)memCache[id];
                        throttleCounter.TotalRequests++;
                        memCache[id] = throttleCounter;
                        return throttleCounter;
                    }

                    throttleCounter = new ThrottleCounter()
                    {
                        Timestamp = DateTime.UtcNow,
                        TotalRequests = 1
                    };
                    memCache.Add(
                        id,
                        throttleCounter,
                        new CacheItemPolicy()
                        {

                            SlidingExpiration = expirationTime
                        });

                    return throttleCounter;
                }
            });
        }
    }
}
