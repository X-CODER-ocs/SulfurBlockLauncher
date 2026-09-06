using System.Runtime.InteropServices;
using Tio.Avalonia.Standard.Modules.DiskIO;

namespace Tio.Avalonia.Standard.Modules.Platform;

public static class DesktopTypeDetector
{
    private static bool _initialized;
    
    /// <summary>
    /// 默认值为 Unknown，直到调用 Initialize。
    /// </summary>
    public static DesktopType CurrentPlatform { get; private set; } = DesktopType.Unknown;

    /// <summary>
    /// 显式初始化方法。在主程序启动时手动调用。
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                CurrentPlatform = DesktopType.Windows;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                CurrentPlatform = DesktopType.Linux;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                CurrentPlatform = DesktopType.MacOs;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            {
                CurrentPlatform = DesktopType.FreeBsd;
            }
            else
            {
                CurrentPlatform = DesktopType.Unknown;
            }

            _initialized = true;
            
            Logger.Info($"[Platform] 平台检测系统显式初始化成功，当前平台为: {CurrentPlatform}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Platform] 平台检测失败: {ex.Message}");
        }
    }
}

public enum DesktopType
{
    Unknown,
    Windows,
    Linux,
    MacOs,
    FreeBsd
}