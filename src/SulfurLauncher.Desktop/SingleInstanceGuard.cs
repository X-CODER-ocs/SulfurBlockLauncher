using SulfurLauncher.Core.Module.Ipc;
using SulfurLauncher.Localization;
using Tio.Avalonia.Standard.Modules.DiskIO;

namespace SulfurLauncher.Desktop;

internal static class SingleInstanceGuard
{
    private const string MutexName = "cc.cangcang.sulfurlauncher.Singleton";

    private const int ForwardAttempts = 4;
    private static Mutex? _mutex;

    public static bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (createdNew)
        {
            Logger.Info(LogLanguageManager.Instance.desktop_singleInstance_acquired.CurrentValue());
            return true;
        }

        try
        {
            if (_mutex.WaitOne(0))
            {
                Logger.Warning(LogLanguageManager.Instance.desktop_singleInstance_abandoned.CurrentValue());
                return true;
            }
        }
        catch (AbandonedMutexException)
        {
            Logger.Warning(LogLanguageManager.Instance.desktop_singleInstance_abandoned.CurrentValue());
            return true;
        }

        Logger.Info(LogLanguageManager.Instance.desktop_singleInstance_runningDetected.CurrentValue());
        return false;
    }

    public static void HandleSecondaryLaunch(string[] args)
    {
        if (PackagePathResolver.TryGetBedrockPackagePath(args, out var bedrockPath))
        {
            ForwardCommand(new SulfurLauncherCommand { Kind = SulfurLauncherCommandKind.DownloadModpack, Source = bedrockPath });
            return;
        }

        if (PackagePathResolver.TryGetJavaPackagePath(args, out var javaPath))
        {
            ForwardCommand(new SulfurLauncherCommand { Kind = SulfurLauncherCommandKind.DownloadModpack, Source = javaPath });
            return;
        }

#if WINDOWS
        if (WindowsJumpListService.TryForwardToRunningInstance(args))
            return;
#endif

        switch (SulfurLauncherCommandParser.Parse(args, out var command, out var error))
        {
            case SulfurLauncherCliParseStatus.Help:
                SulfurLauncherCommandService.WriteConsole(SulfurLauncherCommandParser.GetHeadlessUsageText());
                return;
            case SulfurLauncherCliParseStatus.Error:
                SulfurLauncherCommandService.WriteConsole(
                    string.Format(CommonLanguageManager.Instance.desktop_commandService_argumentError.CurrentValue(), error, Environment.NewLine, Environment.NewLine, SulfurLauncherCommandParser.GetUsageText()));
                return;
            case SulfurLauncherCliParseStatus.Command when command is not null:
                ForwardCommand(command);
                return;
            case SulfurLauncherCliParseStatus.NotACommand:
            default:
                NotifyShowMainWindow();
                return;
        }
    }

    private static void ForwardCommand(SulfurLauncherCommand command)
    {
        if (SulfurLauncherCommandService.TryForwardToRunningInstance(command, ForwardAttempts))
        {
            SulfurLauncherCommandService.WriteConsole(CommonLanguageManager.Instance.desktop_commandService_forwarded.CurrentValue());
            Logger.Info(string.Format(LogLanguageManager.Instance.desktop_singleInstance_forwardedWithKind.CurrentValue(), command.Kind));
        }
        else
        {
            Logger.Warning(LogLanguageManager.Instance.desktop_singleInstance_forwardFailed.CurrentValue());
        }
    }

    private static void NotifyShowMainWindow()
    {
        var showCommand = new SulfurLauncherCommand { Kind = SulfurLauncherCommandKind.ShowMainWindow };
        if (SulfurLauncherCommandService.TryForwardToRunningInstance(showCommand, ForwardAttempts))
            Logger.Info(LogLanguageManager.Instance.desktop_singleInstance_notifyShowWindow.CurrentValue());
        else
            Logger.Warning(LogLanguageManager.Instance.desktop_singleInstance_notifyShowWindowFailed.CurrentValue());
    }
}