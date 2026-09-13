using System.Collections.ObjectModel;
using System.Numerics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteSkinViewer3D.Shared.Enums;
using SulfurLauncher.Core.Const;
using SulfurLauncher.Core.Minecraft.Classes;
using SulfurLauncher.Core.Module.AggregatedSearch;
using SulfurLauncher.Core.Module.Initialize;
using SulfurLauncher.Core.Module.SkinLibrary;
using SulfurLauncher.Localization;
using Tio.Avalonia.Standard.Tab.Entries;
using Tio.Avalonia.Standard.Tab.Interface;
using TioUi.Common;
using TioUi.Common.Extensions;
using TioUi.Controls;
using Pointer = LiteSkinViewer3D.Shared.Enums.PointerType;

namespace SulfurLauncher.Views.Pages;

[AggregatedSearchPage("pages_skinLibrary", "pages_skinLibraryPath", "SkinLibrary")]
public partial class SkinLibraryPage : UserControl, ITioTabPage
{
    private readonly SkinLibraryPageViewModel _viewModel;

    public SkinLibraryPage()
    {
        InitializeComponent();
        _viewModel = new SkinLibraryPageViewModel();
        DataContext = _viewModel;
    }

    public PageInfo PageInfo { get; init; } = new()
    {
        Title = CommonLanguageManager.Instance.pages_skinLibrary.CurrentValue(),
        IconGlyph = "\ue63f",
        IconFont = IconResources.FontFamilyName
    };

    public TabEntry HostTab { get; set; }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _viewModel.AttachTopLevel(TopLevel.GetTopLevel(this));
        SkinViewer.PointerMoved += OnPointerMoved;
        SkinViewer.PointerPressed += OnPointerPressed;
        SkinViewer.PointerReleased += OnPointerReleased;
        SkinViewer.PointerWheelChanged += OnPointerWheelChanged;
    }

    public void OnClose()
    {
        if (DataContext is IDisposable disposable) disposable.Dispose();
        _viewModel.DetachTopLevel();
        DataContext = null;
    }

    private float _initialY;
    private Pointer _pressedPointer;

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

public partial class SkinLibraryPageViewModel : ObservableObject, IDisposable
{
    private TopLevel? _topLevel;
    private bool _isLoaded;

    public ObservableCollection<AccountItemViewModel> Accounts { get; } = [];

    public ObservableCollection<SkinItemViewModel> Skins { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    [NotifyPropertyChangedFor(nameof(PreviewPath))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial SkinItemViewModel? Selected { get; set; }

    private AccountItemViewModel? _selectedAccount;
    public AccountItemViewModel? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (SetProperty(ref _selectedAccount, value)) OnPropertyChanged(nameof(CanApply));
        }
    }

    public string Title => CommonLanguageManager.Instance.pages_skinLibrary.CurrentValue();
    public string SelectAccountHint => SettingsLanguageManager.Instance.account_skinLibrarySelectAccountHint.CurrentValue();
    public string SelectAccountPlaceholder => SettingsLanguageManager.Instance.account_skinLibrarySelectAccountPlaceholder.CurrentValue();
    public string ImportText => SettingsLanguageManager.Instance.account_skinLibraryImport.CurrentValue();
    public string DeleteText => SettingsLanguageManager.Instance.account_skinLibraryDelete.CurrentValue();
    public string ApplyText => SettingsLanguageManager.Instance.account_skinLibraryApplyAccount.CurrentValue();
    public string EmptyText => SettingsLanguageManager.Instance.account_skinLibraryEmpty.CurrentValue();
    public string SelectHint => SettingsLanguageManager.Instance.account_skinLibrarySelectHint.CurrentValue();

    public string? PreviewPath => Selected?.Item.FilePath;
    public bool HasSelection => Selected is not null;
    public bool HasSkins => Skins.Count > 0;
    public bool CanApply => Selected is not null && SelectedAccount is not null;

    public ICommand ImportCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ApplyCommand { get; }

    public event Action<(NotificationType Type, string Message)>? Notify;

    public SkinLibraryPageViewModel()
    {
        ImportCommand = new AsyncRelayCommand(ImportAsync);
        DeleteCommand = new RelayCommand(Delete);
        ApplyCommand = new RelayCommand(Apply, () => CanApply);
        PopulateAccounts();
        Reload();
    }

    partial void OnSelectedChanged(SkinItemViewModel? value)
    {
        (DeleteCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ApplyCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    public void AttachTopLevel(TopLevel? topLevel)
    {
        _topLevel = topLevel;
        // Ensure each attach subscribes exactly once.
        Notify -= HandleNotify;
        if (topLevel is not null) Notify += HandleNotify;
        _isLoaded = true;
    }

    private void HandleNotify((NotificationType Type, string Message) tuple) =>
        _topLevel?.Notice(tuple.Message, tuple.Type);

    public void DetachTopLevel()
    {
        _isLoaded = false;
        _topLevel = null;
    }

    private void PopulateAccounts()
    {
        Accounts.Clear();
        foreach (var account in Data.ConfigEntry.MinecraftAccounts)
            Accounts.Add(new AccountItemViewModel(account));
        var currentName = Data.ConfigEntry.UsingMinecraftMinecraftAccount?.Name;
        SelectedAccount = Accounts.FirstOrDefault(a => a.Account.Name == currentName)
                          ?? Accounts.FirstOrDefault();
    }

    private void Reload()
    {
        Skins.Clear();
        var index = 0;
        foreach (var item in SkinLibraryService.Instance.GetAll())
            Skins.Add(new SkinItemViewModel(item, ++index));
        OnPropertyChanged(nameof(HasSkins));
        OnPropertyChanged(nameof(CanApply));
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
        if (!CanApply || SelectedAccount is not { } accountItem) return;
        SkinLibraryService.Instance.ApplyToAccount(accountItem.Account, Selected!.Item);
        ConfigSaver.SaveConfig();
        if (_topLevel is { } topLevel)
            topLevel.Notice(string.Format(
                SettingsLanguageManager.Instance.account_skinLibraryApplied.CurrentValue(),
                accountItem.Account.Name), NotificationType.Success);
    }

    private static SkinModel DetectModel(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        return name.Contains("slim", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("alex", StringComparison.OrdinalIgnoreCase)
            ? SkinModel.Slim
            : SkinModel.Classic;
    }

    public void Dispose()
    {
        DetachTopLevel();
    }
}

public sealed class AccountItemViewModel
{
    public AccountItemViewModel(MinecraftAccount account)
    {
        Account = account;
    }

    public MinecraftAccount Account { get; }

    public string DisplayName => Account.ShortDisplay;

    public override string ToString() => DisplayName;
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