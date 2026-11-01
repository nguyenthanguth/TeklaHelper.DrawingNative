// =====================================================================================
// TeklaHelper.DrawingNative - DgMetadata: the D<file guid>.dg.metadata file saved next to every drawing file version
// =====================================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace TeklaHelper.DrawingNative.DrawingFile
{
    /// <summary>
    /// Metadata of one saved drawing file version (&lt;DrawingVersionMetadata&gt; in D&lt;file guid&gt;.dg.metadata).
    /// </summary>
    /// <remarks>
    /// Every save writes a new D&lt;random guid&gt;.dg and keeps the older versions, so the file name says nothing about
    /// the drawing: the drawing GUID is <see cref="DrawingGuid"/>. Use <see cref="FindLatest"/> to get the newest version
    /// of a drawing.
    /// </remarks>
    public sealed class DgMetadata
    {
        private const string MetadataExtension = ".metadata";
        private const string MetadataSearchPattern = "*.dg.metadata";

        private DgMetadata(string metadataPath, XElement root)
        {
            MetadataPath = metadataPath;
            DrawingFilePath = metadataPath.Substring(0, metadataPath.Length - MetadataExtension.Length);

            Guid drawingGuid;
            Guid.TryParse(Value(root, "DrawingGuid"), out drawingGuid);
            DrawingGuid = drawingGuid;
            Name = Value(root, "Name");
            Mark = Value(root, "Mark");
            DrawingType = (int)Number(root, "DrawingType");

            // Seconds since 1970 of the local wall-clock time (not UTC).
            ModifyTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local).AddSeconds(Number(root, "ModifyDate"));
        }

        /// <summary>Path of the .dg.metadata file.</summary>
        public string MetadataPath { get; private set; }

        /// <summary>Path of the drawing file version (.dg) this metadata describes.</summary>
        public string DrawingFilePath { get; private set; }

        /// <summary>GUID of the drawing (Drawing.GetIdentifier().GUID in the Open API); <see cref="Guid.Empty"/> if missing.</summary>
        public Guid DrawingGuid { get; private set; }

        /// <summary>Drawing name, for example "FOOTING".</summary>
        public string Name { get; private set; }

        /// <summary>Drawing mark, for example "[25]".</summary>
        public string Mark { get; private set; }

        /// <summary>Drawing type code.</summary>
        public int DrawingType { get; private set; }

        /// <summary>Local time the drawing was saved (ModifyDate).</summary>
        public DateTime ModifyTime { get; private set; }

        /// <summary>
        /// Reads one .dg.metadata file.
        /// </summary>
        /// <param name="metadataPath">Path of the D&lt;file guid&gt;.dg.metadata file.</param>
        /// <exception cref="ArgumentNullException"><paramref name="metadataPath"/> is null.</exception>
        /// <exception cref="System.Xml.XmlException">The file is not valid XML.</exception>
        public static DgMetadata Load(string metadataPath)
        {
            if (metadataPath == null) throw new ArgumentNullException("metadataPath");
            return new DgMetadata(metadataPath, XDocument.Load(metadataPath).Root);
        }

        /// <summary>
        /// Reads every .dg.metadata file of a drawings folder; unreadable files are skipped.
        /// </summary>
        /// <param name="drawingsDirectory">The &lt;model&gt;\drawings folder.</param>
        /// <exception cref="ArgumentNullException"><paramref name="drawingsDirectory"/> is null.</exception>
        public static List<DgMetadata> LoadAll(string drawingsDirectory)
        {
            if (drawingsDirectory == null) throw new ArgumentNullException("drawingsDirectory");

            List<DgMetadata> result = new List<DgMetadata>();
            if (!Directory.Exists(drawingsDirectory)) return result;

            foreach (string path in Directory.GetFiles(drawingsDirectory, MetadataSearchPattern))
            {
                try
                {
                    result.Add(Load(path));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException)
                {
                    // A file being written or a foreign file: not a usable version.
                }
            }
            return result;
        }

        /// <summary>
        /// Finds the newest saved version of a drawing whose .dg file exists.
        /// </summary>
        /// <param name="drawingsDirectory">The &lt;model&gt;\drawings folder.</param>
        /// <param name="drawingGuid">GUID of the drawing (Drawing.GetIdentifier().GUID in the Open API).</param>
        /// <returns>Metadata of the newest version, or null if the drawing has no saved file.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="drawingsDirectory"/> is null.</exception>
        public static DgMetadata FindLatest(string drawingsDirectory, Guid drawingGuid)
        {
            DgMetadata latest = null;
            foreach (DgMetadata metadata in LoadAll(drawingsDirectory))
            {
                if (metadata.DrawingGuid != drawingGuid || !File.Exists(metadata.DrawingFilePath)) continue;
                if (latest == null || IsNewer(metadata, latest)) latest = metadata;
            }
            return latest;
        }

        private static bool IsNewer(DgMetadata candidate, DgMetadata current)
        {
            if (candidate.ModifyTime != current.ModifyTime) return candidate.ModifyTime > current.ModifyTime;
            return File.GetLastWriteTimeUtc(candidate.DrawingFilePath) > File.GetLastWriteTimeUtc(current.DrawingFilePath);
        }

        private static string Value(XElement root, string name)
        {
            XElement element = root == null ? null : root.Element(name);
            return element == null ? string.Empty : element.Value.Trim();
        }

        private static long Number(XElement root, string name)
        {
            long value;
            return long.TryParse(Value(root, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        /// <summary>
        /// Returns a short description: name, mark, drawing GUID, save time and file name.
        /// </summary>
        public override string ToString()
        {
            return "DgMetadata(" + Name + " " + Mark + ", DrawingGuid=" + DrawingGuid + ", ModifyTime=" +
                   ModifyTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + ", File=" + Path.GetFileName(DrawingFilePath) + ")";
        }
    }
}
