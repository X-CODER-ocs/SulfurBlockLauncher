using MinecraftLaunch.Base.Models.Game;
using MinecraftLaunch.Components.Parser;
using MinecraftLaunch.Extensions;
using System.Threading;

namespace MinecraftLaunch.Launch;

public sealed class MinecraftRunner {
    private readonly MinecraftParser _minecraftParser;

    public LaunchConfig LaunchConfig { get; set; }

    public MinecraftRunner(LaunchConfig launchConfig, MinecraftParser parser) {
        _minecraftParser = parser;
        LaunchConfig = launchConfig;
    }

    public MinecraftProcess Run(string id) {
        MinecraftEntry minecraft = _minecraftParser.GetMinecraft(id);
        ArgumentsParser parser = new(minecraft, LaunchConfig);
        IEnumerable<string> arguments = parser.Parse();

        if (string.IsNullOrEmpty(LaunchConfig.NativesFolder))
            minecraft.ExtractNatives(parser.GetNatives());

        return new MinecraftProcess(LaunchConfig, minecraft, arguments);
    }

    public MinecraftProcess Run(MinecraftEntry minecraft) {
        ArgumentsParser parser = new(minecraft, LaunchConfig);
        IEnumerable<string> arguments = parser.Parse();

        if (string.IsNullOrEmpty(LaunchConfig.NativesFolder))
            minecraft.ExtractNatives(parser.GetNatives());

        return new MinecraftProcess(LaunchConfig, minecraft, arguments);
    }

    public async Task<MinecraftProcess> RunAsync(string id, CancellationToken cancellationToken = default) {
        MinecraftEntry minecraft = _minecraftParser.GetMinecraft(id);
        ArgumentsParser parser = new(minecraft, LaunchConfig);
        IEnumerable<string> arguments = parser.Parse();

        if (string.IsNullOrEmpty(LaunchConfig.NativesFolder))
            await minecraft.ExtractNativesAsync(parser.GetNatives(), cancellationToken);

        return new MinecraftProcess(LaunchConfig, minecraft, arguments);
    }

    public async Task<MinecraftProcess> RunAsync(MinecraftEntry minecraft, CancellationToken cancellationToken = default) {
        ArgumentsParser parser = new(minecraft, LaunchConfig);
        IEnumerable<string> arguments = parser.Parse();

        if (string.IsNullOrEmpty(LaunchConfig.NativesFolder))
            await minecraft.ExtractNativesAsync(parser.GetNatives(), cancellationToken);

        return new MinecraftProcess(LaunchConfig, minecraft, arguments);
    }
}
