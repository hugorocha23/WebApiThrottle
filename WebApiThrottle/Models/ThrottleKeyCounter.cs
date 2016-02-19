using System;

namespace WebApiThrottle
{
    [Serializable]
    public struct ThrottleKeyCounter
    {
        public string Key { get; set; }

        public ThrottleCounter ThrottleCounter { get; set; }
    }
}
