using Avalonia.Controls;
using SulfurLauncher.Core.Minecraft.Classes;

namespace SulfurLauncher.Views.Pages.InstancePages;

public partial class BedrockSkins : UserControl, IDisposable
{
    public BedrockSkins()
    {
        InitializeComponent();
    }

    public BedrockSkins(MinecraftInstance instance) : this()
    {
        SkinPacksContent.Content = new SkinPacks(instance);
    }


    public void Dispose()
    {
        (SkinPacksContent.Content as IDisposable)?.Dispose();
    }
}