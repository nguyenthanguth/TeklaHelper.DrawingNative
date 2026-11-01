// =====================================================================================
// TeklaHelper.DrawingNative - DrawingObjectTable: reads the gr_object table of the drawing database
// Same approach as the internal Drawings.dll code FUN_1800cdc00 (grdb_object.cpp): a cursor on gr_object_belongs_to_idx.
// =====================================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TeklaHelper.DrawingNative.Database
{
    /// <summary>
    /// Queries the object table gr_object (table 0x130) of the drawing editor database.
    /// Use only inside the Tekla process while a drawing is open.
    /// </summary>
    internal static class DrawingObjectTable
    {
        // Virtual database name of the drawing editor (string found in Drawings.dll).
        private const string DrawingDatabaseName = "drawings_editor_db";

        private const int GrObjectTable = 0x130;
        private const string BelongsToIndex = "gr_object_belongs_to_idx";

        // gr_object row layout: +0x0 GR_O_* type, +0x4 ID, +0xC belongs_to (owning plugin).
        private const int RowIdOffset = 0x4;
        private const int RowBelongsToOffset = 0xC;

        // Row buffers are oversized for safety in case the actual row length is larger.
        private const int MinRowBuffer = 0x100;

        /// <summary>
        /// IDs of the objects whose belongs_to == <paramref name="ownerId"/>.
        /// </summary>
        /// <param name="ownerId">ID of the owning object (a plugin).</param>
        /// <exception cref="InvalidOperationException">The drawing database was not found or the cursor could not be opened.</exception>
        internal static List<int> GetIdsBelongingTo(int ownerId)
        {
            List<int> ids = new List<int>();
            if (ownerId <= 0) return ids;
            NativeHost.EnsureInsideTekla();

            IntPtr ctx, index;
            if (!TryGetBelongsToIndex(out ctx, out index))
            {
                throw new InvalidOperationException("Index " + BelongsToIndex + " was not found in the drawing database.");
            }

            // dbv_fetch_ctx writes a whole row into the buffer, so an unknown row length must not be guessed.
            int rowSize;
            if (DatabaseNativeMethods.dbd_getvirtualtablesizeof_ctx(ctx, GrObjectTable, out rowSize) != 0 ||
                rowSize < RowBelongsToOffset + sizeof(int))
            {
                throw new InvalidOperationException("Could not read a valid row length of table gr_object (0x" +
                                                    GrObjectTable.ToString("X") + ") from the drawing database.");
            }
            int bufferSize = Math.Max(rowSize, MinRowBuffer);

            IntPtr key = Marshal.AllocHGlobal(bufferSize);
            IntPtr row = Marshal.AllocHGlobal(bufferSize);
            try
            {
                Marshal.Copy(new byte[bufferSize], 0, key, bufferSize);
                Marshal.WriteInt32(key, RowBelongsToOffset, ownerId);

                IntPtr cursor;
                if (DatabaseNativeMethods.dbv_opencursor_ctx(index, key, key, out cursor) != 0 || cursor == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Could not open a cursor on " + BelongsToIndex + " for ID " + ownerId + ".");
                }

                try
                {
                    int status;
                    while (DatabaseNativeMethods.dbv_fetch_ctx(cursor, row, out status) == 0 && status == 0)
                    {
                        if (Marshal.ReadInt32(row, RowBelongsToOffset) == ownerId)
                        {
                            ids.Add(Marshal.ReadInt32(row, RowIdOffset));
                        }
                    }
                }
                finally
                {
                    DatabaseNativeMethods.dbv_closecursor_ctx(cursor);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(key);
                Marshal.FreeHGlobal(row);
            }
            return ids;
        }

        /// <summary>
        /// Finds a database that has the gr_object_belongs_to_idx index: "drawings_editor_db" first, then the current database.
        /// </summary>
        private static bool TryGetBelongsToIndex(out IntPtr ctx, out IntPtr index)
        {
            if (TryGetBelongsToIndex(DrawingDatabaseName, out ctx, out index)) return true;

            IntPtr currentName = DatabaseNativeMethods.dbd_getvirtualdatabase();
            string current = currentName == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(currentName);
            return !string.IsNullOrEmpty(current) && current != DrawingDatabaseName &&
                   TryGetBelongsToIndex(current, out ctx, out index);
        }

        /// <summary>
        /// Gets the context of <paramref name="databaseName"/> and its gr_object_belongs_to_idx index.
        /// </summary>
        private static bool TryGetBelongsToIndex(string databaseName, out IntPtr ctx, out IntPtr index)
        {
            index = IntPtr.Zero;
            return DatabaseNativeMethods.dbd_getvirtualdatabasebyname(databaseName, out ctx) == 0 && ctx != IntPtr.Zero &&
                   DatabaseNativeMethods.dbd_getvirtualindexpointer_ctx(ctx, GrObjectTable, BelongsToIndex, out index) == 0 &&
                   index != IntPtr.Zero;
        }
    }
}
