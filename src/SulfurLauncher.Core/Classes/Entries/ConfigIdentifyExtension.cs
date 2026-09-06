using SulfurLauncher.Core.Classes.Config;
using SulfurLauncher.Core.Minecraft.Classes;
using Tio.Avalonia.Standard.Modules.DiskIO;

namespace SulfurLauncher.Core.Classes.Entries;

public class ConfigIdentifyExtension
{
    public static void MinecraftFolder(ConfigEntry entry)
    {
        var installableFolders = entry.MinecraftFolders.Where(IsInstallableFolder).ToList();
        if (installableFolders.Count == 0)
        {
            entry.DefaultMinecraftFolder = null;
            var defaultFolder = CreateDefaultMinecraftFolder();
            entry.MinecraftFolders.Insert(0, defaultFolder);
            installableFolders.Add(defaultFolder);
        }

        if (entry.DefaultMinecraftFolder == null ||
            !entry.MinecraftFolders.Contains(entry.DefaultMinecraftFolder) ||
            !IsInstallableFolder(entry.DefaultMinecraftFolder))
            entry.DefaultMinecraftFolder = installableFolders[0];
    }

    public static void Window(ConfigEntry entry)
    {
        if (entry.TabWindowHeight < 379)
            entry.TabWindowHeight = 710;
        if (entry.TabWindowWidth < 709)
            entry.TabWindowWidth = 1200;
        if (entry.AppScale < 0.49)
            entry.AppScale = 1;
    }

    private static bool IsInstallableFolder(MinecraftFolderEntry folder)
    {
        return folder.DetectedLayout.Kind == MinecraftFolderKind.SulfurLauncherMc;
    }

    private static MinecraftFolderEntry CreateDefaultMinecraftFolder()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "studio.lively.sulfurlauncher.minecraft");

        foreach (var directory in new[]
                 {
                     Path.Combine(path, "meta", "assets"),
                     Path.Combine(path, "meta", "libraries"),
                     Path.Combine(path, "meta", "natives"),
                     Path.Combine(path, "meta", "versions"),
                     Path.Combine(path, "instances"),
                     Path.Combine(path, "bedrock_instances")
                 })
            Helper.TryCreateFolder(directory);
        return new MinecraftFolderEntry
        {
            FolderName = "SulfurLauncher MC",
            FolderPath = path,
            FolderKind = MinecraftFolderKind.SulfurLauncherMc
        };
    }
}