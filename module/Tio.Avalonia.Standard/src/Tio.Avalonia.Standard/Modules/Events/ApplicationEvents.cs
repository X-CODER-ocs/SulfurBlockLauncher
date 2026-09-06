using Avalonia.Input;

namespace Tio.Avalonia.Standard.Modules.Events;

public static class ApplicationEvents
{
    public static event Action? SaveSettings;

    public static void RaiseSaveSettings()
    {
        SaveSettings?.Invoke();
    }


    public static event Func<Task<bool>>? AppExiting;

    public static async Task<bool> RaiseAppExiting()
    {
        if (AppExiting == null) return true;

        foreach (var handler in AppExiting.GetInvocationList())
        {
            var asyncHandler = (Func<Task<bool>>)handler;
            var canContinue = await asyncHandler.Invoke();

            if (!canContinue)
            {
                return false;
            }
        }

        return true;
    }

    public static event Action<object?, DragEventArgs>? AppDragDrop;

    public static void RaiseAppDragDrop(object? sender, DragEventArgs e)
    {
        AppDragDrop?.Invoke(sender, e);
    }
}