// =====================================================================================
// TeklaHelper.DrawingNative - StdVectorInt: std::vector<int> returned by value from Drawings.dll
// =====================================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TeklaHelper.DrawingNative
{
    /// <summary>
    /// MSVC x64 layout of std::vector&lt;int&gt; (First / Last / End), used as a P/Invoke return type.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct StdVectorInt
    {
        /// <summary>Pointer to the first element.</summary>
        public IntPtr First;

        /// <summary>Pointer one past the last element.</summary>
        public IntPtr Last;

        /// <summary>Pointer one past the end of the allocated storage.</summary>
        public IntPtr End;

        // Drawings.dll uses the Universal CRT: its operator delete is free() in ucrtbase.dll,
        // so memory of a std::vector allocated by Drawings.dll can be released with this very function.
        [DllImport("ucrtbase.dll", EntryPoint = "free", CallingConvention = CallingConvention.Cdecl)]
        private static extern void CrtFree(IntPtr block);

        // The Universal CRT allocates from the process heap (Windows 10+), so a block it owns passes HeapValidate.
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcessHeap();

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool HeapValidate(IntPtr heap, uint flags, IntPtr block);

        /// <summary>
        /// Reads the elements, then frees the vector memory. Call only once per vector returned by native code.
        /// </summary>
        public List<int> ToListAndFree()
        {
            List<int> result = new List<int>();
            if (First == IntPtr.Zero) return result;

            long count = (Last.ToInt64() - First.ToInt64()) / 4;
            for (int i = 0; i < count; i++)
            {
                result.Add(Marshal.ReadInt32(First, i * 4));
            }

            // MSVC std::allocator: blocks of 0x1000 bytes or more are 32-byte aligned and
            // the original malloc pointer is stored right before First (same as the deallocate code in Drawings.dll).
            long capacityBytes = End.ToInt64() - First.ToInt64();
            IntPtr block = capacityBytes >= 0x1000 ? Marshal.ReadIntPtr(First, -8) : First;

            // If Tekla ever allocates from another heap (custom operator new, static CRT, debug allocator),
            // leaking the block is far safer than corrupting the heap.
            if (HeapValidate(GetProcessHeap(), 0, block)) CrtFree(block);

            First = Last = End = IntPtr.Zero;
            return result;
        }
    }
}
