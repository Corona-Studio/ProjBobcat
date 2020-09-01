using System;

namespace ProjBobcat.Class.Helper
{
    public static class TimeHelper
    {
        public static DateTime Unix11ToDateTime(long time)
        {
            return TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1))
                .AddSeconds(time);
        }

        public static DateTime Unix13ToDateTime(long time)
        {
            return TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1))
                .AddMilliseconds(time);
        }
    }
}