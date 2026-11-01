// =====================================================================================
// TeklaHelper.DrawingNative - DgFile: reads a saved drawing file (<model>\drawings\D<guid>.dg) without Tekla
// Format reverse engineered from dbv_readtable (DBvirtual.dll, FUN_18003ee20); see REVERSE-ENGINEERING.md.
// =====================================================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace TeklaHelper.DrawingNative.DrawingFile
{
    /// <summary>
    /// Read-only view of a saved drawing file (.dg): the drawing database as it was when the drawing was last saved.
    /// Pure managed code: works in any process, with or without Tekla, but does not see unsaved changes.
    /// </summary>
    /// <remarks>
    /// Layout of the (gzip-compressed) file:
    /// <code>
    /// "Xsteel %5.2f"                   12-byte header, for example "Xsteel  9.54"
    /// table directory                  a table whose 4-byte rows are the table numbers
    /// one table per directory entry    in reverse directory order
    /// </code>
    /// Every table is: uint32 row count, uint32 row length, then per row one state byte followed by the row bytes.
    /// </remarks>
    public sealed class DgFile
    {
        private const string HeaderPrefix = "Xsteel";
        private const int HeaderLength = 12;
        private const int MaxRowSize = 0x100000;

        // gr_object row: +0x0 GR_O_* type, +0x4 ID, +0x8 World, +0xC belongs_to.
        private const int GrObjectTable = 0x130;
        private const int RowTypeOffset = 0x0;
        private const int RowIdOffset = 0x4;
        private const int RowWorldOffset = 0x8;
        private const int RowBelongsToOffset = 0xC;

        // Dimension row: +0x4 ID, +0xC ID of the dimension set that contains the dimension (index gr_dim_pid_idx).
        private const int DimensionTable = 0x100;
        private const int DimensionSetOffset = 0xC;

        private readonly Dictionary<int, DgTable> _tables;

        private DgFile(string header, double version, IList<int> tableNumbers, Dictionary<int, DgTable> tables)
        {
            Header = header;
            Version = version;
            TableNumbers = new ReadOnlyCollection<int>(tableNumbers);
            _tables = tables;
        }

        #region Loading

        /// <summary>
        /// Loads a drawing file, compressed (as saved by Tekla) or already decompressed.
        /// </summary>
        /// <param name="path">Path of the .dg file, usually &lt;model&gt;\drawings\D&lt;guid&gt;.dg.</param>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
        /// <exception cref="InvalidDataException">The file is not a drawing file or is truncated.</exception>
        public static DgFile Load(string path)
        {
            if (path == null) throw new ArgumentNullException("path");
            using (FileStream stream = File.OpenRead(path))
            {
                return Load(stream);
            }
        }

        /// <summary>
        /// Loads a drawing file from a stream, compressed (as saved by Tekla) or already decompressed.
        /// </summary>
        /// <param name="stream">Stream positioned at the start of the file; it is read to the end but not closed.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        /// <exception cref="InvalidDataException">The data is not a drawing file or is truncated.</exception>
        public static DgFile Load(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            return Parse(Decompress(ReadAll(stream)));
        }

        private static byte[] ReadAll(Stream stream)
        {
            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                return buffer.ToArray();
            }
        }

        private static byte[] Decompress(byte[] data)
        {
            bool isGzip = data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B;
            if (!isGzip) return data;

            using (GZipStream gzip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress))
            {
                return ReadAll(gzip);
            }
        }

        private static DgFile Parse(byte[] data)
        {
            if (data.Length < HeaderLength || Encoding.ASCII.GetString(data, 0, HeaderPrefix.Length) != HeaderPrefix)
            {
                throw new InvalidDataException("Not a Tekla drawing file: the '" + HeaderPrefix + "' header is missing.");
            }

            string header = Encoding.ASCII.GetString(data, 0, HeaderLength).TrimEnd();
            double version;
            double.TryParse(header.Substring(HeaderPrefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out version);

            int position = HeaderLength;
            DgTable directory = ReadTable(data, ref position, 0);
            if (directory.Rows.Count > 0 && directory.RowSize != sizeof(int))
            {
                throw new InvalidDataException("Unexpected table directory row length " + directory.RowSize + ".");
            }

            List<int> tableNumbers = new List<int>();
            foreach (DgRow row in directory.Rows) tableNumbers.Add(row.ReadInt32(0));

            // The tables follow in reverse directory order.
            Dictionary<int, DgTable> tables = new Dictionary<int, DgTable>();
            for (int i = tableNumbers.Count - 1; i >= 0; i--)
            {
                tables[tableNumbers[i]] = ReadTable(data, ref position, tableNumbers[i]);
            }

            return new DgFile(header, version, tableNumbers, tables);
        }

        /// <summary>
        /// Reads one table: uint32 row count, uint32 row length, then (state byte + row) per row.
        /// </summary>
        private static DgTable ReadTable(byte[] data, ref int position, int number)
        {
            if (position + 8 > data.Length)
            {
                throw new InvalidDataException("Drawing file is truncated before table 0x" + number.ToString("X") + ".");
            }

            uint rowCount = BitConverter.ToUInt32(data, position);
            uint rowSize = BitConverter.ToUInt32(data, position + 4);
            position += 8;

            long tableBytes = rowCount * (1L + rowSize);
            if (rowSize > MaxRowSize || tableBytes > data.Length - position)
            {
                throw new InvalidDataException("Table 0x" + number.ToString("X") + " (" + rowCount + " rows of " + rowSize +
                                               " bytes) does not fit in the drawing file.");
            }

            List<DgRow> rows = new List<DgRow>((int)rowCount);
            for (uint i = 0; i < rowCount; i++)
            {
                // DBvirtual.dll accepts states 1..10 only.
                byte state = data[position];
                if (state < 1 || state > 10)
                {
                    throw new InvalidDataException("Invalid row state " + state + " in table 0x" + number.ToString("X") + ".");
                }

                byte[] row = new byte[rowSize];
                Buffer.BlockCopy(data, position + 1, row, 0, (int)rowSize);
                rows.Add(new DgRow(state, row));
                position += 1 + (int)rowSize;
            }

            return new DgTable(number, (int)rowSize, rows);
        }

        #endregion

        #region Raw tables

        /// <summary>File header, for example "Xsteel  9.54".</summary>
        public string Header { get; private set; }

        /// <summary>Database version from the header, for example 9.54; 0 if it cannot be parsed.</summary>
        public double Version { get; private set; }

        /// <summary>Numbers of the tables stored in the file, in directory order.</summary>
        public ReadOnlyCollection<int> TableNumbers { get; private set; }

        /// <summary>
        /// Gets a table by number.
        /// </summary>
        /// <param name="tableNumber">Table number, for example 0x130 for gr_object.</param>
        /// <returns>The table, or null if the file does not contain it.</returns>
        public DgTable GetTable(int tableNumber)
        {
            DgTable table;
            return _tables.TryGetValue(tableNumber, out table) ? table : null;
        }

        #endregion

        #region Drawing objects

        /// <summary>
        /// Every drawing object of the gr_object table (0x130), deleted rows excluded.
        /// </summary>
        public List<DgDrawingObject> GetObjects()
        {
            List<DgDrawingObject> objects = new List<DgDrawingObject>();
            DgTable table = GetTable(GrObjectTable);
            if (table == null || table.RowSize < RowBelongsToOffset + sizeof(int)) return objects;

            foreach (DgRow row in table.Rows)
            {
                if (row.IsDeleted) continue;
                objects.Add(new DgDrawingObject(row.ReadInt32(RowTypeOffset), row.ReadInt32(RowIdOffset),
                                                row.ReadInt32(RowWorldOffset), row.ReadInt32(RowBelongsToOffset)));
            }
            return objects;
        }

        /// <summary>
        /// IDs of the objects drawn by a plugin (belongs_to == <paramref name="pluginId"/>), as saved in the file.
        /// Same result as <see cref="Plugin.DrawingNativePlugin.GetChildIds"/> for a drawing without unsaved changes.
        /// </summary>
        /// <param name="pluginId">Drawing ID of the plugin.</param>
        /// <returns>IDs of the drawn objects, including the dimensions inside a dimension set; empty if none.</returns>
        public List<int> GetChildIds(int pluginId)
        {
            List<int> ids = new List<int>();
            if (pluginId <= 0) return ids;

            foreach (DgDrawingObject drawingObject in GetObjects())
            {
                if (drawingObject.BelongsTo == pluginId) ids.Add(drawingObject.Id);
            }
            return ids;
        }

        /// <summary>
        /// IDs of the dimensions inside a dimension set, read from the dimension table (0x100, +0xC = set ID).
        /// </summary>
        /// <param name="dimensionSetId">Drawing ID of the dimension set.</param>
        /// <returns>IDs of the dimensions of the set; empty if none.</returns>
        public List<int> GetDimensionIds(int dimensionSetId)
        {
            List<int> ids = new List<int>();
            DgTable table = GetTable(DimensionTable);
            if (dimensionSetId <= 0 || table == null || table.RowSize < DimensionSetOffset + sizeof(int)) return ids;

            foreach (DgRow row in table.Rows)
            {
                if (!row.IsDeleted && row.ReadInt32(DimensionSetOffset) == dimensionSetId) ids.Add(row.ReadInt32(RowIdOffset));
            }
            return ids;
        }

        #endregion
    }
}
