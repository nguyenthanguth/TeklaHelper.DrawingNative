// =====================================================================================
// TeklaHelper.DrawingNative - DgDrawingObject: one row of the gr_object table of a saved drawing file (.dg)
// =====================================================================================

namespace TeklaHelper.DrawingNative.DrawingFile
{
    /// <summary>
    /// One drawing object from the gr_object table (0x130).
    /// Row layout (0x18 bytes): +0x0 GR_O_* type, +0x4 ID, +0x8 World (view), +0xC belongs_to (owning plugin).
    /// </summary>
    public sealed class DgDrawingObject
    {
        internal DgDrawingObject(int type, int id, int world, int belongsTo)
        {
            Type = type;
            Id = id;
            World = world;
            BelongsTo = belongsTo;
        }

        /// <summary>
        /// GR_O_* object type code, for example 0x100 dimension, 0x101 dimension set, 0xFE mark, 0x116 line,
        /// 0x137 leader line, 0x131 plugin.
        /// </summary>
        public int Type { get; private set; }

        /// <summary>Drawing ID of the object.</summary>
        public int Id { get; private set; }

        /// <summary>World (view) the object belongs to.</summary>
        public int World { get; private set; }

        /// <summary>ID of the plugin that drew the object; 0 if it was not drawn by a plugin.</summary>
        public int BelongsTo { get; private set; }

        /// <summary>
        /// Returns a short description: type, ID, World and owning plugin.
        /// </summary>
        public override string ToString()
        {
            return "DgDrawingObject(Type=0x" + Type.ToString("X") + ", Id=" + Id + ", World=" + World + ", BelongsTo=" + BelongsTo + ")";
        }
    }
}
