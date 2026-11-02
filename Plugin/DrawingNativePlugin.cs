// =====================================================================================
// TeklaHelper.DrawingNative.dll - DrawingNativePlugin: single-class OOP wrapper for the native Plugin API of Drawings.dll
// =====================================================================================
// Target       : Drawings.dll (x64); the analyzed build and its SHA-256 are listed in REVERSE-ENGINEERING.md
// Verification : EntryPoints checked against the export table; memory layouts and parameter semantics reverse
//                engineered with REA + Ghidra 12.1.4 (REA\Cpp\grclaPluginInput_c_decompiled.cpp,
//                grclaPlugin_relations_decompiled.cpp, grclaPlugin_children_decompiled.cpp, dbv_cursor_api_decompiled.cpp).
// Dependencies : System.* only. Runs only inside the Tekla process while a drawing is open.
// =====================================================================================

using System.Collections.Generic;
using TeklaHelper.DrawingNative.Database;

namespace TeklaHelper.DrawingNative.Plugin
{
    /// <summary>
    /// OOP wrapper for the native drawing Plugin API of Drawings.dll (Tekla Structures, x64):
    /// plugin inputs (grclaPluginInput_c), objects drawn by a plugin, and parent/child relations (grdbRelation_c).
    /// </summary>
    /// <remarks>
    /// Use only inside the Tekla process (macros, plugins) while a drawing is open; in any other process every method
    /// throws <see cref="System.InvalidOperationException"/>.
    /// </remarks>
    public static class DrawingNativePlugin
    {
        /// <summary>
        /// Gets the IDs of all inputs of a plugin.
        /// </summary>
        /// <param name="pluginId">Drawing ID of the plugin.</param>
        /// <returns>Input IDs; empty if <paramref name="pluginId"/> is not positive or the plugin has no inputs.</returns>
        public static List<int> GetInputIds(int pluginId)
        {
            if (pluginId <= 0) return new List<int>();
            NativeHost.EnsureInsideTekla();
            return NativeMethods.PluginInput_GetInputs(pluginId).ToListAndFree();
        }

        /// <summary>
        /// Loads every input of a plugin as a <see cref="NativePluginInput"/>.
        /// The caller must dispose each element.
        /// </summary>
        /// <param name="pluginId">Drawing ID of the plugin.</param>
        /// <returns>Loaded inputs; inputs that cannot be loaded are skipped.</returns>
        public static List<NativePluginInput> GetInputs(int pluginId)
        {
            List<NativePluginInput> inputs = new List<NativePluginInput>();
            try
            {
                foreach (int inputId in GetInputIds(pluginId))
                {
                    NativePluginInput input = NativePluginInput.SelectById(inputId);
                    if (input != null) inputs.Add(input);
                }
            }
            catch
            {
                // Do not leave the inputs loaded so far to the finalizer thread.
                foreach (NativePluginInput input in inputs) input.Dispose();
                throw;
            }
            return inputs;
        }

        /// <summary>
        /// Finds every plugin that uses a model object as an input.
        /// </summary>
        /// <param name="objectId">ID of the model object.</param>
        /// <returns>Plugin IDs; empty if no plugin uses the object.</returns>
        public static List<int> GetPluginIdsUsingObject(int objectId)
        {
            if (objectId <= 0) return new List<int>();
            NativeHost.EnsureInsideTekla();
            return NativeMethods.PluginInput_GetAllPluginsUsingObjectAsInput(objectId).ToListAndFree();
        }

        /// <summary>
        /// Moves every input of the old plugin to the new plugin (for example after the plugin was recreated with a new ID).
        /// </summary>
        /// <param name="oldPluginId">ID of the plugin that currently owns the inputs.</param>
        /// <param name="newPluginId">ID of the plugin that receives the inputs; must exist in the drawing database.</param>
        /// <returns>true if at least one input was moved.</returns>
        public static bool MoveInputsToPlugin(int oldPluginId, int newPluginId)
        {
            if (oldPluginId <= 0 || newPluginId <= 0) return false;
            NativeHost.EnsureInsideTekla();
            return NativeMethods.PluginInput_UpdateRelations(oldPluginId, newPluginId);
        }

        /// <summary>
        /// Gets the ID of the plugin that drew a drawing object (mark, dimension, line...).
        /// </summary>
        /// <param name="childId">Drawing ID of the object.</param>
        /// <returns>ID of the owning plugin, or 0 if the object was not drawn by a plugin.</returns>
        public static int GetParentPluginId(int childId)
        {
            // Native code only reads childId (IDs <= 10 are rejected); the other two parameters are unused.
            if (childId <= 10) return 0;
            NativeHost.EnsureInsideTekla();
            return NativeMethods.Plugin_IsPluginChild(childId, 0, 0);
        }

        /// <summary>
        /// Checks whether a drawing object was drawn by the plugin <paramref name="pluginId"/>.
        /// </summary>
        /// <param name="childId">Drawing ID of the object.</param>
        /// <param name="pluginId">Drawing ID of the plugin.</param>
        public static bool IsChildOf(int childId, int pluginId)
        {
            return pluginId > 0 && GetParentPluginId(childId) == pluginId;
        }

        /// <summary>
        /// Gets the IDs of the objects drawn by a plugin (dimensions, marks, lines...): the gr_object rows whose
        /// belongs_to == pluginId. Different from <see cref="GetInputIds"/> (inputs) and
        /// <see cref="GetRelationChildIds"/> (relation table - input targets).
        /// </summary>
        /// <remarks>
        /// Reads through a cursor on the gr_object_belongs_to_idx index (DBdict.dll / DBvirtual.dll), like Tekla's own code.
        /// Child IDs change every time the plugin is recalculated (for example on CommitChanges after its inputs were moved).
        /// </remarks>
        /// <param name="pluginId">Drawing ID of the plugin.</param>
        /// <returns>IDs of the drawn objects; empty if the plugin has drawn nothing.</returns>
        /// <exception cref="System.InvalidOperationException">The drawing database was not found or the cursor could not be opened.</exception>
        public static List<int> GetChildIds(int pluginId)
        {
            return DrawingObjectTable.GetIdsBelongingTo(pluginId);
        }

        /// <summary>
        /// Gets child IDs from the grdbRelation relation table (grdbRelation_c::GetIDsOfChildren).
        /// For a plugin this table only holds input targets (ObjectId, point sets), not the objects the plugin draws.
        /// </summary>
        /// <param name="fatherId">ID of the father object.</param>
        /// <param name="relationType">Relation type to filter on; <see cref="RelationType.All"/> = every type.</param>
        /// <returns>Child IDs of the matching relations.</returns>
        /// <exception cref="System.NotSupportedException">Tekla Structures 2020 (the native function returns a toolArray_c).</exception>
        public static List<int> GetRelationChildIds(int fatherId, RelationType relationType = RelationType.All)
        {
            if (fatherId <= 0) return new List<int>();
            NativeHost.EnsureInsideTekla();
            DrawingsBuild.EnsureStdVectorBuild("DrawingNativePlugin.GetRelationChildIds");
            return NativeMethods.Relation_GetIDsOfChildren(fatherId, (int)relationType).ToListAndFree();
        }

        /// <summary>
        /// Gets father IDs from the grdbRelation relation table (grdbRelation_c::GetIDsOfFathers).
        /// </summary>
        /// <param name="childId">ID of the child object.</param>
        /// <param name="relationType">Relation type to filter on; <see cref="RelationType.All"/> = every type.</param>
        /// <returns>Father IDs of the matching relations.</returns>
        /// <exception cref="System.NotSupportedException">Tekla Structures 2020 (the native function returns a toolArray_c).</exception>
        public static List<int> GetRelationFatherIds(int childId, RelationType relationType = RelationType.All)
        {
            if (childId <= 0) return new List<int>();
            NativeHost.EnsureInsideTekla();
            DrawingsBuild.EnsureStdVectorBuild("DrawingNativePlugin.GetRelationFatherIds");
            return NativeMethods.Relation_GetIDsOfFathers(childId, (int)relationType).ToListAndFree();
        }

        /// <summary>
        /// Creates a new father → child relation in the drawing database.
        /// </summary>
        /// <param name="fatherId">ID of the father object.</param>
        /// <param name="childId">ID of the child object.</param>
        /// <param name="relationType">
        /// Relation type; must not be <see cref="RelationType.All"/> and must be a valid value
        /// (any other value makes native code assert "Unknown relation type").
        /// </param>
        /// <returns>true if the relation was created.</returns>
        public static bool AddRelation(int fatherId, int childId, RelationType relationType)
        {
            if (fatherId <= 0 || childId <= 0) return false;
            if (relationType == RelationType.All || !relationType.IsDefined()) return false;
            NativeHost.EnsureInsideTekla();
            return NativeMethods.Relation_AddRelation(fatherId, childId, (int)relationType);
        }
    }
}
