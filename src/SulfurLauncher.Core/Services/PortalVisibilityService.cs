using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using SulfurLauncher.Core.Classes;
using SulfurLauncher.Core.Const;
using SulfurLauncher.Core.Module.Initialize;

namespace SulfurLauncher.Core.Services;

public static class SulfurLauncherVisibilityService
{
    private static int _runningGames;
    private static SulfurLauncherVisibleMode _appliedMode = SulfurLauncherVisibleMode.NoOperation;
    private static readonly List<Window> _hiddenWindows = [];
    private static readonly List<(Window Window, WindowState State)> _minimizedWindows = [];

    public static void OnGameStarted(SulfurLauncherVisibleMode mode)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _runningGames++;
            if (_runningGames > 1)
                return;

            _appliedMode = mode;
            switch (_appliedMode)
            {
                case SulfurLauncherVisibleMode.QuitAfterLaunch:
                    Shutdown();
                    break;
                case SulfurLauncherVisibleMode.HiddenAfterLaunchAndReopen:
                    HideAllWindows();
                    break;
                case SulfurLauncherVisibleMode.MinimizedAfterLaunch:
                case SulfurLauncherVisibleMode.MinimizedAfterLaunchAndRestore:
                    MinimizeAllWindows();
                    break;
            }
        });
    }

    public static void OnGameExited()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _runningGames = Math.Max(0, _runningGames - 1);
            if (_runningGames > 0)
                return;

            switch (_appliedMode)
            {
                case SulfurLauncherVisibleMode.HiddenAfterLaunchAndReopen:
                    ShowHiddenWindows();
                    break;
                case SulfurLauncherVisibleMode.MinimizedAfterLaunchAndRestore:
                    RestoreMinimizedWindows();
                    break;
                case SulfurLauncherVisibleMode.MinimizedAfterLaunch:
                    _minimizedWindows.Clear();
                    break;
            }

            _appliedMode = SulfurLauncherVisibleMode.NoOperation;
        });
    }

    private static IEnumerable<Window> GetWindows()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows
            : [];
    }

    private static void HideAllWindows()
    {
        _hiddenWindows.Clear();
        foreach (var window in GetWindows())
        {
            if (!window.IsVisible)
                continue;
            _hiddenWindows.Add(window);
            window.Hide();
        }
    }

    private static void ShowHiddenWindows()
    {
        foreach (var window in _hiddenWindows)
            try
            {
                window.Show();
                window.Activate();
            }
            catch
            {
            }

        _hiddenWindows.Clear();
    }

    private static void MinimizeAllWindows()
    {
        _minimizedWindows.Clear();
        foreach (var window in GetWindows())
        {
            if (!window.IsVisible || window.WindowState == WindowState.Minimized)
                continue;
            _minimizedWindows.Add((window, window.WindowState));
            window.WindowState = WindowState.Minimized;
        }
    }

    private static void RestoreMinimizedWindows()
    {
        foreach (var (window, state) in _minimizedWindows)
            try
            {
                window.WindowState = state;
                window.Activate();
            }
            catch
            {
            }

        _minimizedWindows.Clear();
    }

    private static void Shutdown()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ConfigSaver.FlushConfig();
            desktop.Shutdown();
        }
    }
}