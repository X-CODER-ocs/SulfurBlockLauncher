using Iridium.Models.Launch;
using Iridium.Minecraft;

namespace Iridium.Interfaces;

public interface IArgumentParser {
    LaunchArguments Build(MinecraftContext context, LaunchConfig config);
}
