using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tio.Avalonia.Standard.Tab.Common;
using Tio.Avalonia.Standard.Tab.Interface;

namespace Tio.Avalonia.Standard.Tab.Entries;

public partial class TabEntry : ObservableObject
{
    private bool _isClosing;
    [ObservableProperty] private string _title;
    [ObservableProperty] private StreamGeometry? _icon;
    [ObservableProperty] private string? _iconGlyph;
    [ObservableProperty] private string? _iconFont;
    [ObservableProperty] private object _header;
    [ObservableProperty] private ITioTabPage _content;
    [ObservableProperty] private int _minWidth = 150;
    [ObservableProperty] private bool _isCloseable;
    [ObservableProperty] private bool _isIconVisible;
    [ObservableProperty] private bool _isDragging;
    [ObservableProperty] private Thickness _iconMargin = new(0,0,4,0);
    [ObservableProperty] private double _iconWidth = 16;
    [ObservableProperty] private double _iconHeight = 16;
    public TioTabWindowBase Window { get; set; }

    public bool HasFontIcon => !string.IsNullOrEmpty(IconGlyph);

    partial void OnIconGlyphChanged(string? value)
    {
        OnPropertyChanged(nameof(HasFontIcon));
    }

    public TabEntry(TioTabWindowBase window, ITioTabPage content, object? header = null, string? title = null,
        StreamGeometry? icon = null, bool? isCloseable = true, bool? isIconVisible = true)
    {
        Window = window;
        Title = title ?? content.PageInfo.Title;
        Icon = icon ?? content.PageInfo.Icon;
        IconGlyph = content.PageInfo.IconGlyph;
        IconFont = content.PageInfo.IconFont;
        Header = header ?? content.PageInfo.Header ?? Title;
        Content = content;
        IsCloseable = isCloseable ?? content.PageInfo.IsCloseable;
        IsIconVisible = isIconVisible ?? content.PageInfo.IsIconVisible;
        content.HostTab = this;
    }

    public async void Close()
    {
        if (!IsCloseable || _isClosing) return;
        _isClosing = true;
        try
        {
            if (!await Content.RequestCloseAsync()) return;
            var selected = Window.SelectedTab == this;
            Window.RemoveTab(this);
            Content.OnClose();
            ReleaseContent();
            if (!selected) return;
            var tabs = Window.Tabs;
            if (tabs.Count == 0 && Window.CreateLastTabFunc != null)
            {
                Window.CreateLastTabFunc();
            }

            if (tabs.Count > 0)
                Window.SelectTab(tabs.Last());
            else
                Window.SelectedTab = null; // 否则窗口会一直握着刚关闭的标签页
        }
        finally
        {
            _isClosing = false;
        }
    }

    /// <summary>
    /// Closes a tab without requesting confirmation from its content.
    /// Intended for operations that invalidate the content, such as deleting its backing instance.
    /// </summary>
    public void CloseImmediately()
    {
        if (!IsCloseable || _isClosing) return;

        _isClosing = true;
        try
        {
            var selected = Window.SelectedTab == this;
            Window.RemoveTab(this);
            Content.OnClose();
            ReleaseContent();
            if (!selected) return;

            var tabs = Window.Tabs;
            if (tabs.Count == 0 && Window.CreateLastTabFunc != null)
                Window.CreateLastTabFunc();

            if (tabs.Count > 0)
                Window.SelectTab(tabs.Last());
            else
                Window.SelectedTab = null;
        }
        finally
        {
            _isClosing = false;
        }
    }

    public void MoveTabForward()
    {
        var tabs = Window.Tabs;
        var currentIndex = tabs.IndexOf(this);
        if (currentIndex <= 0) return;
        Window.ReorderTab(currentIndex, currentIndex - 1);
    }

    public void MoveTabBackward()
    {
        var tabs = Window.Tabs;
        var currentIndex = tabs.IndexOf(this);
        if (currentIndex < 0 || currentIndex >= tabs.Count - 1) return;
        Window.ReorderTab(currentIndex, currentIndex + 1);
    }

    public void MoveTabToFront()
    {
        var tabs = Window.Tabs;
        var currentIndex = tabs.IndexOf(this);
        if (currentIndex <= 0) return;
        Window.ReorderTab(currentIndex, 0);
    }

    public void MoveTabToLast()
    {
        var tabs = Window.Tabs;
        var currentIndex = tabs.IndexOf(this);
        var lastIndex = tabs.Count - 1;
        if (currentIndex < 0 || currentIndex >= lastIndex) return;
        Window.ReorderTab(currentIndex, lastIndex);
    }

    public async void CloseOther()
    {
        var tabs = Window.Tabs.Where(tab => tab != this && tab.IsCloseable).ToList();
        foreach (var tab in tabs)
        {
            if (!await tab.Content.RequestCloseAsync()) return;
        }

        foreach (var tab in tabs)
        {
            Window.RemoveTab(tab);
            tab.Content.OnClose();
            tab.ReleaseContent();
        }
        Window.SelectTab(this);
    }

    /// <summary>
    /// 把页面从可视化树上摘下来，切断 ContentPresenter / 焦点管理器对已关闭页面的引用。
    /// </summary>
    internal void ReleaseContent()
    {
        if (Content is not Control control)
            return;

        // 焦点仍停留在被关闭的页面里时，TopLevel 的焦点管理器会一直持有它。
        if (control.IsKeyboardFocusWithin)
            TopLevel.GetTopLevel(control)?.Focus();

        DetachControl(control);
    }

    public void MoveTabToNewWindow(PixelPoint? screenPosition = null)
    {
        var oldWindow = Window;
        if (oldWindow == null)
            return;

        var oldSelected = oldWindow.SelectedTab == this;

        if (Content is Control contentControl)
        {
            DetachControl(contentControl);
        }

        Window.RemoveTab(this);

        if (oldSelected && oldWindow.Tabs.Count > 0)
        {
            oldWindow.SelectTab(oldWindow.Tabs.Last());
        }

        var window = Functions.CreateNewTabWindowFunc(screenPosition ?? new PixelPoint(100, 100));
        
        if (screenPosition.HasValue)
        {
            window.Position = new PixelPoint(
                screenPosition.Value.X - 52,
                screenPosition.Value.Y - 22
            );
        }

        window.AddTab(this);
        Window = window;
        window.SelectTab(this);
        window.Show();

        if (oldWindow.Tabs.Count == 0)
            oldWindow.Close();
    }

    public void MoveTabToWindow(TioTabWindowBase targetWindow)
    {
        if (targetWindow == null || !TioTabWindowBase.AllWindows.Contains(targetWindow))
            return;

        var oldWindow = Window;
        if (oldWindow == null)
            return;

        var oldSelected = oldWindow.SelectedTab == this;

        if (Content is Control contentControl)
        {
            DetachControl(contentControl);
        }

        Window.RemoveTab(this);

        if (oldSelected && oldWindow.Tabs.Count > 0)
        {
            oldWindow.SelectTab(oldWindow.Tabs.Last());
        }

        targetWindow.AddTab(this);
        Window = targetWindow;
        targetWindow.SelectTab(this);
        targetWindow.Activate();

        if (oldWindow.Tabs.Count == 0)
            oldWindow.Close();
    }

    private static void DetachControl(Control control)
    {
        if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
        else if (control.Parent is ContentControl contentControl)
        {
            contentControl.Content = null;
        }

        var visualParent = control.GetVisualParent();

        if (visualParent is ContentPresenter contentPresenter)
        {
            contentPresenter.Content = null; 
        }
        else if (visualParent is Panel visualPanel)
        {
            if (visualPanel.Children.Contains(control))
            {
                visualPanel.Children.Remove(control);
            }
        }
    }

    public MenuFlyout BuildContextMenu()
    {
        var flyout = new MenuFlyout();

        if (IsCloseable)
        {
            flyout.Items.Add(new MenuItem
            {
                Header = "关闭标签页",
                InputGesture = KeyGesture.Parse("Ctrl+W"),
                Command = new RelayCommand(Close),
                Icon = new PathIcon()
                {
                    Data = Geometry.Parse(
                        "M13.41 12l4.3-4.29a1 1 0 1 0-1.42-1.42L12 10.59l-4.29-4.3a1 1 0 0 0-1.42 1.42l4.3 4.29-4.3 4.29a1 1 0 0 0 0 1.42 1 1 0 0 0 1.42 0l4.29-4.3 4.29 4.3a1 1 0 0 0 1.42 0 1 1 0 0 0 0-1.42z"),
                    Width = 10, Height = 10,
                }
            });
        }

        if (Window.Tabs.Count > 1)
        {
            flyout.Items.Add(new MenuItem
            {
                Header = "关闭其他标签页",
                Command = new RelayCommand(CloseOther)
            });
        }

        var otherWindows = TioTabWindowBase.AllWindows.Where(w => w != Window).ToList();
        if (otherWindows.Any())
        {
            var moveToWindowMenuItem = new MenuItem
            {
                Header = "转移到窗口", Icon = new PathIcon()
                {
                    Data = Geometry.Parse(
                        "F1 M640,640z M0,0z M566.6,342.6C579.1,330.1,579.1,309.8,566.6,297.3L406.6,137.3C394.1,124.8 373.8,124.8 361.3,137.3 348.8,149.8 348.8,170.1 361.3,182.6L466.7,288 96,288C78.3,288 64,302.3 64,320 64,337.7 78.3,352 96,352L466.7,352 361.3,457.4C348.8,469.9 348.8,490.2 361.3,502.7 373.8,515.2 394.1,515.2 406.6,502.7L566.6,342.7z"),
                    Height = 16,
                }
            };
            foreach (var win in otherWindows)
            {
                moveToWindowMenuItem.Items.Add(new MenuItem
                {
                    Header = $"#{win.WindowId}",
                    Command = new RelayCommand(() => MoveTabToWindow(win)),
                    Classes = { "hide-icon" }
                });
            }

            flyout.Items.Add(moveToWindowMenuItem);
        }

        if (Window.Tabs.Count > 1)
        {
            flyout.Items.Add(new MenuItem
            {
                Header = "在新窗口打开",
                Command = new RelayCommand(() => MoveTabToNewWindow())
            });
        }

        if (Window.Tabs.Count > 1)
        {
            var tabs = Window.Tabs;
            var currentIndex = tabs.IndexOf(this);
            var isFirst = currentIndex <= 0;
            var isLast = currentIndex >= tabs.Count - 1;

            var reorderMenuItem = new MenuItem
            {
                Header = "排序标签页",
                Icon = new PathIcon()
                {
                    Data = Geometry.Parse(
                        "M576,512z M0,0z M246.6,374.6L150.6,470.6C138.1,483.1,117.8,483.1,105.3,470.6L9.3,374.6C-3.2,362.1 -3.2,341.8 9.3,329.3 21.8,316.8 42.1,316.8 54.6,329.3L96,370.7 96,64C96,46.3 110.3,32 128,32 145.7,32 160,46.3 160,64L160,370.7 201.4,329.3C213.9,316.8 234.2,316.8 246.7,329.3 259.2,341.8 259.2,362.1 246.7,374.6z M320,32L352,32C369.7,32 384,46.3 384,64 384,81.7 369.7,96 352,96L320,96C302.3,96 288,81.7 288,64 288,46.3 302.3,32 320,32z M320,160L416,160C433.7,160 448,174.3 448,192 448,209.7 433.7,224 416,224L320,224C302.3,224 288,209.7 288,192 288,174.3 302.3,160 320,160z M320,288L480,288C497.7,288 512,302.3 512,320 512,337.7 497.7,352 480,352L320,352C302.3,352 288,337.7 288,320 288,302.3 302.3,288 320,288z M320,416L544,416C561.7,416 576,430.3 576,448 576,465.7 561.7,480 544,480L320,480C302.3,480 288,465.7 288,448 288,430.3 302.3,416 320,416z"),
                    Height = 14,
                }
            };

            reorderMenuItem.Items.Add(new MenuItem
            {
                Header = "向前移动",
                Command = new RelayCommand(MoveTabForward, () => !isFirst),
                IsEnabled = !isFirst,
                Classes = { "hide-icon" }
            });

            reorderMenuItem.Items.Add(new MenuItem
            {
                Header = "向后移动",
                Command = new RelayCommand(MoveTabBackward, () => !isLast),
                IsEnabled = !isLast,
                Classes = { "hide-icon" }
            });

            reorderMenuItem.Items.Add(new MenuItem
            {
                Header = "移动到最前",
                Command = new RelayCommand(MoveTabToFront, () => !isFirst),
                IsEnabled = !isFirst,
                Classes = { "hide-icon" }
            });

            reorderMenuItem.Items.Add(new MenuItem
            {
                Header = "移动到最后",
                Command = new RelayCommand(MoveTabToLast, () => !isLast),
                IsEnabled = !isLast,
                Classes = { "hide-icon" }
            });

            flyout.Items.Add(reorderMenuItem);
        }

        if (Content is IContextMenuTabPage contextMenuTab)
        {
            flyout.Items.Add(new Separator());
            var customItems = new List<MenuItem>();
            contextMenuTab.BuildContextMenu(customItems);
            foreach (var item in customItems)
            {
                flyout.Items.Add(item);
            }
        }

        return flyout;
    }
}
