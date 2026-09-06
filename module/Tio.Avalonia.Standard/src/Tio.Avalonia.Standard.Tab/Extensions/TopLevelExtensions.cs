using Avalonia.Controls;
using Tio.Avalonia.Standard.Tab.Interface;
using TioUi.Common.Extensions;
using TioUi.Controls;

namespace Tio.Avalonia.Standard.Tab.Extensions;

public static class TopLevelExtensions
{
    public static TioToastManager? TryGetToast(this Control control)
    {
        return ((control.GetTopLevel() as TioWindow) as TioTabWindowBase)?.Toast;
    }

    public static TioNotificationManager? TryGetNotification(this Control control)
    {
        return ((control.GetTopLevel() as TioWindow) as TioTabWindowBase)?.Notification;
    }
    
    public static TopLevel AsTopLevel(this object control)
    {
        return (control as Control)!.GetTopLevel();
    }
}