using Avalonia.Controls;
using SulfurLauncher.Core.Minecraft.Classes;

namespace SulfurLauncher.Views.Pages.InstancePages;

public partial class BedrockBehaviorPacks : UserControl, IDisposable
{
    public BedrockBehaviorPacks()
    {
        InitializeComponent();
    }

    public BedrockBehaviorPacks(MinecraftInstance instance) : this()
    {
        BehaviorPacksContent.Content = new BehaviorPacks(instance);
    }


    public void Dispose()
    {
        (BehaviorPacksContent.Content as IDisposable)?.Dispose();
    }
}