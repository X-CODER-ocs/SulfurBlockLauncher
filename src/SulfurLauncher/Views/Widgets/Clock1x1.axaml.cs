using SulfurLauncher.Core.Module.Widgets;

namespace SulfurLauncher.Views.Widgets;

public partial class Clock1x1 : ClockWidgetBase
{
    public Clock1x1()
    {
        Size = new WidgetCellSize(1, 1);
        InitializeComponent();
        InitializeClock();
    }
}