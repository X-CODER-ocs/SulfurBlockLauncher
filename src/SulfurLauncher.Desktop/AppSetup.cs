using SulfurLauncher.Bedrock;
using SulfurLauncher.Bedrock.Standard.Interface;
using SulfurLauncher.Core.Minecraft;
using SulfurLauncher.Core.Services;

namespace SulfurLauncher.Desktop;

internal static class AppSetup
{
#if WINDOWS || LINUX
    public static void RegisterBedrockLauncher()
    {
#if WINDOWS
        MinecraftLaunchService.DefaultBedrockLauncherFactory =
            config =>
            {
                config.LauncherVersion = AppVersionService.Instance.Version.VersionTitle;
                return new BedrockLaunch(config);
            };
        BedrockInstallationService.DefaultInstaller =
            new BedrockInstaller();
        BedrockToolsService.Default =
            new BedrockWindowsToolsService();
#elif LINUX
        MinecraftLaunchService.DefaultBedrockLauncherFactory =
            config =>
            {
                config.LauncherVersion = AppVersionService.Instance.Version.VersionTitle;
                return new Bedrock.Linux.BedrockLaunch(config);
            };
        Bedrock.Standard.Interface.BedrockInstallationService.DefaultInstaller =
            new Bedrock.Linux.BedrockInstaller();
#endif
    }
#endif
}
