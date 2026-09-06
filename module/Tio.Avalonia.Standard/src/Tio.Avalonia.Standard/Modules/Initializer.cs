using Tio.Avalonia.Standard.Modules.DiskIO;
using Tio.Avalonia.Standard.Modules.Platform;

namespace Tio.Avalonia.Standard.Modules;

public class Initializer
{
    public static void Program(string appName, string? nameSpace = null, string version = "Unknown")
    {
        Logger.Initialize(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameSpace ?? appName, "Logs"),
            appName, version);
        if (RunnerTypeDetector.IsDesktop) DesktopTypeDetector.Initialize();
    }
}
