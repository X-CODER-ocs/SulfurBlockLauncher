using System.Diagnostics;

namespace Iridium.Launch;

/// <summary>
/// Applies the macOS JIT write-protection SIGBUS workaround to a
/// <see cref="ProcessStartInfo"/> by preloading a small native dylib
/// (<c>libjvm_sigbus_fix.dylib</c>) via <c>DYLD_INSERT_LIBRARIES</c>.
/// <para>
/// When System Integrity Protection / AMFI is disabled on macOS
/// (<c>amfi_get_out_of_my_way=1</c>), dyld treats every process as
/// platform-signed and resets <c>MAP_JIT</c> regions to read-execute after
/// <c>dlopen</c>. HotSpot then crashes in <c>CodeHeap::allocate</c> with
/// <c>SIGBUS (BUS_ADRALN)</c> because it can no longer write JIT stubs.
/// </para>
/// <para>
/// The injected dylib installs a <c>SIGBUS</c> handler that calls
/// <c>pthread_jit_write_protect_np(0)</c> to re-enable writes, after which
/// the kernel retries the faulting store instruction.
/// </para>
/// </summary>
public static class MacOSJitFix
{
    private const string DylibRelativePath = "runtimes/osx-arm64/native/libjvm_sigbus_fix.dylib";
    private const string EnvVar = "DYLD_INSERT_LIBRARIES";

    /// <summary>
    /// Resolves the absolute path to the bundled fix dylib, or <c>null</c> if
    /// it cannot be found.
    /// </summary>
    public static string? ResolveDylibPath() {
        if (!OperatingSystem.IsMacOS())
            return null;

        var baseDirs = new[]
        {
            Path.GetDirectoryName(typeof(MacOSJitFix).Assembly.Location),
            AppContext.BaseDirectory
        };

        foreach (var dir in baseDirs) {
            if (string.IsNullOrEmpty(dir))
                continue;
            var candidate = Path.Combine(dir, DylibRelativePath);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Injects the macOS JIT SIGBUS workaround into <paramref name="startInfo"/>
    /// when running on macOS and the bundled dylib is present.
    /// </summary>
    /// <returns><c>true</c> if the workaround was applied.</returns>
    public static bool Apply(ProcessStartInfo startInfo) {
        ArgumentNullException.ThrowIfNull(startInfo);

        var dylib = ResolveDylibPath();
        if (dylib is null)
            return false;

        // Use the modern IDictionary<string,string?> API (ProcessStartInfo.Environment)
        // instead of the legacy StringDictionary (EnvironmentVariables). The legacy
        // indexer throws KeyNotFoundException on missing keys across some runtime
        // builds; the modern dictionary's TryGetValue / indexer never throws.
        var env = startInfo.Environment;
        env.TryGetValue(EnvVar, out var existing);
        env[EnvVar] = !string.IsNullOrEmpty(existing)
            ? $"{dylib}:{existing}"
            : dylib;

        return true;
    }
}
