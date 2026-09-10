using System.Runtime.InteropServices;

namespace SulfurLauncher.Desktop;

/// <summary>
/// Works around the macOS 27 + SIP/AMFI-disabled JIT write-protection bug.
/// <para>
/// When <c>amfi_get_out_of_my_way=1</c>, dyld treats every process as
/// platform-signed and resets <c>MAP_JIT</c> regions to read-execute after
/// <c>dlopen</c> / mmap. The .NET runtime's JIT compiler and GC background
/// thread crash when writing to JIT code pages (SIGBUS / GC corruption).
/// </para>
/// <para>
/// Fix: install a SIGBUS handler that calls
/// <c>pthread_jit_write_protect_np(0)</c>. Because signal handlers run in the
/// faulting thread's context, this re-enables JIT writes for whichever thread
/// triggered the fault. The kernel then retries the faulting store instruction.
/// Call <see cref="Install"/> at the very beginning of <c>Main</c>.
/// </para>
/// </summary>
internal static class MacOSJitFixNative
{
    // macOS struct sigaction layout (arm64): 8 + 4 + 4 = 16 bytes.
    // sigset_t is __uint32_t (4 bytes), NOT a pointer.
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct Sigaction
    {
        public nuint SaSigaction; // union sa_handler/sa_sigaction (func ptr)
        public uint SaMask;       // sigset_t = uint32_t
        public int SaFlags;
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
    private static void SigbusHandler(int sig, nint info, nint ctx)
    {
        // Runs in the faulting thread's context, so this enables JIT writes
        // for exactly the thread that needs it.
        pthread_jit_write_protect_np(0);
    }

    /// <summary>
    /// Disables JIT write protection for the main thread and installs the
    /// SIGBUS handler. No-op on non-macOS platforms.
    /// </summary>
    public static unsafe void Install()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        try
        {
            // Protect the main thread immediately (before any JIT activity).
            pthread_jit_write_protect_np(0);

            var sa = new Sigaction
            {
                SaSigaction = (nuint)(delegate* unmanaged[Cdecl]<int, nint, nint, void>)&SigbusHandler,
                SaFlags = SA_SIGINFO | SA_RESTART,
                SaMask = 0
            };

            sigaction(SIGBUS, ref sa, nint.Zero);
        }
        catch
        {
            // Best-effort; continue even if signal setup fails.
        }
    }
}
