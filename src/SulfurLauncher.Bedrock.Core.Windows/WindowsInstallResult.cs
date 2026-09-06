using Windows.Management.Deployment;

namespace SulfurLauncher.Bedrock.Core.Windows;

public class WindowsInstallResult : InstallResult
{
	public DeploymentResult? DeploymentResult { get; set; }
}
