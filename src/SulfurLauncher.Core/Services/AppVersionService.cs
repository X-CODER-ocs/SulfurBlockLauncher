using System.Reflection;
using System.Text.Json;
using SulfurLauncher.Core.Classes.Entries;
using SulfurLauncher.Core.Json;
using SulfurLauncher.Localization;
using Tio.Avalonia.Standard.Modules.DiskIO;

namespace SulfurLauncher.Core.Services;

public sealed class AppVersionService
{
    private const string VersionResourceName = "SulfurLauncher.Core.Assets.version-ci.txt";
    private static AppVersionService? _instance;

    private AppVersionService()
    {
        Version = LoadVersionInfo();
    }

    public static AppVersionService Instance => _instance ??= new AppVersionService();

    public CiVersionInfo Version { get; private set; }

    private static CiVersionInfo LoadVersionInfo()
    {
        Logger.Info(LogLanguageManager.Instance.appVersion_loadStart.CurrentValue());
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(VersionResourceName);
        if (stream is null)
        {
            Logger.Warning(LogLanguageManager.Instance.appVersion_embeddedNotFoundUseLocal.CurrentValue());
            return CreateLocalVersionInfo();
        }

        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        var versionInfo = string.IsNullOrWhiteSpace(text)
            ? null
            : JsonSerializer.Deserialize<CiVersionInfo>(text, SulfurLauncherJson.Options);
        Logger.Info(string.Format(LogLanguageManager.Instance.appVersion_loadComplete.CurrentValue(),
            versionInfo?.VersionTitle ?? "local-build", versionInfo?.Type ?? "dev"));
        return versionInfo ?? CreateLocalVersionInfo();
    }

    private static CiVersionInfo CreateLocalVersionInfo()
    {
        return new CiVersionInfo
        {
            Type = "dev",
            VersionTitle = "local-build"
        };
    }
}