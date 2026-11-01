// =====================================================================================
// TeklaHelper.DrawingNative - DatabaseNativeMethods: P/Invoke declarations for the C API of DBdict.dll / DBvirtual.dll
// Signatures reverse engineered with REA + Ghidra 12.1.4 (REA\Cpp\dbv_cursor_api_decompiled.cpp).
// =====================================================================================

using System;
using System.Runtime.InteropServices;

namespace TeklaHelper.DrawingNative.Database
{
    /// <summary>
    /// P/Invoke declarations for the Tekla virtual database engine (hidden from users).
    /// Every function returns 0 on success.
    /// </summary>
    internal static class DatabaseNativeMethods
    {
        #region DBdict.dll

        /// <summary>Name of the current virtual database, or NULL.</summary>
        [DllImport(NativeFile.DBDICT, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr dbd_getvirtualdatabase();

        /// <summary>Gets the context of a virtual database by name (name shorter than 32 characters).</summary>
        [DllImport(NativeFile.DBDICT, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false)]
        internal static extern int dbd_getvirtualdatabasebyname(string name, out IntPtr ctx);

        /// <summary>Gets an index pointer by table number and index name.</summary>
        [DllImport(NativeFile.DBDICT, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false)]
        internal static extern int dbd_getvirtualindexpointer_ctx(IntPtr ctx, int table, string indexName, out IntPtr index);

        /// <summary>Row length of a table, in bytes.</summary>
        [DllImport(NativeFile.DBDICT, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int dbd_getvirtualtablesizeof_ctx(IntPtr ctx, int table, out int size);

        #endregion

        #region DBvirtual.dll

        /// <summary>Opens a cursor on an index for the key range [keyFrom, keyTo] (a key is a table row).</summary>
        [DllImport(NativeFile.DBVIRTUAL, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int dbv_opencursor_ctx(IntPtr index, IntPtr keyFrom, IntPtr keyTo, out IntPtr cursor);

        /// <summary>Reads the next row; a row was read when the function returns 0 and status == 0.</summary>
        [DllImport(NativeFile.DBVIRTUAL, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int dbv_fetch_ctx(IntPtr cursor, IntPtr row, out int status);

        /// <summary>Closes a cursor opened by <see cref="dbv_opencursor_ctx"/>.</summary>
        [DllImport(NativeFile.DBVIRTUAL, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int dbv_closecursor_ctx(IntPtr cursor);

        #endregion
    }
}
