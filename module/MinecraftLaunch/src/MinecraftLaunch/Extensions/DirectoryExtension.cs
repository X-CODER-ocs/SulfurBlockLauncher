namespace MinecraftLaunch.Extensions;

public static class DirectoryExtension {
    public static IEnumerable<FileInfo> FindAll(this DirectoryInfo directory, string file) {
        foreach (var item in directory.EnumerateFiles())
            if (item.Name == file)
                yield return item;

        foreach (var item in directory.EnumerateDirectories())
            foreach (var info in item.FindAll(file))
                yield return info;
    }

    /// <summary>
    /// 有限深度递归搜索文件。maxDepth=0 只搜当前目录，不进入子目录。
    /// </summary>
    public static IEnumerable<FileInfo> FindAllLimited(this DirectoryInfo directory, string file, int maxDepth) {
        foreach (var item in directory.EnumerateFiles())
            if (item.Name == file)
                yield return item;

        if (maxDepth <= 0) yield break;

        foreach (var item in directory.EnumerateDirectories())
            foreach (var info in item.FindAllLimited(file, maxDepth - 1))
                yield return info;
    }
}
