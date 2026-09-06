using System;
using Windows.Management.Deployment;

namespace SulfurLauncher.Bedrock.Core.Windows;

public class WindowsLocalGamePackageOptions : LocalGamePackageOptions
{
	public Progress<DeploymentProgress>? DeployProgress { get; set; }
}
