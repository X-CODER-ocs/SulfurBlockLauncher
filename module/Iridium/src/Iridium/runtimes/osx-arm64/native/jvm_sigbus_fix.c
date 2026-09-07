/*
 * jvm_sigbus_fix.c — macOS JIT write-protection SIGBUS workaround
 *
 * On macOS with System Integrity Protection (SIP) / AMFI disabled
 * (amfi_get_out_of_my_way=1), dyld treats every process as platform-signed.
 * This causes dlopen() to reset MAP_JIT regions to read-execute (R-X).
 * HotSpot's CodeHeap::allocate then tries to write JIT stubs into that R-X
 * region and receives SIGBUS (BUS_ADRALN).
 *
 * Fix: install a SIGBUS handler that calls pthread_jit_write_protect_np(0)
 * to re-enable writes on the JIT region; the kernel then retries the
 * faulting store instruction.
 *
 * Usage: DYLD_INSERT_LIBRARIES=/path/to/libjvm_sigbus_fix.dylib java ...
 */
#define _DARWIN_C_SOURCE
#include <signal.h>
#include <pthread.h>

extern void pthread_jit_write_protect_np(int enable);

static void jvm_sigbus_fix_handler(int sig, siginfo_t *info, void *ctx) {
    (void)sig;
    (void)info;
    (void)ctx;
    pthread_jit_write_protect_np(0);
}

__attribute__((constructor))
static void jvm_sigbus_fix_init(void) {
    struct sigaction sa;
    sa.sa_sigaction = jvm_sigbus_fix_handler;
    sa.sa_flags = SA_SIGINFO | SA_RESTART;
    sigemptyset(&sa.sa_mask);
    sigaction(SIGBUS, &sa, NULL);
    pthread_jit_write_protect_np(0);
}
