using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using SulfurLauncher.Core.Classes.Entries;
using SulfurLauncher.Core.Const;
using SulfurLauncher.Core.Module.AggregatedSearch;
using SulfurLauncher.Module;
using Tio.Avalonia.Standard.Modules.Extensions;
using TioUi.Common.Extensions;
using TioUi.Controls;

namespace SulfurLauncher.Views.Components;

public partial class AggregatedSearchDialog : UserControl
{
    public AggregatedSearchDialog()
    {
        InitializeComponent();
        DragMove.PointerPressed += (s, e) =>
        {
            var a = (s! as Control)!.GetTopLevel() as CustomDialogWindow;
            a?.BeginMoveDrag(e);
        };

        TemplateApplied += (s, e) =>
        {
            var a = (s! as Control)!.GetTopLevel() as CustomDialogWindow;
            a.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) a.Close();
            };
            a.Loaded += (_, e) =>
            {
                Data.UiProperty.AggregatedSearchResults.Clear();
                Data.UiProperty.AggregatedSearchResults.AddRange(
                    Searcher.Search(
                        Data.UiProperty.AggregatedSearchQuery,
                        Data.UiProperty.AggregatedSelectedType.EnumFlag));

                SearchBox.Focus();
                SearchBox.SelectAll();
            };
        };
    }

    private void Button_OnClick(object? s, RoutedEventArgs e)
    {
        var a = (s! as Control)!.GetTopLevel() as CustomDialogWindow;
        a?.Close();
    }

    private void SelectingItemsControl_OnSelectionChanged(object? s, SelectionChangedEventArgs e)
    {
        if (ListBox.SelectedItem is not AggregatedSearchEntry entry) return;
        var window = (DataContext as AggregatedSearchDialogViewModel).Window;

        var a = (s! as Control)!.GetTopLevel() as CustomDialogWindow;
        a?.Close();

        AggregatedSearchHandler.HandleAsync(entry, window);
    }
}

public class AggregatedSearchDialogViewModel : ObservableObject
{
    public readonly TioWindow Window;

    public AggregatedSearchDialogViewModel(TioWindow window)
    {
        Window = window;
    }

    public Data Data => Data.Instance;
}