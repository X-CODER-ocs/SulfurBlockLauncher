using MinecraftLaunch.Base.Models.Game;
using MinecraftLaunch.Components.Parser;
using MinecraftLaunch.Extensions;
using System.Diagnostics;
using System.Text;

namespace MinecraftLaunch.Launch;

public sealed class MinecraftProcess : IDisposable {
    public Process Process { get; private set; }
    public IEnumerable<string> ArgumentList { get; init; }
    public IReadOnlyList<MinecraftLibrary> Natives { get; private set; }
    public nint MainWindowHandle => Process.MainWindowHandle;

    public event EventHandler Started;
    public event EventHandler<EventArgs> Exited;
    public event EventHandler<LogReceivedEventArgs> OutputLogReceived;

    public MinecraftProcess(LaunchConfig launchConfig, MinecraftEntry minecraft, IEnumerable<string> launchArgs) {
        ArgumentList = launchArgs;
        if (!ArgumentList.Any())
            return;

        var fileName = launchConfig.JavaPath.JavaPath;
        var arguments = string.Join(' ', launchArgs);

        if (!string.IsNullOrWhiteSpace(launchConfig.WrapperCommand)) {
            var javaCommand = $"\"{fileName}\" {arguments}";
            var wrapped = launchConfig.WrapperCommand.Contains("{command}")
                ? launchConfig.WrapperCommand.Replace("{command}", javaCommand)
                : $"{launchConfig.WrapperCommand} {javaCommand}";

            (fileName, arguments) = SplitCommandLine(wrapped);
        }

        Process = new Process {
            StartInfo = new ProcessStartInfo(fileName) {
                WorkingDirectory = minecraft.ToWorkingPath(launchConfig.IsEnableIndependency),
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Default,
                StandardErrorEncoding = Encoding.Default,
            },
            EnableRaisingEvents = true,
        };

        foreach (var (key, value) in launchConfig.EnvironmentVariables ?? []) {
            Process.StartInfo.EnvironmentVariables[key] = value;
        }

        Process.Exited += OnMinecraftProcessExited;
        Process.ErrorDataReceived += OnOutputDataReceived;
        Process.OutputDataReceived += OnOutputDataReceived;

        Start();
    }

    public void Start() {
        Process.Start();
        Process.BeginOutputReadLine();
        Process.BeginErrorReadLine();
        Started?.Invoke(this, EventArgs.Empty);
    }

    public void Close() {
        Process.Kill();
    }

    public void Dispose() => Process?.Dispose();

    private static (string fileName, string arguments) SplitCommandLine(string command) {
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

    private void OnMinecraftProcessExited(object sender, EventArgs e) {
        Exited?.Invoke(this, new());
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e) {
        if (!string.IsNullOrEmpty(e.Data)) {
            OutputLogReceived?.Invoke(this, new LogReceivedEventArgs(MinecraftLoggingParser.Parse(e.Data)));
        }
    }
}

public record LogReceivedEventArgs(MinecraftLogEntry Data);
