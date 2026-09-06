namespace Tio.Avalonia.Standard.Modules.Events;

public static class InitializationEvents
{
    public static event Action? BeforeReadSettings;

    public static void RaiseBeforeReadSettings()
    {
        BeforeReadSettings?.Invoke();
    }

    public static event Action? BeforeUiLoaded;

    public static void RaiseBeforeUiLoaded()
    {
        BeforeUiLoaded?.Invoke();
    }

    public static event Action? AfterUiLoaded;

    public static void RaiseAfterUiLoaded()
    {
        AfterUiLoaded?.Invoke();
    }
}