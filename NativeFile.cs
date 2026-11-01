// =====================================================================================
// TeklaHelper.DrawingNative - NativeFile: names of the native libraries shared by all P/Invoke declarations
// =====================================================================================

namespace TeklaHelper.DrawingNative
{
    /// <summary>
    /// Names of the Tekla Structures native libraries, used in [DllImport(NativeFile.DRAWINGS, ...)].
    /// </summary>
    internal static class NativeFile
    {
        /// <summary>Tekla Structures drawing library (&lt;Tekla install&gt;\bin\Drawings.dll).</summary>
        internal const string DRAWINGS = "Drawings.dll";

        /// <summary>Virtual database dictionary: databases, tables, indexes (dbd_*).</summary>
        internal const string DBDICT = "DBdict.dll";

        /// <summary>Virtual database engine: cursors, fetch (dbv_*).</summary>
        internal const string DBVIRTUAL = "DBvirtual.dll";
    }
}
