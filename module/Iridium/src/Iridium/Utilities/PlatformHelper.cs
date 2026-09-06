using System.Runtime.InteropServices;

namespace Iridium.Utilities;

public static class PlatformHelper {
    public static Architecture Architecture => RuntimeInformation.ProcessArchitecture;

    public static string GetPlatformName() {
        var os = OperatingSystem.IsWindows()
            ? "windows"
            : OperatingSystem.IsMacOS() 
                ? "macos"
                : "linux";
        
        return os;
    }
    
    public static string GetPlatformInfo() {
        var os = GetPlatformName();
        
        var arch = Architecture switch {
            Architecture.X86 => "x86",
            Architecture.X64 => "x86_64",
            Architecture.Arm64 => "arm64",
            var other => other.ToString().ToLowerInvariant()
        };
        
        return $"{os}-{arch}";
    }
}