using System;
using System.Threading;
using Windows.Management.Deployment;

namespace SulfurLauncher.Bedrock.Core.Windows;

public class LaunchOptions
{
	public required MinecraftBuildTypeVersion MinecraftBuildType;

	public required MinecraftGameTypeVersion GameType;

	public required string GameFolder;

	public IProgress<LaunchState>? Progress;

	public string? LaunchArgs;

	public CancellationToken? CancellationToken;

	public IProgress<DeploymentProgress>? RegisterProgress;

	public bool Old_VersionLaunching;
}
