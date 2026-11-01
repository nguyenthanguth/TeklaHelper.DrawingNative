// =====================================================================================
// TeklaHelper.DrawingNative - RelationType: grdbRelation_c::Type_e of Drawings.dll
// =====================================================================================
// Source (REA + Ghidra 12.1.4, Drawings.dll SHA-256 c5ab9d73...6186b):
//   - Value list: relation type validation function FUN_1800c9740 (grdb_relation.cpp:0x268) - any other value
//     asserts "Unknown relation type". The list is complete for the DLL version above.
//   - Values < 100000: same as drawing object type codes GR_O_* (name table PTR_s_GR_O_VIEW, index = code - 0xFB).
//     Type codes were cross-checked against the object table of a .dg file: 0x100 DIMENSION, 0x101 DIM_SET,
//     0x103 DRAWING_PART, 0x104 DRAWING_VIEWS, 0x131 PLUGIN.
//   - 100002..100009: grclaPluginInput_c::DBInsert / DBSelect / IsPointInput / IsObjectInput.
//   - Names were chosen during reverse engineering (Drawings.dll ships without a PDB); "inferred" entries are uncertain.
// =====================================================================================

namespace TeklaHelper.DrawingNative
{
    /// <summary>
    /// Relation type in the drawing database (grdbRelation_c::Type_e), used by GetRelationChildIds /
    /// GetRelationFatherIds / AddRelation and by the RelationType field of a plugin input.
    /// </summary>
    public enum RelationType
    {
        /// <summary>No filter: every relation type (used for queries).</summary>
        All = 0,

        #region Relations to drawing objects (value = GR_O_* type code)

        /// <summary>GR_O_DRAWING_PART (0x103): model object shown in the drawing (part, rebar...). Keyed by model object.</summary>
        DrawingPart = 0x103,

        /// <summary>GR_O_DRAWING_VIEWS (0x104): drawing view.</summary>
        DrawingView = 0x104,

        /// <summary>GR_O_MARK (0x108): mark.</summary>
        Mark = 0x108,

        /// <summary>GR_O_WELD (0x115): weld.</summary>
        Weld = 0x115,

        /// <summary>GR_O_FOREIGN_OBJECT (0x117): foreign object (DWG/DXF, link...). AddRelation uses this value directly.</summary>
        ForeignObject = 0x117,

        /// <summary>GR_O_STRING (0x125).</summary>
        String = 0x125,

        /// <summary>GR_O_BOLT_GROUP (0x126): bolt group. Keyed by model object.</summary>
        BoltGroup = 0x126,

        /// <summary>GR_O_FILTER (0x12D).</summary>
        Filter = 0x12D,

        /// <summary>GR_O_LEADERLINE (0x137): leader line.</summary>
        LeaderLine = 0x137,

        #endregion

        #region Extended relations (>= 100000)

        /// <summary>
        /// 100000 - inferred: link to a model object. gr_dim_edit.cpp copies children of this type from the old
        /// DimensionSet to the new one when a dimension point is added. Keyed by model object.
        /// </summary>
        ModelObjectLink = 100000,

        /// <summary>100001 - inferred: link from a view to a model object (only seen with view objects). Keyed by model object.</summary>
        ViewModelObjectLink = 100001,

        /// <summary>100002: plugin point input with 1 point.</summary>
        PluginInputPoint1 = 100002,

        /// <summary>100003: plugin point input with 2 points.</summary>
        PluginInputPoint2 = 100003,

        /// <summary>100004: plugin point input with 3 points.</summary>
        PluginInputPoint3 = 100004,

        /// <summary>100005: plugin point input with 4 or more points.</summary>
        PluginInputPointMany = 100005,

        /// <summary>100006: plugin model object input.</summary>
        PluginInputObject = 100006,

        /// <summary>100008: plugin input → World (view) of the input.</summary>
        PluginInputWorld = 100008,

        /// <summary>100009: plugin input → list of anchored points.</summary>
        PluginInputAnchoredPoints = 100009,

        /// <summary>100010 - meaning unknown; keyed by model object (seen in mark/dimension code).</summary>
        Unknown100010 = 100010,

        /// <summary>100011 - meaning unknown; keyed by model object (seen in mark/dimension code).</summary>
        Unknown100011 = 100011,

        /// <summary>150684 (0x24C9C) - only seen in validation and in the conversion of legacy relation tables (dr_db_conversions.cpp).</summary>
        Legacy150684 = 150684,

        #endregion
    }

    /// <summary>
    /// Helpers for <see cref="RelationType"/>.
    /// </summary>
    public static class RelationTypeExtensions
    {
        /// <summary>Whether the value is a plugin point input relation (100002..100005).</summary>
        /// <param name="type">Relation type to test.</param>
        public static bool IsPluginInputPoint(this RelationType type)
        {
            return type >= RelationType.PluginInputPoint1 && type <= RelationType.PluginInputPointMany;
        }

        /// <summary>Whether the value is in the list of valid Type_e values of Drawings.dll.</summary>
        /// <param name="type">Relation type to test.</param>
        public static bool IsDefined(this RelationType type)
        {
            return System.Enum.IsDefined(typeof(RelationType), type);
        }
    }
}
