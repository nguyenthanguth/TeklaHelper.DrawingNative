// =====================================================================================
// TeklaHelper.DrawingNative - DgRow: one row of a table stored in a saved drawing file (.dg)
// =====================================================================================

using System;

namespace TeklaHelper.DrawingNative.DrawingFile
{
    /// <summary>
    /// One row of a <see cref="DgTable"/>: a state byte followed by the raw row bytes, exactly as written by DBvirtual.dll.
    /// </summary>
    public sealed class DgRow
    {
        private readonly byte[] _data;

        internal DgRow(byte state, byte[] data)
        {
            State = state;
            _data = data;
        }

        /// <summary>
        /// Row state byte (1..10). DBvirtual.dll skips rows in state 6, 8 or 10 when it loads a table (see <see cref="IsDeleted"/>).
        /// </summary>
        public byte State { get; private set; }

        /// <summary>
        /// Whether DBvirtual.dll ignores this row when loading the table (state 6, 8 or 10).
        /// </summary>
        public bool IsDeleted
        {
            get { return State == 6 || State == 8 || State == 10; }
        }

        /// <summary>Row length in bytes.</summary>
        public int Length
        {
            get { return _data.Length; }
        }

        /// <summary>Copy of the raw row bytes.</summary>
        public byte[] GetBytes()
        {
            return (byte[])_data.Clone();
        }

        /// <summary>Reads a little-endian 32-bit integer at <paramref name="offset"/>.</summary>
        /// <param name="offset">Byte offset inside the row.</param>
        /// <exception cref="ArgumentOutOfRangeException">The value does not fit inside the row.</exception>
        public int ReadInt32(int offset)
        {
            CheckRange(offset, sizeof(int));
            return BitConverter.ToInt32(_data, offset);
        }

        /// <summary>Reads a little-endian 64-bit floating point value at <paramref name="offset"/>.</summary>
        /// <param name="offset">Byte offset inside the row.</param>
        /// <exception cref="ArgumentOutOfRangeException">The value does not fit inside the row.</exception>
        public double ReadDouble(int offset)
        {
            CheckRange(offset, sizeof(double));
            return BitConverter.ToDouble(_data, offset);
        }

        private void CheckRange(int offset, int size)
        {
            if (offset < 0 || offset + size > _data.Length)
            {
                throw new ArgumentOutOfRangeException("offset", "Offset " + offset + " is outside a row of " + _data.Length + " bytes.");
            }
        }
    }
}
