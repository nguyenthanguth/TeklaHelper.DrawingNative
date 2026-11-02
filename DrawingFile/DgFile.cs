// =====================================================================================
// TeklaHelper.DrawingNative - DgFile: reads a saved drawing file (<model>\drawings\D<guid>.dg) without Tekla
// Format reverse engineered from dbv_readtable (DBvirtual.dll, FUN_18003ee20) and, for the older "Xsteel#" layout,
// dbv_read_domain / dbv_readtable_domain; see REVERSE-ENGINEERING.md.
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
    /// <para>
    /// Older versions write the domain layout, marked by '#' in the header
    /// ("Xsteel# 8.95"), taken from dbv_read_domain and dbv_readtable_domain:
    /// <code>
    /// "Xsteel#%5.2f"                   12-byte header
    /// uint32 domain count, domain header (marker 0xDBCEC0B9 + version, then one description per domain)
    /// table directory, then the tables of the directory entries that are not deleted, in reverse directory order
    /// </code>
    /// Every table is: marker 0xDBCEC063 + version, uint32 row length, uint32 n, n int32 domain numbers (the domain
    /// of the row identity, then one per field), then per row a state byte, the row bytes and the domain values,
    /// ending with a 0 state byte. The row bytes have the same layout in both formats.
    /// </para>
    /// </remarks>
    public sealed class DgFile
    {
        private const string HeaderPrefix = "Xsteel";
        private const int HeaderLength = 12;
        private const int MaxRowSize = 0x100000;

        // Domain layout ("Xsteel#"). Markers are a base plus the domain header version; only version 3 stores the
        // domain flags in the file.
        private const char DomainFormatMark = '#';
        private const uint DomainHeaderMarkerBase = 0xDBCEC0B9;
        private const uint DomainTableMarkerBase = 0xDBCEC063;
        private const int SupportedDomainVersion = 3;
        private const uint MaxDomainCount = 0x3FF;

        // Domain flags: kind (bits 0x83), 64-bit values, and the optional parts of a domain description.
        private const uint DomainKindMask = 0x83;
        private const uint DomainKnownFlags = 0xFFF;
        private const uint DomainRangesFlag = 0x4;
        private const uint DomainFreeListFlag = 0x10;
        private const uint DomainShortEntryFlag = 0x40;
        private const uint Domain64BitFlag = 0x400;
        private const uint DomainRunMapFlag = 0x800;
        private const int DomainGuidLength = 16;

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

            // "Xsteel" + one layout character (' ' or '#') + "%5.2f"; parsed before trimming so a blank version is safe.
            string rawHeader = Encoding.ASCII.GetString(data, 0, HeaderLength);
            string header = rawHeader.TrimEnd();
            double version;
            double.TryParse(rawHeader.Substring(HeaderPrefix.Length + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out version);

            if (rawHeader[HeaderPrefix.Length] == DomainFormatMark) return ParseDomainFormat(data, header, version);

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

        /// <summary>
        /// Parses the domain layout of older versions ("Xsteel#"): domain header, directory, then the tables of the
        /// directory entries that are not deleted, in reverse directory order (dbv_read_domain, dbv_readtable_domain).
        /// </summary>
        private static DgFile ParseDomainFormat(byte[] data, string header, double version)
        {
            int position = HeaderLength;
            uint domainCount = ReadUInt32(data, ref position);
            int domainVersion = (int)(ReadUInt32(data, ref position) - DomainHeaderMarkerBase);
            if (domainVersion != SupportedDomainVersion)
            {
                throw new InvalidDataException("Unsupported domain header version " + domainVersion + ".");
            }
            if (ReadUInt32(data, ref position) != 0 || ReadUInt32(data, ref position) != domainCount ||
                domainCount - 1 >= MaxDomainCount)
            {
                throw new InvalidDataException("Invalid domain header.");
            }

            uint[] domainFlags = new uint[domainCount];
            for (int i = 0; i < domainFlags.Length; i++) domainFlags[i] = SkipDomain(data, ref position);

            uint tableMarker = DomainTableMarkerBase + (uint)domainVersion;
            DgTable directory = ReadDomainTable(data, ref position, 0, tableMarker, domainFlags);
            if (directory.Rows.Count > 0 && directory.RowSize != sizeof(int))
            {
                throw new InvalidDataException("Unexpected table directory row length " + directory.RowSize + ".");
            }

            // Deleted directory entries (state 12) have no table.
            List<int> tableNumbers = new List<int>();
            foreach (DgRow row in directory.Rows)
            {
                if (!row.IsDeleted) tableNumbers.Add(row.ReadInt32(0));
            }

            Dictionary<int, DgTable> tables = new Dictionary<int, DgTable>();
            for (int i = tableNumbers.Count - 1; i >= 0; i--)
            {
                tables[tableNumbers[i]] = ReadDomainTable(data, ref position, tableNumbers[i], tableMarker, domainFlags);
            }

            return new DgFile(header, version, tableNumbers, tables);
        }

        /// <summary>
        /// Skips one domain description of the domain header and returns its flags.
        /// </summary>
        private static uint SkipDomain(byte[] data, ref int position)
        {
            uint flags = ReadUInt32(data, ref position);
            if ((flags & ~DomainKnownFlags) != 0 || (flags & DomainKindMask) == 1 || (flags & DomainRunMapFlag) != 0)
            {
                throw new InvalidDataException("Unsupported domain flags 0x" + flags.ToString("X") + ".");
            }

            // Identity entries: int32 key, int32 index (unless DomainShortEntryFlag), 16-byte GUID; entry 0 is not stored.
            uint entryCount = ReadUInt32(data, ref position);
            int entryLength = sizeof(int) + ((flags & DomainShortEntryFlag) == 0 ? sizeof(int) : 0) + DomainGuidLength;
            if (entryCount > 1) Skip(data, ref position, (entryCount - 1L) * entryLength);

            if ((flags & DomainFreeListFlag) != 0)
            {
                while (ReadUInt32(data, ref position) != 0) { }
            }
            if ((flags & DomainRangesFlag) != 0) Skip(data, ref position, ReadUInt32(data, ref position) * 16L);

            // Two domain values close the description.
            int valueLength = (flags & Domain64BitFlag) != 0 ? sizeof(long) : sizeof(int);
            Skip(data, ref position, 2 * (valueLength + DomainValueExtraLength(flags)));
            return flags;
        }

        /// <summary>
        /// Bytes stored after a domain value, by domain kind: none (1), 8 bytes (2) or a GUID (0x81, 0x82).
        /// </summary>
        private static int DomainValueExtraLength(uint flags)
        {
            switch (flags & DomainKindMask)
            {
                case 0x1: return 0;
                case 0x2: return sizeof(long);
                case 0x81:
                case 0x82: return DomainGuidLength;
                default: throw new InvalidDataException("Unsupported domain kind 0x" + (flags & DomainKindMask).ToString("X") + ".");
            }
        }

        /// <summary>
        /// Reads one table of the domain layout: marker, row length, domain numbers, then (state byte + row + domain
        /// values) per row until a 0 state byte.
        /// </summary>
        private static DgTable ReadDomainTable(byte[] data, ref int position, int number, uint marker, uint[] domainFlags)
        {
            string table = "table 0x" + number.ToString("X");
            if (ReadUInt32(data, ref position) != marker)
            {
                throw new InvalidDataException("Missing marker of " + table + ".");
            }

            uint rowSize = ReadUInt32(data, ref position);
            uint columnCount = ReadUInt32(data, ref position);
            if (rowSize > MaxRowSize || columnCount == 0 || columnCount * 4L > data.Length - position)
            {
                throw new InvalidDataException("Invalid header of " + table + ".");
            }

            // Column 0 is the domain of the row identity (two values, 64-bit if negative); the others are the fields.
            long valuesLength = 0;
            for (int column = 0; column < columnCount; column++)
            {
                int domain = (int)ReadUInt32(data, ref position);
                bool is64Bit = domain < 0;
                if (is64Bit && column > 0) throw new InvalidDataException("Invalid field domain in " + table + ".");
                if (is64Bit) domain = -domain;
                if (domain == 0) continue;
                if (domain < 0 || domain > domainFlags.Length) throw new InvalidDataException("Unknown domain in " + table + ".");

                int extraLength = DomainValueExtraLength(domainFlags[domain - 1]);
                valuesLength += column == 0 ? 2 * ((is64Bit ? sizeof(long) : sizeof(int)) + extraLength) : extraLength;
            }

            long rowLength = 1 + rowSize + valuesLength;
            List<DgRow> rows = new List<DgRow>();
            while (true)
            {
                if (position >= data.Length) throw new InvalidDataException("Drawing file is truncated inside " + table + ".");

                byte state = data[position];
                if (state == 0) break;
                if (state > 12 || state == 11 || rowLength > data.Length - position)
                {
                    throw new InvalidDataException("Invalid row in " + table + ".");
                }

                byte[] row = new byte[rowSize];
                Buffer.BlockCopy(data, position + 1, row, 0, (int)rowSize);
                rows.Add(new DgRow(state, row));
                position += (int)rowLength;
            }
            position++;

            return new DgTable(number, (int)rowSize, rows);
        }

        private static uint ReadUInt32(byte[] data, ref int position)
        {
            if (position > data.Length - sizeof(uint)) throw new InvalidDataException("Drawing file is truncated.");
            uint value = BitConverter.ToUInt32(data, position);
            position += sizeof(uint);
            return value;
        }

        private static void Skip(byte[] data, ref int position, long length)
        {
            if (length > data.Length - position) throw new InvalidDataException("Drawing file is truncated.");
            position += (int)length;
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
