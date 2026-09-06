using Avalonia.Controls;
using TioUi.Controls;

namespace Tio.Avalonia.Standard.Standard.Ui;

public interface ITioWindow
{
    public TioNotificationManager Notification { get; set; }
    public TioToastManager Toast { get; set; }
    public TioWindow Window { get; set; }

    public void Show()
    {
        Window.Show();
    }

    public void Close()
    {
        Window.Close();
    }

    public void Activate()
    {
        Window.Activate();
    }
}