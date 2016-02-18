using System;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;

namespace WebApiThrottle
{
    /// <summary>
    /// Stores throttle metrics in asp.net cache
    /// </summary>
    public class CacheRepository : IThrottleRepository
    {
        private static readonly object ProcessLocker = new object();

        /// <summary>
        /// Insert or update
        /// </summary>
        /// <param name="id">
        /// The id.
        /// </param>
        /// <param name="throttleCounter">
        /// The throttle Counter.
        /// </param>
        /// <param name="expirationTime">
        /// The expiration Time.
        /// </param>
        public void Save(string id, ThrottleCounter throttleCounter, TimeSpan expirationTime)
        {
            if (HttpContext.Current.Cache[id] != null)
            {
                HttpContext.Current.Cache[id] = throttleCounter;
            }
            else
            {
                HttpContext.Current.Cache.Add(
                    id,
                    throttleCounter,
                    null,
                    Cache.NoAbsoluteExpiration,
                    expirationTime,
                    CacheItemPriority.Low,
                    null);
            }
        }

        public bool Any(string id)
        {
            return HttpContext.Current.Cache[id] != null;
        }

        public ThrottleCounter? FirstOrDefault(string id)
        {
            return (ThrottleCounter?)HttpContext.Current.Cache[id];
        }

        public void Remove(string id)
        {
            HttpContext.Current.Cache.Remove(id);
        }

        public void Clear()
        {
            var cacheEnumerator = HttpContext.Current.Cache.GetEnumerator();
            while (cacheEnumerator.MoveNext())
            {
                if (cacheEnumerator.Value is ThrottleCounter)
                {
                    HttpContext.Current.Cache.Remove(cacheEnumerator.Key.ToString());
                }
            }
        }

        public Task<ThrottleCounter> IncAsync(string id, TimeSpan expirationTime)
        {
            return Task.Factory.StartNew(
                state =>
                {
                    var httpContext = (HttpContext)state;

                    lock (ProcessLocker)
                    {
                        var throttleCounter = new ThrottleCounter()
                        {
                            Timestamp = DateTime.UtcNow,
                            TotalRequests = 1
                        };

                        if (httpContext.Cache[id] != null)
                        {
                            throttleCounter = (ThrottleCounter)httpContext.Cache[id];
                            throttleCounter.TotalRequests++;
                            httpContext.Cache[id] = throttleCounter;
                        }
                        else
                        {
                            httpContext.Cache.Add(
                                id,
                                throttleCounter,
                                null,
                                Cache.NoAbsoluteExpiration,
                                expirationTime,
                                CacheItemPriority.Low,
                                null);
                        }

                        return throttleCounter;
                    }
                },
                HttpContext.Current);
        }
    }
}
