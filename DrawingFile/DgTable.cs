// =====================================================================================
// TeklaHelper.DrawingNative - DgTable: one database table stored in a saved drawing file (.dg)
// =====================================================================================

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeklaHelper.DrawingNative.DrawingFile
{
    /// <summary>
    /// One table of the drawing database as written by dbv_writetable (DBvirtual.dll):
    /// a header (row count, row length) followed by the rows, or in the older domain layout by dbv_writetable_domain.
    /// </summary>
    public sealed class DgTable
    {
        internal DgTable(int number, int rowSize, IList<DgRow> rows)
        {
            Number = number;
            RowSize = rowSize;
            Rows = new ReadOnlyCollection<DgRow>(rows);
        }

        /// <summary>Table number, for example 0x130 for gr_object or 0x100 for dimensions.</summary>
        public int Number { get; private set; }

        /// <summary>Row length in bytes, as stored in the file.</summary>
        public int RowSize { get; private set; }

        /// <summary>Every row in file order, including rows marked as deleted (see <see cref="DgRow.IsDeleted"/>).</summary>
        public ReadOnlyCollection<DgRow> Rows { get; private set; }
    }
}
