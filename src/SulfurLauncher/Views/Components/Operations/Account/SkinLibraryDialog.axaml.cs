using System.Numerics;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteSkinViewer3D.Shared.Enums;
using SulfurLauncher.Core.Minecraft.Classes;
using SulfurLauncher.Core.Module.Initialize;
using SulfurLauncher.Core.Module.SkinLibrary;
using SulfurLauncher.Localization;
using Tio.Avalonia.Standard.Tab.Gateway;
using TioUi.Common;
using TioUi.Common.Extensions;
using TioUi.Common.Interfaces;
using TioUi.Controls;
using Pointer = LiteSkinViewer3D.Shared.Enums.PointerType;

namespace SulfurLauncher.Views.Components.Operations.Account;

public partial class SkinLibraryDialog : UserControl
{
    private float _initialY;
    private Pointer _pressedPointer;

    public SkinLibraryDialog(SkinLibraryDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        SkinViewer.PointerMoved += OnPointerMoved;
        SkinViewer.PointerPressed += OnPointerPressed;
        SkinViewer.PointerReleased += OnPointerReleased;
        SkinViewer.PointerWheelChanged += OnPointerWheelChanged;
        SkinViewer.RenderMode = SkinRenderMode.MSAA;
        SkinViewer.IsTopLayer3D = true;
    }

    private void OnPointerMoved(object? s, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        var type = Pointer.None;
        var prop = e.GetCurrentPoint(this).Properties;
        if (prop.IsLeftButtonPressed) type = Pointer.PointerLeft;
        else if (prop.IsRightButtonPressed) type = Pointer.PointerRight;
        if (type == Pointer.PointerRight)
        {
            SkinViewer.UpdatePointerMoved(type, new Vector2((float)pos.X, (float)pos.Y));
            return;
        }

        var point = new Vector2((float)pos.X * 8f, (float)pos.Y * 8f);
        SkinViewer.UpdatePointerMoved(type, point);
    }

    private void OnPointerPressed(object? s, PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(this);
        _initialY = (float)pos.Y;
        var prop = e.GetCurrentPoint(this).Properties;
        var type = Pointer.None;
        if (prop.IsLeftButtonPressed) type = Pointer.PointerLeft;
        else if (prop.IsRightButtonPressed) type = Pointer.PointerRight;
        _pressedPointer = type;
        SkinViewer.UpdatePointerPressed(type, new Vector2((float)pos.X, _initialY));
    }

    private void OnPointerReleased(object? s, PointerReleasedEventArgs e)
    {
        var pos = e.GetPosition(this);
        SkinViewer.UpdatePointerReleased(_pressedPointer, new Vector2((float)pos.X, (float)pos.Y));
        _pressedPointer = Pointer.None;
    }

    private void OnPointerWheelChanged(object? s, PointerWheelEventArgs e)
    {
        SkinViewer.UpdatePointerWheelChanged(e.Delta.Y > 0);
    }
}

public static class SkinLibraryDialogLauncher
{
    public static async Task<SkinLibraryItem?> Show(Control owner, MinecraftAccount account,
        Action? changed = null)
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        var options = new OverlayDialogOptions
        {
            Mode = DialogMode.None,
            Buttons = DialogButton.None,
            CanLightDismiss = false,
            CanDragMove = true,
            IsCloseButtonVisible = false,
            CanResize = false,
            VerticalAnchor = VerticalPosition.Center
        };

        var viewModel = new SkinLibraryDialogViewModel(account, topLevel);
        var dialog = new SkinLibraryDialog(viewModel);
        viewModel.Notify += tuple => topLevel?.Notice(tuple.Message, tuple.Type);

        var applied = await OverlayDialog.ShowCustomAsync<SkinLibraryItem?>(
            dialog, viewModel, owner.TryGetHostId(), options);

        if (applied is not null)
        {
            ConfigSaver.SaveConfig();
            changed?.Invoke();
        }

        return applied;
    }
}

public sealed class SkinItemViewModel : ObservableObject
{
    private Bitmap? _thumbnail;

    public SkinItemViewModel(SkinLibraryItem item, int index)
    {
        Item = item;
        DisplayName = $"Skin {index}";
    }

    public SkinLibraryItem Item { get; }

    public string DisplayName { get; }

    public string ModelText => Item.SkinModel == SkinModel.Slim
        ? SettingsLanguageManager.Instance.account_skinLibraryModelSlim.CurrentValue()
        : SettingsLanguageManager.Instance.account_skinLibraryModelClassic.CurrentValue();

    public Bitmap? Thumbnail => _thumbnail ??= SkinLibraryService.Instance.GetHeadImage(Item);
}

public partial class SkinLibraryDialogViewModel : ObservableObject, IDialogContext
{
    private readonly MinecraftAccount _account;
    private readonly TopLevel? _topLevel;

    public ObservableCollection<SkinItemViewModel> Skins { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewPath))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial SkinItemViewModel? Selected { get; set; }

    public string Title => SettingsLanguageManager.Instance.account_skinLibrary.CurrentValue();

    public string ImportText => SettingsLanguageManager.Instance.account_skinLibraryImport.CurrentValue();

    public string DeleteText => SettingsLanguageManager.Instance.account_skinLibraryDelete.CurrentValue();

    public string ApplyText => SettingsLanguageManager.Instance.account_skinLibraryApply.CurrentValue();

    public string CloseText => SettingsLanguageManager.Instance.account_skinLibraryClose.CurrentValue();

    public string EmptyText => SettingsLanguageManager.Instance.account_skinLibraryEmpty.CurrentValue();

    public string SelectHint => SettingsLanguageManager.Instance.account_skinLibrarySelectHint.CurrentValue();

    public string? PreviewPath => Selected?.Item.FilePath;

    public bool HasSelection => Selected is not null;

    public bool HasSkins => Skins.Count > 0;

    public ICommand ImportCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand CloseCommand { get; }

    public event EventHandler<object?>? RequestClose;
    public event Action<(NotificationType Type, string Message)>? Notify;

    public SkinLibraryDialogViewModel(MinecraftAccount account, TopLevel? topLevel)
    {
        _account = account;
        _topLevel = topLevel;
        ImportCommand = new AsyncRelayCommand(ImportAsync);
        DeleteCommand = new RelayCommand(Delete);
        ApplyCommand = new RelayCommand(Apply);
        CloseCommand = new RelayCommand(Close);
        Reload();
    }

    public void Close()
    {
        RequestClose?.Invoke(this, null);
    }

    partial void OnSelectedChanged(SkinItemViewModel? value)
    {
        (DeleteCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ApplyCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void Reload()
    {
        Skins.Clear();
        var index = 0;
        foreach (var item in SkinLibraryService.Instance.GetAll())
            Skins.Add(new SkinItemViewModel(item, ++index));
        OnPropertyChanged(nameof(HasSkins));
    }

    private async Task ImportAsync()
    {
        if (_topLevel is not { StorageProvider: { } storage }) return;

        var file = (await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = SettingsLanguageManager.Instance.account_skinLibraryImport.CurrentValue(),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("PNG") { Patterns = ["*.png"] }
            ]
        })).FirstOrDefault();
        if (file is null) return;

        var localPath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
        {
            Notify?.Invoke((NotificationType.Error,
                SettingsLanguageManager.Instance.account_skinLibraryImportFailed.CurrentValue()));
            return;
        }

        try
        {
            var bytes = File.ReadAllBytes(localPath);
            var (isValid, _, _) = SkinFileValidator.Validate(bytes);
            if (!isValid)
            {
                Notify?.Invoke((NotificationType.Warning,
                    SettingsLanguageManager.Instance.account_skinLibraryInvalidFile.CurrentValue()));
                return;
            }

            var model = DetectModel(localPath);
            var imported = SkinLibraryService.Instance.ImportFile(localPath, model);
            if (imported is null)
            {
                Notify?.Invoke((NotificationType.Warning,
                    SettingsLanguageManager.Instance.account_skinLibraryInvalidFile.CurrentValue()));
                return;
            }

            Reload();
            Selected = Skins.FirstOrDefault(item => item.Item.Id == imported.Id);
            Notify?.Invoke((NotificationType.Success,
                SettingsLanguageManager.Instance.account_skinLibraryImported.CurrentValue()));
        }
        catch (Exception exception)
        {
            Notify?.Invoke((NotificationType.Error, string.Format(
                SettingsLanguageManager.Instance.account_skinLibraryImportFailed.CurrentValue(),
                exception.Message)));
        }
    }

    private void Delete()
    {
        if (Selected is null) return;
        SkinLibraryService.Instance.Delete(Selected.Item);
        Reload();
        Selected = null;
        Notify?.Invoke((NotificationType.Success,
            SettingsLanguageManager.Instance.account_skinLibraryDeleted.CurrentValue()));
    }

    private void Apply()
    {
        if (Selected is null) return;
        SkinLibraryService.Instance.ApplyToAccount(_account, Selected.Item);
        RequestClose?.Invoke(this, Selected.Item);
    }

    private static SkinModel DetectModel(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        return name.Contains("slim", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("alex", StringComparison.OrdinalIgnoreCase)
            ? SkinModel.Slim
            : SkinModel.Classic;
    }
}