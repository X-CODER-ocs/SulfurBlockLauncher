// 本段部分实现逻辑参考自 ProjBobcat 的 DeepJavaSearcher.cs
// 仓库地址：https://github.com/Corona-Studio/ProjBobcat

using Microsoft.Win32;
using MinecraftLaunch.Base.Models.Game;
using MinecraftLaunch.Base.Utilities;
using MinecraftLaunch.Extensions;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace MinecraftLaunch.Utilities;

public static partial class JavaUtil {
    public static async Task<JavaEntry> GetJavaInfoAsync(string javaPath, CancellationToken cancellationToken = default) {
        if (string.IsNullOrEmpty(javaPath) || !File.Exists(javaPath)) {
            return null;
        }

        using var process = Process.Start(new ProcessStartInfo(javaPath) {
            Arguments = "-version",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        });

        string text = await process.StandardError.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
            text = await process.StandardOutput.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var match = JavaVersionRegex().Match(text);
        if (!match.Success)
            return null;

        bool is64bit = text.Contains("64-bit", StringComparison.OrdinalIgnoreCase);
        string javaVersion = match.Groups["version"].Value;

        string javaType = text.Contains("java(tm)", StringComparison.OrdinalIgnoreCase)
            ? "Java"
            : text.Contains("zulu")
                ? "ZuluJDK"
                : "OpenJDK";

        var versionParts = javaVersion.Split(['.', '_', '-', '+'], StringSplitOptions.RemoveEmptyEntries);
        if (!int.TryParse(versionParts[0], out var firstVersionPart))
            return null;

        var majorVersion = firstVersionPart;
        if (firstVersionPart == 1)
        {
            if (versionParts.Length < 2 || !int.TryParse(versionParts[1], out majorVersion))
                return null;
        }

        return new JavaEntry {
            Is64bit = is64bit,
            JavaPath = javaPath,
            JavaType = javaType,
            JavaVersion = javaVersion,
            MajorVersion = majorVersion,
        };
    }

    public static async IAsyncEnumerable<JavaEntry> EnumerableJavaAsync(
        bool fastMode = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        if (EnvironmentUtil.IsWindow) {
            static string GetJavaHomePath(string javaPath) {
                var binPath = Path.GetDirectoryName(javaPath);
                return binPath is null ? javaPath : Directory.GetParent(binPath)?.FullName ?? binPath;
            }

            var javaPaths = GetJavasForWindows(fastMode)
                .Where(File.Exists)
                .GroupBy(GetJavaHomePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(path =>
                    string.Equals(Path.GetFileName(path), "java.exe", StringComparison.OrdinalIgnoreCase) ? 0 : 1).First());

            foreach (var java in javaPaths) {
                if (File.Exists(java))
                    yield return await GetJavaInfoAsync(java, cancellationToken);
            }

            yield break;
        }

        using var process = Process.Start(new ProcessStartInfo("which") {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            ArgumentList = {
                "-a",
                "java"
            },
        });

        if (process == null)
            yield break;

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        foreach (var path in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Distinct(StringComparer.Ordinal)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
                yield return await GetJavaInfoAsync(path, cancellationToken);
        }
    }

    #region Privates

    [GeneratedRegex("(?im)^(?:java|openjdk)(?:\\s+version)?\\s+\\\"?(?<version>\\d+[^\\s\\\"]*)")]
    private static partial Regex JavaVersionRegex();

    [SupportedOSPlatform("Windows")]
    private static IEnumerable<string> GetJavasForWindows(bool fastMode = false) {
        //Use by:https://github.com/Xcube-Studio/Natsurainko.FluentCore/blob/main/Natsurainko.FluentCore/Environment/JavaUtils.cs
        List<string> result = [];

        #region Cmd: Find Java by running "where javaw" command in cmd.exe

        using var process = new Process() {
            StartInfo = new ProcessStartInfo() {
                FileName = "cmd",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true,
        };

        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        var output = new List<string>();

        process.OutputDataReceived += (sender, e) => output.Add(e.Data);
        process.ErrorDataReceived += (sender, e) => output.Add(e.Data);

        process.StandardInput.WriteLine("where javaw");
        process.StandardInput.WriteLine("exit");
        process.WaitForExit();

        IEnumerable<string> javaPaths = output.Where(
            x => !string.IsNullOrEmpty(x) && x.EndsWith("javaw.exe") && File.Exists(x)
        )!; // null checked in the where clause
        result.AddRange(javaPaths);

        #endregion

        #region Registry: Find Java by searching the registry

        var javaHomePaths = new List<string>();

        // Local function: recursively search for the keyName in the registry
        static List<string> ForRegistryKey(RegistryKey registryKey, string keyName) {
            var result = new List<string>();

            foreach (string valueName in registryKey.GetValueNames()) {
                if (valueName == keyName) // Check that the valueName exists
                    result.Add((string)registryKey.GetValue(valueName)!);
            }

            foreach (string registrySubKey in registryKey.GetSubKeyNames()) {
                using var subKey = registryKey.OpenSubKey(registrySubKey);
                if (subKey is not null) // Check that the registrySubKey exists
                    result.AddRange(ForRegistryKey(subKey, keyName));
            }

            return result;
        }
        ;

        using var reg = Registry.LocalMachine.OpenSubKey("SOFTWARE");

        if (reg is not null && reg.GetSubKeyNames().Contains("JavaSoft")) {
            using var registryKey = reg.OpenSubKey("JavaSoft");
            if (registryKey is not null)
                javaHomePaths.AddRange(ForRegistryKey(registryKey, "JavaHome"));
        }

        if (reg is not null && reg.GetSubKeyNames().Contains("WOW6432Node")) {
            using var registryKey = reg.OpenSubKey("WOW6432Node");
            if (registryKey is not null && registryKey.GetSubKeyNames().Contains("JavaSoft")) {
                using var registrySubKey = reg.OpenSubKey("JavaSoft");
                if (registrySubKey is not null)
                    ForRegistryKey(registrySubKey, "JavaHome").ForEach(x => javaHomePaths.Add(x));
            }
        }

        foreach (var item in javaHomePaths)
            if (Directory.Exists(item))
                result.AddRange(new DirectoryInfo(item).FindAll("javaw.exe").Select(x => x.FullName));

        #endregion

        #region Special Folders

        List<string> folders = [];

        // %APPDATA%\.minecraft\cache\java
        string appDataPath = Environment.GetEnvironmentVariable("APPDATA");

        if (!string.IsNullOrEmpty(appDataPath))
            folders.Add(Path.Combine(appDataPath, ".minecraft\\cache\\java"));

        // %APPDATA%\.minecraft\runtime\
        if (!string.IsNullOrEmpty(appDataPath))
            folders.Add(Path.Combine(appDataPath, ".minecraft\\runtime\\"));

        // %JAVA_HOME%
        string javaHomePath = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (javaHomePath is not null)
            folders.Add(javaHomePath);

        // Program Files\Java
        folders.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"));
        folders.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Java"));

        // Program Files\Zulu
        folders.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Zulu"));
        folders.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Zulu"));

        // Common Java and Minecraft locations on every ready fixed drive.
        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed)) {
            folders.Add(Path.Combine(drive.RootDirectory.FullName, "Java"));
            folders.Add(Path.Combine(drive.RootDirectory.FullName, "jdk"));
            folders.Add(Path.Combine(drive.RootDirectory.FullName, "Minecraft"));
        }

        // Check Java for each folder.
        // fastMode: 有限深度搜索（5层），跳过深层递归，大幅提速
        // 非 fastMode: 无限深度搜索（FindAll），确保找到所有 Java
        var searchDepth = fastMode ? 5 : int.MaxValue;
        foreach (var folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
            if (Directory.Exists(folder)) {
                var directory = new DirectoryInfo(folder);
                result.AddRange(directory.FindAllLimited("java.exe", searchDepth).Select(x => x.FullName));
                result.AddRange(directory.FindAllLimited("javaw.exe", searchDepth).Select(x => x.FullName));
            }

        #endregion

        return result.Distinct();
    }

    #endregion
}
