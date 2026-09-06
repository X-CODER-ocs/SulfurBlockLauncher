using MinecraftLaunch.Base.Enums;

namespace MinecraftLaunch.Base.Models;

public record ComponentSettings {
    public DownloadSourceMode MinecraftMetadataSource { get; set; } = DownloadSourceMode.Auto;
    public DownloadSourceMode MinecraftFileSource { get; set; } = DownloadSourceMode.Auto;
    public bool IsEnableFragment { get; set; } = true;

    public int MaxThread { get; set; } = 64;
    public int MaxFragment { get; set; } = 128;
    public int MaxRetryCount { get; set; } = 8;

    public string CurseForgeApiKey { get; set; } = null;
    public string UserAgent { get; set; } = "MinecraftLaunch/4.0";
    public bool DisableSystemProxy { get; set; }
    public string? ProxyServer { get; set; }
}
