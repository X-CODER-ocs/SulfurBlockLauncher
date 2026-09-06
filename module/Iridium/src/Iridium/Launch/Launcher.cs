using System.Diagnostics;
using System.Text;
using Iridium.Extensions;
using Iridium.Installation;
using Iridium.Models.Launch;
using Iridium.Minecraft;
using Iridium.Interfaces;

namespace Iridium.Launch;

public sealed class Launcher {
    private readonly IArgumentParser _parser;

    public Launcher(IArgumentParser? parser = null) {
        _parser = parser ?? new ArgumentParser();
    }

    public async Task<MinecraftProcess> LaunchAsync(MinecraftContext context, LaunchConfig config, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(config);
        if (config.JavaPath is null)
            throw new InvalidOperationException("JavaPath is required");

        var entry = context.Entry;
        var layout = context.Layout;
        var directories = LaunchDirectories.Resolve(layout, entry, config);

        // Deploy the un-hashed ("virtual") asset layout before the argument parser resolves
        // ${game_assets}/${assets_root}. For pre-1.6 indexes this also populates the game
        // directory's resources/ folder, which is where those versions load sounds from.
        await new AssetsReconstructor(layout)
            .ReconstructAsync(entry, directories.GameDirectory, cancellationToken)
            .ConfigureAwait(false);

        var arguments = _parser.Build(context, config);

        if (arguments.Natives.Count > 0)
            await entry.ExtractNativesAsync(arguments.Natives, directories.NativesDirectory, cancellationToken: cancellationToken);

        List<string> launchArgs = [.. arguments.JvmArguments, arguments.MainClass, .. arguments.GameArguments];
        var javaPath = config.JavaPath.JavaPath;
        ProcessStartInfo startInfo;

        if (!string.IsNullOrWhiteSpace(config.WrapperCommand)) {
            var javaCommand = $"\"{javaPath}\" {string.Join(' ', launchArgs)}";
            var wrapped = config.WrapperCommand.Contains("{command}", StringComparison.Ordinal)
                ? config.WrapperCommand.Replace("{command}", javaCommand)
                : $"{config.WrapperCommand} {javaCommand}";

            var (fileName, wrappedArguments) = SplitCommandLine(wrapped);
            startInfo = new ProcessStartInfo(fileName) {
                Arguments = wrappedArguments,
                WorkingDirectory = directories.GameDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Default,
                StandardErrorEncoding = Encoding.Default
            };
        } else {
            startInfo = new ProcessStartInfo(javaPath) {
                WorkingDirectory = directories.GameDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Default,
                StandardErrorEncoding = Encoding.Default
            };
            foreach (var argument in launchArgs)
                startInfo.ArgumentList.Add(argument);
        }

        if (config.EnvironmentVariables is { Count: > 0 } environmentVariables)
            foreach (var (key, value) in environmentVariables)
                startInfo.EnvironmentVariables[key] = value;

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start Minecraft process: {startInfo.FileName}");
        process.EnableRaisingEvents = true;

        return new MinecraftProcess(process, launchArgs);
    }

    private static (string FileName, string Arguments) SplitCommandLine(string command) {
        command = command.Trim();
        if (command.StartsWith('"')) {
            var end = command.IndexOf('"', 1);
            if (end > 0)
                return (command[1..end], command[(end + 1)..].TrimStart());
        }

        var space = command.IndexOf(' ');
        return space < 0
            ? (command, string.Empty)
            : (command[..space], command[(space + 1)..].TrimStart());
    }
}
