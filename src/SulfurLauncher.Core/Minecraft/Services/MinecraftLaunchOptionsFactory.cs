using SulfurLauncher.Core.App.Events;
using SulfurLauncher.Core.Const;
using SulfurLauncher.Core.Minecraft;
using SulfurLauncher.Core.Minecraft.Classes;
using SulfurLauncher.Core.Services;

namespace SulfurLauncher.Core.Minecraft.Services;

public static class MinecraftLaunchOptionsFactory
{
    public static MinecraftLaunchOptions Create(MinecraftInstance instance, Action<MinecraftLogSession>? openLog = null)
    {
        var javaConfig = instance.JavaConfig;
        var overrideAdvanced = instance.Config.EnableOverrideAdvancedOptions;

        return new MinecraftLaunchOptions
        {
            Account = Data.ConfigEntry.UsingMinecraftMinecraftAccount,
            BedrockAccount = Data.ConfigEntry.UsingBedrockAccount,
            EnableBedrockAccountInjection = Data.ConfigEntry.EnableBedrockAccountInjection,
            EnableGameOverlay = overrideAdvanced && javaConfig != null
                ? javaConfig.EnableGameOverlay
                : Data.ConfigEntry.EnableGameOverlay,
            IsFullscreen = overrideAdvanced && javaConfig != null
                ? javaConfig.EnableFullscreen
                : Data.ConfigEntry.EnableFullscreen,
            ShowGameOverlay = UiEvents.ShowGameOverlay,
            JavaRuntimes = Data.ConfigEntry.JavaRuntimes,
            JavaVersionDefaults = Data.ConfigEntry.JavaVersionDefaultPaths,
            WindowWidth = overrideAdvanced && javaConfig != null
                ? javaConfig.MinecraftWindowWidth
                : Data.ConfigEntry.MinecraftWindowWidth,
            WindowHeight = overrideAdvanced && javaConfig != null
                ? javaConfig.MinecraftWindowHeight
                : Data.ConfigEntry.MinecraftWindowHeight,
            MaxMemory = Data.ConfigEntry.MinecraftMaxMemory,
            AutoSetJavaHighPerformanceGpu = Data.ConfigEntry.AutoSetJavaHighPerformanceGpu,
            AutoOptimizeMemoryBeforeGameLaunch = Data.ConfigEntry.AutoOptimizeMemoryBeforeGameLaunch,
            SetChineseLanguageOnLaunch = overrideAdvanced && javaConfig != null
                ? javaConfig.AutoSetChineseLanguage
                : Data.ConfigEntry.AutoSetChineseLanguage,
            WindowTitle = overrideAdvanced && javaConfig != null && !string.IsNullOrWhiteSpace(javaConfig.OverrideMinecraftWindowTitle)
                ? javaConfig.OverrideMinecraftWindowTitle
                : Data.ConfigEntry.OverrideMinecraftWindowTitle,
            JvmArguments = overrideAdvanced && javaConfig != null && !string.IsNullOrWhiteSpace(javaConfig.JvmArgs)
                ? javaConfig.JvmArgs
                : Data.ConfigEntry.JvmArgs,
            BeforeLaunchCommand = overrideAdvanced && javaConfig != null && !string.IsNullOrWhiteSpace(javaConfig.BeforeLaunchCommand)
                ? javaConfig.BeforeLaunchCommand
                : Data.ConfigEntry.BeforeLaunchCommand,
            AfterLaunchCommand = overrideAdvanced && javaConfig != null && !string.IsNullOrWhiteSpace(javaConfig.AfterLaunchCommand)
                ? javaConfig.AfterLaunchCommand
                : Data.ConfigEntry.AfterLaunchCommand,
            WrapperCommand = overrideAdvanced && javaConfig != null && !string.IsNullOrWhiteSpace(javaConfig.PackagedCommand)
                ? javaConfig.PackagedCommand
                : Data.ConfigEntry.PackagedCommand,
            GameStarted = () => SulfurLauncherVisibilityService.OnGameStarted(
                overrideAdvanced ? instance.Config.SulfurLauncherVisibleMode : Data.ConfigEntry.SulfurLauncherVisibleMode),
            GameExited = SulfurLauncherVisibilityService.OnGameExited,
            AccountRefreshed = UpdateMicrosoftAccount,
            BedrockAccountRefreshed = UpdateBedrockAccount,
            OpenLog = openLog,
            InstallMissingJava = (version, progress, token) =>
                JavaAutoInstallCoordinator.EnsureAsync(version, progress, token),
            ResourceSourceRoots = ResolveResourceSourceRoots(instance)
        };
    }

    private static IReadOnlyList<string> ResolveResourceSourceRoots(MinecraftInstance instance)
    {
        var currentFolder = instance.FolderPath;
        try
        {
            return Data.ConfigEntry.MinecraftFolders
                .Where(folder => !string.Equals(folder.FolderPath, currentFolder, StringComparison.OrdinalIgnoreCase))
                .SelectMany(MinecraftResourceRoots.Resolve)
                .Where(path => Directory.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static void UpdateMicrosoftAccount(MinecraftAccount original, MinecraftAccount refreshed)
    {
        var accounts = Data.ConfigEntry.MinecraftAccounts;
        var index = accounts.IndexOf(original);
        if (index >= 0)
            accounts[index] = refreshed;
        Data.ConfigEntry.UsingMinecraftMinecraftAccount = refreshed;
    }

    private static void UpdateBedrockAccount(BedrockAccount original, BedrockAccount refreshed)
    {
        var accounts = Data.ConfigEntry.BedrockAccounts;
        var index = accounts.IndexOf(original);
        if (index >= 0) accounts[index] = refreshed;
        Data.ConfigEntry.UsingBedrockAccount = refreshed;
    }
}
