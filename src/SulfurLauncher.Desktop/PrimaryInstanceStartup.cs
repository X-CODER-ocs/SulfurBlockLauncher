using System.Runtime.InteropServices;
using SulfurLauncher.Core.Module.Ipc;
using SulfurLauncher.Core.Services;
using SulfurLauncher.Localization;
using Tio.Avalonia.Standard.Modules;
using Tio.Avalonia.Standard.Modules.DiskIO;

namespace SulfurLauncher.Desktop;

internal static class PrimaryInstanceStartup
{
    public static bool Run(string[] args)
    {
        SulfurLauncherCommandQueue.Initialize();
        PackagePathResolver.TryGetBedrockPackagePath(args, out var packagePath);
        if (packagePath != null)
            App.BedrockPackagePath = packagePath;

        if (PackagePathResolver.TryGetJavaPackagePath(args, out var javaPackagePath))
        {
            var javaCommand = new SulfurLauncherCommand
            {
                Kind = SulfurLauncherCommandKind.DownloadModpack,
                Source = javaPackagePath
            };

            if (packagePath == null && SulfurLauncherCommandService.TryForwardToRunningInstance(javaCommand))
            {
                Logger.Info(string.Format(LogLanguageManager.Instance.desktop_primaryInstance_javaForwarded.CurrentValue(), javaPackagePath));
                return false;
            }

            App.JavaPackagePath = javaPackagePath;
            if (packagePath == null)
                SulfurLauncherCommandQueue.Enqueue(javaCommand);
        }

        switch (packagePath)
        {
            case null when javaPackagePath == null && SulfurLauncherCommandService.TryHandleStartupArgs(args):
                return false;
#if WINDOWS
            case null when javaPackagePath == null && WindowsJumpListService.TryForwardToRunningInstance(args):
                return false;
#endif
            case null:
#if WINDOWS
                WindowsJumpListService.StartCommandServer();
#endif
                break;
        }

#if WINDOWS

        WindowsJumpListService.SetAppUserModelId();
#endif

        if (packagePath == null)
            SulfurLauncherCommandService.StartCommandServer();

        Logger.Info(string.Format(LogLanguageManager.Instance.desktop_primaryInstance_starting.CurrentValue(), args.Length));
        var versionInfo = AppVersionService.Instance.Version;
        Initializer.Program("SulfurLauncher", "cc.cangcang.sulfurlauncher", versionInfo.VersionTitle);

        Logger.Info(LogLanguageManager.Instance.desktop_primaryInstance_mainEntry.CurrentValue());

#if WINDOWS || LINUX
        AppSetup.RegisterBedrockLauncher();
#endif

        LogOperatingSystem();
        _ = RunDeferredMaintenanceAsync();
        return true;
    }

    private static async Task RunDeferredMaintenanceAsync()
    {
        // Keep shell integration maintenance off the critical path to the first window.
        await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        await ProtocolRegistration.TryRegisterLinuxOnStartupAsync().ConfigureAwait(false);
        await SulfurLauncherCommandRegistration.RegisterAsync().ConfigureAwait(false);
#if WINDOWS
        await Task.Run(() =>
        {
            WindowsBedrockFileAssociationService.Register();
            WindowsJavaFileAssociationService.Register();
        }).ConfigureAwait(false);
#endif
    }

    private static void LogOperatingSystem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Logger.Info(LogLanguageManager.Instance.desktop_startup_osWindows.CurrentValue());
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Logger.Info(LogLanguageManager.Instance.desktop_startup_osLinux.CurrentValue());
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Logger.Info(LogLanguageManager.Instance.desktop_startup_osMacos.CurrentValue());
    }
}
