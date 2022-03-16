using System;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     时间戳助手
/// </summary>
public static class TimeHelper
{
    /// <summary>
    ///     Unix11 时间戳（11位）转 DateTIme
    /// </summary>
    /// <param name="time"></param>
    /// <returns></returns>
    public static DateTime Unix11ToDateTime(long time)
    {
        return TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1), TimeZoneInfo.Local)
            .AddSeconds(time);
    }
}