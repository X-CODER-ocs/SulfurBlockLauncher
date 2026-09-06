using Avalonia.Controls;
using SulfurLauncher.Core.Minecraft.Classes;

namespace SulfurLauncher.Views.Pages.InstancePages;

public partial class BedrockResourcePacks : UserControl, IDisposable
{
    public BedrockResourcePacks()
    {
        InitializeComponent();
    }

    public BedrockResourcePacks(MinecraftInstance instance) : this()
    {
        ResourcePacksContent.Content = new ResourcePacks(instance);
    }


    public void Dispose()
    {
        (ResourcePacksContent.Content as IDisposable)?.Dispose();
    }
}