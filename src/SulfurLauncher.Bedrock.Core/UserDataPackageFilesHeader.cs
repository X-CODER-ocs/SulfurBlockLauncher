using System.Runtime.InteropServices;

namespace SulfurLauncher.Bedrock.Core;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
public struct UserDataPackageFilesHeader
{
	public uint Version;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
	public string PackageFullName;

	public uint FileCount;
}
