// =====================================================================================
// TeklaHelper.DrawingNative - NativeHost: checks that the code is running inside the Tekla Structures process
// =====================================================================================

using System;
using System.Runtime.InteropServices;

namespace TeklaHelper.DrawingNative
{
    /// <summary>
    /// Guards native calls: Drawings.dll / DBdict.dll only hold drawing data inside TeklaStructures.exe.
    /// In an external process (console, WinForms...) P/Invoke loads a fresh, empty copy of those DLLs and the first
    /// call raises an AccessViolationException (not catchable with try/catch on .NET Framework), so it must be blocked first.
    /// </summary>
    internal static class NativeHost
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string procName);

        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> if Drawings.dll has not already been loaded by Tekla
        /// in the current process.
        /// </summary>
        internal static void EnsureInsideTekla()
        {
            if (GetModuleHandle(NativeFile.DRAWINGS) == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "TeklaHelper.DrawingNative only works inside the Tekla Structures process (macros, plugins). " +
                    "The current process has not loaded Tekla's " + NativeFile.DRAWINGS + ", so no drawing data is available.");
            }
        }

        /// <summary>
        /// Whether the Drawings.dll loaded by Tekla exports <paramref name="exportName"/> (exact decorated name).
        /// Throws <see cref="InvalidOperationException"/> outside the Tekla process, like <see cref="EnsureInsideTekla"/>.
        /// </summary>
        /// <param name="exportName">Decorated (mangled) export name.</param>
        internal static bool HasExport(string exportName)
        {
            EnsureInsideTekla();
            return GetProcAddress(GetModuleHandle(NativeFile.DRAWINGS), exportName) != IntPtr.Zero;
        }
    }
}
