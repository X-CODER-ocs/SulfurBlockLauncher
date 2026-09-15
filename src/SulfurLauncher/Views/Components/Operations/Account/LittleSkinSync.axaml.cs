using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SulfurLauncher.Core.Module.LittleSkin;
using SulfurLauncher.Core.Module.SkinLibrary;
using SulfurLauncher.Localization;
using TioUi.Common;
using TioUi.Common.Interfaces;
using TioUi.Controls;

namespace SulfurLauncher.Views.Components.Operations.Account;

public partial class LittleSkinSync : UserControl
{
    public LittleSkinSync()
    {
        InitializeComponent();
    }

    /// <summary>弹出 LittleSkin OAuth 同步对话框，完成后返回导入的皮肤数量（取消返回 null）。</summary>
    public static async Task<int?> ShowAndSync(string? hostId)
    {
        var options = new OverlayDialogOptions
        {
            Mode = DialogMode.None,
            Buttons = DialogButton.None,
            CanLightDismiss = false,
            CanDragMove = true,
            IsCloseButtonVisible = false,
            CanResize = false,
            VerticalAnchor = VerticalPosition.Top,
            VerticalOffset = 110
        };
        return await OverlayDialog.ShowCustomAsync<LittleSkinSync, LittleSkinSyncViewModel, int?>(
            new LittleSkinSyncViewModel(), hostId, options);
    }
}

public partial class LittleSkinSyncViewModel : ObservableObject, IDialogContext
{
    private readonly CancellationTokenSource _cts = new();
    private string _verificationUrl = string.Empty;

    public LittleSkinSyncViewModel()
    {
        OpenBrowserCommand = new RelayCommand(OpenBrowser, () => CanOpenBrowser);
        CancelCommand = new RelayCommand(Cancel);
        DoneCommand = new RelayCommand(Done, () => CanDone);

        _ = RunAsync();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpenBrowser))]
    public partial string UserCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    [NotifyPropertyChangedFor(nameof(CancelVisibility))]
    public partial bool IsBusy { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDone))]
    [NotifyPropertyChangedFor(nameof(DoneVisibility))]
    public partial bool IsComplete { get; set; }

    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    public ICommand OpenBrowserCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DoneCommand { get; }

    public bool CanOpenBrowser => !string.IsNullOrWhiteSpace(UserCode) && IsBusy;
    public bool CanCancel => IsBusy;
    public Avalonia.Controls.Visibility CancelVisibility =>
        IsBusy ? Avalonia.Controls.Visibility.Visible : Avalonia.Controls.Visibility.Collapsed;
    public bool CanDone => IsComplete;
    public Avalonia.Controls.Visibility DoneVisibility =>
        IsComplete ? Avalonia.Controls.Visibility.Visible : Avalonia.Controls.Visibility.Collapsed;

    /// <summary>在保存的导入发生前被覆盖；对话框结果显示导入的皮肤数量。</summary>
    public int ImportedCount { get; private set; }

    public void Close()
    {
        RequestClose?.Invoke(this, null);
    }

    public event EventHandler<object?>? RequestClose;

    private async Task RunAsync()
    {
        try
        {
            var clientId = LittleSkinSettings.ClientId;
            if (string.IsNullOrWhiteSpace(clientId))
            {
                ProgressText = CommonLanguageManager.Instance.littleskin_errorClientId.CurrentValue();
                IsBusy = false;
                return;
            }

            StatusText = CommonLanguageManager.Instance.littleskin_requestingCode.CurrentValue();
            ProgressText = string.Empty;

            var code = await LittleSkinOAuthService.Instance.RequestDeviceCodeAsync(
                LittleSkinSettings.Scopes, _cts.Token);

            UserCode = code.UserCode;
            _verificationUrl = !string.IsNullOrWhiteSpace(code.VerificationUriComplete)
                ? code.VerificationUriComplete
                : code.VerificationUri;
            StatusText = CommonLanguageManager.Instance.littleskin_waitingAuth.CurrentValue();
            OpenBrowser();

            var poll = await LittleSkinOAuthService.Instance.PollForTokenAsync(code, _cts.Token);
            if (!poll.Succeeded)
            {
                ProgressText = poll.ErrorMessage ?? CommonLanguageManager.Instance.littleskin_authFailed.CurrentValue();
                IsBusy = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(poll.AccessToken))
            {
                ProgressText = CommonLanguageManager.Instance.littleskin_authFailed.CurrentValue();
                IsBusy = false;
                return;
            }

            StatusText = CommonLanguageManager.Instance.littleskin_fetchingSkins.CurrentValue();
            var closet = await LittleSkinClosetService.Instance.ListClosetAsync(poll.AccessToken, _cts.Token);
            ImportedCount = await ImportSkinsAsync(closet);
            ProgressText = string.Format(
                CommonLanguageManager.Instance.littleskin_importedFormat.CurrentValue(), ImportedCount);
            StatusText = string.Empty;
            IsBusy = false;
            IsComplete = true;
        }
        catch (OperationCanceledException)
        {
            ProgressText = CommonLanguageManager.Instance.littleskin_cancelled.CurrentValue();
            IsBusy = false;
        }
        catch (Exception ex)
        {
            ProgressText = CommonLanguageManager.Instance.littleskin_error.CurrentValue() + ex.Message;
            IsBusy = false;
        }
    }

    private async Task<int> ImportSkinsAsync(List<LittleSkinSkinItem> closet)
    {
        if (closet.Count == 0)
            return 0;

        var imported = 0;
        var index = 0;
        foreach (var item in closet)
        {
            index++;
            ProgressText = string.Format(
                CommonLanguageManager.Instance.littleskin_downloadingFormat.CurrentValue(), index, closet.Count);
            var bytes = await LittleSkinClosetService.Instance.DownloadTextureAsync(item.TextureHash, _cts.Token);
            if (bytes is { Length: > 0 } &&
                SkinLibraryService.Instance.Import(bytes, item.Model) != null)
            {
                imported++;
            }
        }

        return imported;
    }

    private void OpenBrowser()
    {
        if (string.IsNullOrWhiteSpace(_verificationUrl))
            return;
        try
        {
            Process.Start(new ProcessStartInfo(_verificationUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ProgressText = CommonLanguageManager.Instance.littleskin_openBrowserFailed.CurrentValue() + ex.Message;
        }
    }

    private void Cancel()
    {
        _cts.Cancel();
        RequestClose?.Invoke(this, null);
    }

    private void Done()
    {
        RequestClose?.Invoke(this, ImportedCount);
    }
}