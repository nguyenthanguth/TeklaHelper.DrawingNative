// =====================================================================================
// TeklaHelper.DrawingNative - NativeMethods: P/Invoke declarations for Drawings.dll
// See DrawingNativePlugin.cs (same folder) for the overview, the Drawings.dll target and the verification sources.
// =====================================================================================

using System;
using System.Runtime.InteropServices;

namespace TeklaHelper.DrawingNative.Plugin
{
    /// <summary>
    /// All P/Invoke declarations for Drawings.dll (hidden from users).
    /// Instance methods take the grclaPluginInput_c pointer as the first argument (x64: ThisCall is ignored).
    /// </summary>
    internal static class NativeMethods
    {
        #region grclaPluginInput_c

        /// <summary>grclaPluginInput_c::grclaPluginInput_c(int inputId).</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "??0grclaPluginInput_c@@QEAA@H@Z", CallingConvention = CallingConvention.ThisCall)]
        internal static extern IntPtr PluginInput_CtorId(IntPtr thisPtr, int inputId);

        /// <summary>grclaPluginInput_c::grclaPluginInput_c(int world, int objectId) - note the (world, objectId) order.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "??0grclaPluginInput_c@@QEAA@HH@Z", CallingConvention = CallingConvention.ThisCall)]
        internal static extern IntPtr PluginInput_CtorWorldObject(IntPtr thisPtr, int world, int objectId);

        /// <summary>grclaPluginInput_c::grclaPluginInput_c(int world, const vector&lt;Vector_c&gt;&amp; points).</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "??0grclaPluginInput_c@@QEAA@HAEBV?$vector@VVector_c@@V?$allocator@VVector_c@@@std@@@std@@@Z", CallingConvention = CallingConvention.ThisCall)]
        internal static extern IntPtr PluginInput_CtorWorldPoints(IntPtr thisPtr, int world, IntPtr pointsVector);

        /// <summary>grclaPluginInput_c::~grclaPluginInput_c().</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "??1grclaPluginInput_c@@QEAA@XZ", CallingConvention = CallingConvention.ThisCall)]
        internal static extern void PluginInput_Dtor(IntPtr thisPtr);

        /// <summary>grclaPluginInput_c::GetId.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?GetId@grclaPluginInput_c@@QEBAHXZ", CallingConvention = CallingConvention.ThisCall)]
        internal static extern int PluginInput_GetId(IntPtr thisPtr);

        /// <summary>grclaPluginInput_c::GetPluginId - reads the owning plugin from the database (null-checked).</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?GetPluginId@grclaPluginInput_c@@QEBAHXZ", CallingConvention = CallingConvention.ThisCall)]
        internal static extern int PluginInput_GetPluginId(IntPtr thisPtr);

        /// <summary>grclaPluginInput_c::GetObjectId.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?GetObjectId@grclaPluginInput_c@@QEBAHXZ", CallingConvention = CallingConvention.ThisCall)]
        internal static extern int PluginInput_GetObjectId(IntPtr thisPtr);

        /// <summary>grclaPluginInput_c::GetWorld.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?GetWorld@grclaPluginInput_c@@QEBAHXZ", CallingConvention = CallingConvention.ThisCall)]
        internal static extern int PluginInput_GetWorld(IntPtr thisPtr);

        /// <summary>grclaPluginInput_c::IsPointInput.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?IsPointInput@grclaPluginInput_c@@QEBA_NXZ", CallingConvention = CallingConvention.ThisCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool PluginInput_IsPointInput(IntPtr thisPtr);

        /// <summary>grclaPluginInput_c::IsObjectInput.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?IsObjectInput@grclaPluginInput_c@@QEBA_NXZ", CallingConvention = CallingConvention.ThisCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool PluginInput_IsObjectInput(IntPtr thisPtr);

        /// <summary>grclaPluginInput_c::GetPointAnchored - asserts if the index is out of range.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?GetPointAnchored@grclaPluginInput_c@@QEBA_NH@Z", CallingConvention = CallingConvention.ThisCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool PluginInput_GetPointAnchored(IntPtr thisPtr, int pointIndex);

        /// <summary>grclaPluginInput_c::SetPointAnchored.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?SetPointAnchored@grclaPluginInput_c@@QEAA_NH_N@Z", CallingConvention = CallingConvention.ThisCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool PluginInput_SetPointAnchored(IntPtr thisPtr, int pointIndex, [MarshalAs(UnmanagedType.I1)] bool anchored);

        /// <summary>grclaPluginInput_c::SetAllPointsAnchored.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?SetAllPointsAnchored@grclaPluginInput_c@@QEAA_N_N@Z", CallingConvention = CallingConvention.ThisCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool PluginInput_SetAllPointsAnchored(IntPtr thisPtr, [MarshalAs(UnmanagedType.I1)] bool anchored);

        /// <summary>grclaPluginInput_c::SetId.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?SetId@grclaPluginInput_c@@QEAA_NH@Z", CallingConvention = CallingConvention.ThisCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool PluginInput_SetId(IntPtr thisPtr, int id);

        /// <summary>grclaPluginInput_c::SetObjectId.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?SetObjectId@grclaPluginInput_c@@QEAA_NH@Z", CallingConvention = CallingConvention.ThisCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool PluginInput_SetObjectId(IntPtr thisPtr, int objectId);

        /// <summary>grclaPluginInput_c::SetWorld.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?SetWorld@grclaPluginInput_c@@QEAA_NH@Z", CallingConvention = CallingConvention.ThisCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool PluginInput_SetWorld(IntPtr thisPtr, int world);

        /// <summary>grclaPluginInput_c::DBSelect - no null check: crashes on a non-existent ID.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?DBSelect@grclaPluginInput_c@@QEAA_NXZ", CallingConvention = CallingConvention.ThisCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool PluginInput_DBSelect(IntPtr thisPtr);

        /// <summary>grclaPluginInput_c::DBInsert(int pluginId).</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?DBInsert@grclaPluginInput_c@@QEAA_NH@Z", CallingConvention = CallingConvention.ThisCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool PluginInput_DBInsert(IntPtr thisPtr, int pluginId);

        /// <summary>grclaPluginInput_c::DBUpdate.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?DBUpdate@grclaPluginInput_c@@QEAA_NXZ", CallingConvention = CallingConvention.ThisCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool PluginInput_DBUpdate(IntPtr thisPtr);

        /// <summary>grclaPluginInput_c::DBDelete.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?DBDelete@grclaPluginInput_c@@QEAA_NXZ", CallingConvention = CallingConvention.ThisCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool PluginInput_DBDelete(IntPtr thisPtr);

        /// <summary>grclaPluginInput_c::Move(const Vector_c&amp; delta, bool vectorInDrawingCoordinates, int newWorld).</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?Move@grclaPluginInput_c@@QEAA_NAEBVVector_c@@_NH@Z", CallingConvention = CallingConvention.ThisCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool PluginInput_Move(IntPtr thisPtr, [In] ref NativeVector3 delta, [MarshalAs(UnmanagedType.I1)] bool vectorInDrawingCoordinates, int newWorld);

        /// <summary>grclaPluginInput_c::Transform(int matrixId).</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?Transform@grclaPluginInput_c@@QEAA_NH@Z", CallingConvention = CallingConvention.ThisCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool PluginInput_Transform(IntPtr thisPtr, int matrixId);

        /// <summary>static grclaPluginInput_c::GetInputs(int pluginId) - returns std::vector&lt;int&gt; by value.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?GetInputs@grclaPluginInput_c@@SA?AV?$vector@HV?$allocator@H@std@@@std@@H@Z", CallingConvention = CallingConvention.Cdecl)]
        internal static extern StdVectorInt PluginInput_GetInputs(int pluginId);

        /// <summary>static grclaPluginInput_c::GetAllPluginsUsingObjectAsInput(int objectId).</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?GetAllPluginsUsingObjectAsInput@grclaPluginInput_c@@SA?AV?$vector@HV?$allocator@H@std@@@std@@H@Z", CallingConvention = CallingConvention.Cdecl)]
        internal static extern StdVectorInt PluginInput_GetAllPluginsUsingObjectAsInput(int objectId);

        /// <summary>static grclaPluginInput_c::UpdateRelations(int oldPluginId, int newPluginId).</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?UpdateRelations@grclaPluginInput_c@@SA_NHH@Z", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool PluginInput_UpdateRelations(int oldPluginId, int newPluginId);

        #endregion

        #region grclaPlugin_c / grdbRelation_c

        /// <summary>static grclaPlugin_c::IsPluginChild(int childId, int, int) - returns the owning plugin ID (gr_object.belongs_to) or 0; only childId is used.</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?IsPluginChild@grclaPlugin_c@@SAHHHH@Z", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Plugin_IsPluginChild(int childId, int unused1, int unused2);

        /// <summary>static grdbRelation_c::GetIDsOfChildren(int fatherId, Type_e type).</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?GetIDsOfChildren@grdbRelation_c@@SA?AV?$vector@HV?$allocator@H@std@@@std@@HW4Type_e@1@@Z", CallingConvention = CallingConvention.Cdecl)]
        internal static extern StdVectorInt Relation_GetIDsOfChildren(int fatherId, int relationType);

        /// <summary>static grdbRelation_c::GetIDsOfFathers(int childId, Type_e type).</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?GetIDsOfFathers@grdbRelation_c@@SA?AV?$vector@HV?$allocator@H@std@@@std@@HW4Type_e@1@@Z", CallingConvention = CallingConvention.Cdecl)]
        internal static extern StdVectorInt Relation_GetIDsOfFathers(int childId, int relationType);

        /// <summary>static grdbRelation_c::AddRelation(int fatherId, int childId, Type_e type).</summary>
        [DllImport(NativeFile.DRAWINGS, EntryPoint = "?AddRelation@grdbRelation_c@@SA_NHHW4Type_e@1@@Z", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool Relation_AddRelation(int fatherId, int childId, int relationType);

        #endregion
    }
}
