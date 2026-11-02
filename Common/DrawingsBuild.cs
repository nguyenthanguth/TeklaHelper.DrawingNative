// =====================================================================================
// TeklaHelper.DrawingNative - DrawingsBuild: differences between the Drawings.dll builds of the Tekla versions
// =====================================================================================

using System;

namespace TeklaHelper.DrawingNative
{
    /// <summary>
    /// Detects which Drawings.dll build Tekla has loaded, for the parts of the native API whose layout differs between
    /// Tekla versions (see REVERSE-ENGINEERING.md, "Tekla Structures 2020").
    /// </summary>
    /// <remarks>
    /// Tekla Structures 2020 uses Tekla's own array type <c>toolArray_c</c> where 2025 and 2026 use <c>std::vector</c>:
    /// <list type="bullet">
    /// <item>grclaPluginInput_c is 0x50 bytes instead of 0x48: Points is a 0x20-byte toolArray_c&lt;Vector_c&gt; (data pointer
    /// at 0x00, int count at 0x10) and every later field sits 8 bytes further (ObjectId 0x40, Id 0x44, World 0x48,
    /// RelationType 0x4C).</item>
    /// <item>The (world, points) constructor and grdbRelation_c::GetIDsOfChildren / GetIDsOfFathers take or return
    /// toolArray_c instead of std::vector, under other export names.</item>
    /// </list>
    /// The build is recognized by an export that only the toolArray_c build has, not by a version number.
    /// </remarks>
    internal static class DrawingsBuild
    {
        // grclaPluginInput_c::GetPoints returning const toolArray_c<Vector_c>& (Tekla 2020); 2025/2026 return std::vector.
        private const string ToolArrayGetPoints = "?GetPoints@grclaPluginInput_c@@QEBAAEBV?$toolArray_c@VVector_c@@@@XZ";

        // -1 = not detected yet, 0 = std::vector build, 1 = toolArray_c build. Tekla never swaps Drawings.dll while running.
        private static int _usesToolArray = -1;

        /// <summary>
        /// Whether the loaded Drawings.dll is the toolArray_c build (Tekla Structures 2020).
        /// Throws <see cref="InvalidOperationException"/> outside the Tekla process.
        /// </summary>
        internal static bool UsesToolArray
        {
            get
            {
                if (_usesToolArray < 0) _usesToolArray = NativeHost.HasExport(ToolArrayGetPoints) ? 1 : 0;
                return _usesToolArray == 1;
            }
        }

        /// <summary>
        /// Throws <see cref="NotSupportedException"/> on the toolArray_c build for a member whose native function only
        /// exists with std::vector in its signature.
        /// </summary>
        /// <param name="member">Name of the public member, for the message.</param>
        internal static void EnsureStdVectorBuild(string member)
        {
            if (UsesToolArray)
            {
                throw new NotSupportedException(member + " is not supported on this Tekla version (Tekla Structures 2020): " +
                                                "its native function takes or returns toolArray_c instead of std::vector.");
            }
        }
    }
}
