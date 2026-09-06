namespace MinecraftLaunch.Base.EventArgs;

/// <summary>
/// 表示从本地资源目录复制游戏资源文件时的进度。
/// </summary>
public sealed class ResourceCopyProgressChangedEventArgs : System.EventArgs {
    public int TotalCount { get; set; }
    public int CompletedCount { get; set; }
    public long TotalBytes { get; set; }
    public long CopiedBytes { get; set; }
    public string CurrentFile { get; set; }

    public double Percentage => TotalBytes > 0
        ? CopiedBytes * 100d / TotalBytes
        : 0;
}
