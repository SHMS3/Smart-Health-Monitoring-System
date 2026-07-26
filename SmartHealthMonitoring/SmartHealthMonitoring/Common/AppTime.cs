using System;
using System.Runtime.InteropServices;

namespace SmartHealthMonitoring.Common;

public static class AppTime
{
    private static readonly string TimeZoneId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh";
    public static readonly TimeZoneInfo VnZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
    
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnZone);
}
