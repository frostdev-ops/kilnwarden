// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;

namespace Kilnwarden.Offline.Support;

/// <summary>Runs code with the x64 SSE "flush to zero" and "denormals are zero" bits set in MXCSR, as a game engine
/// sets them on its threads (Enshrouded's tick thread, where Kiln reloads mods). With DAZ on, a subnormal operand such
/// as double.Epsilon reads as 0. MXCSR is per thread: the caller must stay on this thread (no await) inside.</summary>
internal static class FloatMode
{
    public const uint FlushToZero = 0x8000, DenormalsAreZero = 0x0040;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint SetMxcsr(uint value);

    private static readonly Lazy<SetMxcsr?> Setter = new(Make);

    /// <summary>False where it cannot run (not x64): tests then check nothing extra.</summary>
    public static bool Supported => Setter.Value is not null;

    public static T WithDenormalsFlushed<T>(Func<T> f)
    {
        var set = Setter.Value ?? throw new PlatformNotSupportedException("MXCSR needs x64");
        var old = set(0x1F80); // the default (all exceptions masked), only to read the current value
        set(old | FlushToZero | DenormalsAreZero);
        try
        {
            return f();
        }
        finally
        {
            set(old);
        }
    }

    // set(value): stmxcsr old; ldmxcsr value; return old. SysV x64 (value in edi, red zone below rsp) or
    // Windows x64 (value in ecx, the caller's home space above the return address).
    private static readonly byte[] SysV =
    [
        0x0F, 0xAE, 0x5C, 0x24, 0xF8, // stmxcsr [rsp-8]
        0x8B, 0x44, 0x24, 0xF8,       // mov eax, [rsp-8]
        0x89, 0x7C, 0x24, 0xFC,       // mov [rsp-4], edi
        0x0F, 0xAE, 0x54, 0x24, 0xFC, // ldmxcsr [rsp-4]
        0xC3,                         // ret
    ];

    private static readonly byte[] Win64 =
    [
        0x0F, 0xAE, 0x5C, 0x24, 0x08, // stmxcsr [rsp+8]
        0x8B, 0x44, 0x24, 0x08,       // mov eax, [rsp+8]
        0x89, 0x4C, 0x24, 0x10,       // mov [rsp+16], ecx
        0x0F, 0xAE, 0x54, 0x24, 0x10, // ldmxcsr [rsp+16]
        0xC3,                         // ret
    ];

    private static SetMxcsr? Make()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64) return null;
        var code = OperatingSystem.IsWindows() ? Win64 : SysV;
        nint mem;
        if (OperatingSystem.IsWindows()) mem = VirtualAlloc(0, 4096, 0x3000, 0x40);
        else if (OperatingSystem.IsLinux()) { mem = mmap(0, 4096, 7, 0x22, -1, 0); if (mem == -1) mem = 0; }
        else return null;
        if (mem == 0) return null;
        Marshal.Copy(code, 0, mem, code.Length);
        return Marshal.GetDelegateForFunctionPointer<SetMxcsr>(mem);
    }

    [DllImport("kernel32", SetLastError = true)]
    private static extern nint VirtualAlloc(nint address, nuint size, uint type, uint protect);

    [DllImport("libc", SetLastError = true)]
    private static extern nint mmap(nint address, nuint length, int prot, int flags, int fd, nint offset);
}
