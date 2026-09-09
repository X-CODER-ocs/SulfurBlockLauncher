using System.Runtime.InteropServices;

namespace SulfurLauncher.Desktop;

/// <summary>
/// Installs a SIGBUS handler directly into the current .NET process on macOS
/// to work around the JIT write-protection issue caused by SIP/AMFI being disabled.
/// <para>
/// When amfi_get_out_of_my_way=1, dyld resets MAP_JIT regions to read-execute after
/// dlopen. Both the .NET runtime (GC background thread) and Java subprocesses crash
/// with SIGBUS (BUS_ADRALN) when writing to JIT code pages.
/// </para>
/// <para>
/// This handler calls pthread_jit_write_protect_np(0) to re-enable writes, after
/// which the kernel retries the faulting instruction. It must be installed before
/// any JIT/GC activity, so call <see cref="Install"/> at the very beginning of Main.
/// </para>
/// </summary>
internal static class MacOSJitFixNative
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Sigaction
    {
        public nuint SaSigaction;
        public UIntPtr SaMask;          // uses sa_mask but we keep it simple
        public int SaFlags;
        public nuint SaRestorer;       // not used but present in struct layout
    }

    [DllImport("libsystem_c.dylib", SetLastError = true)]
    private static extern int sigaction(
        int sig,
        ref Sigaction act,
        nint oldact);

    [DllImport("libsystem_pthread.dylib")]
    private static extern void pthread_jit_write_protect_np(int enable);

    private const int SIGBUS = 10;
    private const int SA_SIGINFO = 0x40;
    private const int SA_RESTART = 0x2;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static unsafe void SigbusHandler(int sig, nint info, nint ctx)
    {
        pthread_jit_write_protect_np(0);
    }

    /// <summary>
    /// Installs the SIGBUS handler and disables JIT write protection.
    /// No-op on non-macOS platforms.
    /// </summary>
    public static unsafe void Install()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        try
        {
            // Disable write protection immediately.
            pthread_jit_write_protect_np(0);

            // Install SIGBUS handler.
            var sa = new Sigaction
            {
                SaSigaction = (nuint)(delegate* unmanaged[Cdecl]<int, nint, nint, void>)&SigbusHandler,
                SaFlags = SA_SIGINFO | SA_RESTART,
                SaMask = UIntPtr.Zero,
                SaRestorer = 0
            };

            sigaction(SIGBUS, ref sa, nint.Zero);

        }
        catch
        {
            // Best-effort; if P/Invoke fails, the process continues without the fix.
        }
    }
}
