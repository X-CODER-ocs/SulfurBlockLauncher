using SulfurLauncher.Core.Module.Widgets;
using SulfurLauncher.Core.Services.SystemResources;

using SulfurLauncher.Module;
namespace SulfurLauncher.Views.Widgets;

public sealed class CpuResourceWidget : ResourceWidgetBase
{
    public CpuResourceWidget(WidgetCellSize size) : base(size)
    {
        Title = "CPU";
        IconGlyph = "\ue62b";
        HasSecondaryText = false;
    }

    public override ResourceKind ResourceKind => ResourceKind.Cpu;

    protected override void OnUpdate(ResourceSnapshot snapshot)
    {
        if (snapshot.CpuUsage is { } usage)
        {
            PrimaryText = $"{usage:F1}%";
            Percentage = usage;
            ProgressValue = usage;
        }
        else
        {
            PrimaryText = "N/A";
            Percentage = 0;
            ProgressValue = 0;
        }
    }
}