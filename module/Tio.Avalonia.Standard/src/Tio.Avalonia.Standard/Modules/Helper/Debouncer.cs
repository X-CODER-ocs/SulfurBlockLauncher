using System.Timers;
using Timer = System.Timers.Timer;

namespace Tio.Avalonia.Standard.Modules.Helper;

public class Debouncer
{
    private readonly Timer _timer;
    private readonly Action _action;

    public Debouncer(Action action, double interval = 1000)
    {
        _action = action;
        _timer = new Timer(interval);
        _timer.Elapsed += OnTimerElapsed!;
        _timer.AutoReset = false;
    }

    public void Invoke()
    {
        _timer.Stop();
        _timer.Start();
    }

    private void OnTimerElapsed(object source, ElapsedEventArgs e)
    {
        _timer.Stop();
        _action();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}