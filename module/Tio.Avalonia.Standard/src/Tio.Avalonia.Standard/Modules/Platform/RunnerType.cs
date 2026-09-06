using Tio.Avalonia.Standard.Modules.DiskIO;

namespace Tio.Avalonia.Standard.Modules.Platform;

public enum RunnerType
{
    Unknown,
    Desktop,     // 包含 Windows / Linux / macOS
    Android,     // 安卓端
    Ios,         // 苹果移动端
    Browser      // 浏览器 / WebAssembly 
}

public static class RunnerTypeDetector
{
    private static bool _initialized;
    
    public static RunnerType CurrentRunner { get; private set; } = RunnerType.Unknown;

    public static void Initialize(RunnerType runner)
    {
        if (_initialized) return;

        CurrentRunner = runner;
        _initialized = true;

        Logger.Info($"[Platform] 运行环境系统初始化成功，当前运行于: {CurrentRunner}");
    }


    public static bool IsDesktop => CurrentRunner == RunnerType.Desktop;
    public static bool IsMobile => CurrentRunner is RunnerType.Android or RunnerType.Ios;
    public static bool IsAndroid => CurrentRunner == RunnerType.Android;
    public static bool IsIos => CurrentRunner == RunnerType.Ios;
    public static bool IsBrowser => CurrentRunner == RunnerType.Browser;
}