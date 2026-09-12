using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SulfurLauncher.Core.Const;
using SulfurLauncher.Core.Module.Multiplayer;
using SulfurLauncher.Localization;
using Tio.Avalonia.Standard.Modules.DiskIO;
using Tio.Avalonia.Standard.Modules.Tasks;
using Tio.Avalonia.Standard.Tab.Gateway;

namespace SulfurLauncher.Views.Pages.ToolsPages.MultiplayerPages;

public partial class PlayitMultiplayerPage : UserControl
{
    private readonly PlayitMultiplayerViewModel _viewModel;

    public PlayitMultiplayerPage()
    {
        InitializeComponent();
        _viewModel = new PlayitMultiplayerViewModel();
        DataContext = _viewModel;
    }

    public PlayitMultiplayerViewModel ViewModel => _viewModel;

    private async void CopyTunnelAddress_OnClick(object? sender, RoutedEventArgs e)
    {
        var address = sender is Button { CommandParameter: string s } ? s : null;
        if (string.IsNullOrWhiteSpace(address)) return;
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        await clipboard.SetTextAsync(address);
        TopLevel.GetTopLevel(this)?.Notice(
            CommonLanguageManager.Instance.multiplayer_playitAddressCopied.CurrentValue(), NotificationType.Success);
    }

    private void OpenDashboard_OnClick(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(PlayitMultiplayerService.DashboardUrl) { UseShellExecute = true });
    }
}

public sealed partial class PlayitTunnelViewModel : ObservableObject
{
    public required string Kind { get; init; }
    public required int LocalPort { get; init; }
    [ObservableProperty] public partial string Address { get; set; } = string.Empty;

    public string KindLabel => Kind == "bedrock"
        ? CommonLanguageManager.Instance.multiplayer_playitTunnelKindBedrock.CurrentValue()
        : CommonLanguageManager.Instance.multiplayer_playitTunnelKindJava.CurrentValue();

    public string LocalPortText =>
        $"{CommonLanguageManager.Instance.multiplayer_playitTunnelLocalPort.CurrentValue()}: {LocalPort}";
}

public partial class PlayitMultiplayerViewModel : ObservableObject, IAsyncDisposable, IMultiplayerPageLifecycle
{
    private readonly PlayitMultiplayerService _service = PlayitMultiplayerService.Instance;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;
    private bool _isActive;

    public PlayitMultiplayerViewModel()
    {
        _service.StateChanged += OnServiceStateChanged;
        RefreshFromService();
    }

    public ObservableCollection<PlayitTunnelViewModel> Tunnels { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDownloading))]
    [NotifyPropertyChangedFor(nameof(DownloadProgress))]
    [NotifyPropertyChangedFor(nameof(BinaryInstalled))]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(ErrorMessage))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusIcon))]
    [NotifyPropertyChangedFor(nameof(IsAgentConnected))]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanRefresh))]
    [NotifyPropertyChangedFor(nameof(ShowTunnels))]
    [NotifyPropertyChangedFor(nameof(ShowNoTunnels))]
    [NotifyPropertyChangedFor(nameof(HasOfflineReason))]
    [NotifyPropertyChangedFor(nameof(OfflineReasonSummary))]
    public partial PlayitState? State { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(RefreshCanStart))]
    public partial string SecretKey { get; set; } = Data.ConfigEntry.PlayitSecretKey ?? string.Empty;

    public bool ShowUnsupported => !PlayitMultiplayerService.IsCurrentPlatformSupported;
    public bool ShowDownloadCard => !_service.IsInstalled();
    public bool IsDownloading => State?.Status == PlayitStatus.Downloading;
    public int DownloadProgress => State?.DownloadProgress ?? 0;
    public bool BinaryInstalled =>
        State is { } s && s.Status is PlayitStatus.Running or PlayitStatus.Stopped or PlayitStatus.Starting or PlayitStatus.Error;
    public bool HasError => State?.Status == PlayitStatus.Error;
    public string ErrorMessage => State?.ErrorMessage ?? string.Empty;
    public bool IsAgentConnected => State?.IsAgentConnected ?? false;
    public bool CanStart => !IsBusy && !string.IsNullOrWhiteSpace(SecretKey);
    public bool CanRefresh => IsAgentConnected && !IsBusy;
    public bool RefreshCanStart => !string.IsNullOrWhiteSpace(SecretKey);
    public bool ShowTunnels => Tunnels.Count > 0 || (State?.IsRefreshingTunnels ?? false);
    public bool ShowNoTunnels => Tunnels.Count == 0 && !(State?.IsRefreshingTunnels ?? false) && IsAgentConnected;
    public bool HasOfflineReason => !string.IsNullOrWhiteSpace(OfflineReasonSummary);

    public string OfflineReasonSummary
    {
        get
        {
            var reasons = State?.Tunnels.Where(t => !string.IsNullOrEmpty(t.OfflineReason))
                .Select(t => t.OfflineReason).Distinct().ToList();
            return reasons is { Count: > 0 } ? string.Join("；", reasons) : string.Empty;
        }
    }

    public string StatusText => State?.Status switch
    {
        PlayitStatus.Running => CommonLanguageManager.Instance.multiplayer_playitStatusRunning.CurrentValue(),
        PlayitStatus.Starting => CommonLanguageManager.Instance.multiplayer_playitStatusStarting.CurrentValue(),
        PlayitStatus.Downloading => CommonLanguageManager.Instance.multiplayer_playitDownloading.CurrentValue(),
        PlayitStatus.Error => CommonLanguageManager.Instance.multiplayer_playitStatusError.CurrentValue(),
        _ => CommonLanguageManager.Instance.multiplayer_playitStatusStopped.CurrentValue()
    };

    public string StatusIcon => State?.Status switch
    {
        PlayitStatus.Running => "\ue61a",
        PlayitStatus.Error => "\ue61f",
        _ => "\ue637"
    };

    public void Activate() => _isActive = true;

    public void Deactivate() => _isActive = false;

    partial void OnSecretKeyChanged(string value)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(RefreshCanStart));
        Data.ConfigEntry.PlayitSecretKey = value?.Trim();
    }

    private void OnServiceStateChanged(object? sender, EventArgs e)
    {
        if (!_isActive) return;
        Dispatcher.UIThread.Post(RefreshFromService);
    }

    private void RefreshFromService()
    {
        State = _service.State;
        OnPropertyChanged(nameof(BinaryInstalled));
        OnPropertyChanged(nameof(ShowDownloadCard));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanRefresh));
    }

    [RelayCommand]
    private async Task DownloadKernelAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        var taskName = CommonLanguageManager.Instance.multiplayer_playitDownloading.CurrentValue();
        var task = TaskManager.Instance.CreateTask(new TaskOptions
        {
            Name = taskName,
            Description = CommonLanguageManager.Instance.multiplayer_preparingDownload.CurrentValue(),
            Progress = 0,
            Actions =
            [
                new TaskActionDefinition
                {
                    Name = CommonLanguageManager.Instance.multiplayer_cancelDownload.CurrentValue(),
                    Description = CommonLanguageManager.Instance.multiplayer_cancelComponentDownload.CurrentValue(),
                    IconKey = "Cancel",
                    ExecuteAsync = (managedTask, _) =>
                    {
                        managedTask.RequestCancellation();
                        return Task.CompletedTask;
                    },
                    CanExecute = managedTask => managedTask.CanBeCancelled,
                    IsVisible = managedTask => !managedTask.IsTerminal
                }
            ]
        }, async context =>
        {
            await _service.EnsureInstalledAsync(context.CancellationToken, fraction =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (context.Task.IsTerminal || context.CancellationToken.IsCancellationRequested) return;
                    context.ReportProgress(Math.Clamp(fraction, 0, 1));
                    context.SetDescription(string.Format(
                        CommonLanguageManager.Instance.multiplayer_playitDownloadProgress.CurrentValue(),
                        (int)(fraction * 100)));
                });
            });
            context.ReportProgress(1);
            context.SetDescription(CommonLanguageManager.Instance.multiplayer_componentDownloaded.CurrentValue());
        });
        task.Start();
        _ = ObserveInstallationAsync(task);
    }

    private async Task ObserveInstallationAsync(ManagedTask task)
    {
        try
        {
            await task.Completion;
        }
        catch (Exception exception)
        {
            Logger.Warning($"[PlayIt] Installation task failed: {exception}");
        }
        finally
        {
            IsBusy = false;
            RefreshFromService();
            OnPropertyChanged(nameof(ShowDownloadCard));
            OnPropertyChanged(nameof(CanStart));
            await Task.Delay(TimeSpan.FromSeconds(3));
            TaskManager.Instance.RemoveTerminalTask(task);
        }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await _service.StartAsync(SecretKey, _lifetime.Token);
            await RefreshTunnelsCoreAsync();
        }
        catch (Exception ex)
        {
            Logger.Error($"[PlayIt] Start failed: {ex}");
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(CanRefresh));
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        IsBusy = true;
        try
        {
            _service.Stop();
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanStart));
        }
        await Task.CompletedTask;
    }

    [RelayCommand]
    private Task RefreshTunnelsAsync() => RefreshTunnelsCoreAsync();

    private async Task RefreshTunnelsCoreAsync()
    {
        var state = State;
        if (state is null) return;
        state.IsRefreshingTunnels = true;
        OnPropertyChanged(nameof(ShowNoTunnels));
        try
        {
            var tunnels = await _service.ListTunnelsAsync(SecretKey, _lifetime.Token);
            Tunnels.Clear();
            foreach (var t in tunnels)
            {
                Tunnels.Add(new PlayitTunnelViewModel
                {
                    Kind = t.Kind,
                    LocalPort = t.LocalPort,
                    Address = t.PublicAddress ?? string.Empty
                });
            }
            state.LastTunnelRefresh = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            Logger.Warning($"[PlayIt] List tunnels failed: {ex}");
            state.Status = PlayitStatus.Error;
            state.ErrorMessage = CommonLanguageManager.Instance.multiplayer_playitInvalidSecret.CurrentValue();
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ErrorMessage));
        }
        finally
        {
            state.IsRefreshingTunnels = false;
            OnPropertyChanged(nameof(ShowTunnels));
            OnPropertyChanged(nameof(ShowNoTunnels));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _service.StateChanged -= OnServiceStateChanged;
        _lifetime.Dispose();
    }
}