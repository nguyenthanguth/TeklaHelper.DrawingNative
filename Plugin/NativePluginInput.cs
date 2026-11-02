// =====================================================================================
// TeklaHelper.DrawingNative - NativePluginInput (grclaPluginInput_c): construction, properties, operations
// See DrawingNativePlugin.cs (same folder) for the overview, the Drawings.dll target and the verification sources.
// =====================================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TeklaHelper.DrawingNative.Plugin
{
    /// <summary>
    /// OOP wrapper for grclaPluginInput_c: one input (picked point or picked object) of a drawing plugin.
    /// </summary>
    /// <remarks>
    /// Native layout, Tekla 2025/2026 (sizeof = 0x48):
    /// 0x00 vector&lt;Vector_c&gt; Points | 0x18 set&lt;int&gt; AnchoredPoints | 0x38 int ObjectId
    /// | 0x3C int Id | 0x40 int World | 0x44 int RelationType.
    /// <para>
    /// Tekla 2020 (sizeof = 0x50): 0x00 toolArray_c&lt;Vector_c&gt; Points (data pointer at 0x00, int count at 0x10)
    /// | 0x20 set&lt;int&gt; AnchoredPoints | 0x40 int ObjectId | 0x44 int Id | 0x48 int World | 0x4C int RelationType.
    /// See <see cref="DrawingsBuild"/>.
    /// </para>
    /// </remarks>
    public sealed class NativePluginInput : IDisposable
    {
        // Bytes allocated for one grclaPluginInput_c: 0x48 (2025/2026) or 0x50 (2020), with slack so that a larger
        // object in another Tekla build cannot make the native constructor write past the block (heap corruption).
        private const int NativeSize = 0x100;
        private const int OffsetPointsBegin = 0x00;
        private const int OffsetPointsEnd = 0x08;
        private const int OffsetToolArrayCount = 0x10;
        private const int OffsetRelationType = 0x44;
        private const int OffsetRelationTypeToolArray = 0x4C;
        private const int VectorSize = 0x18;

        private IntPtr _nativeHandle;
        private readonly bool _ownsMemory;
        private bool _disposed;

        #region Constructors & Destructor

        /// <summary>
        /// Wraps an existing grclaPluginInput_c pointer (not owned, never freed).
        /// </summary>
        /// <param name="existingNativeHandle">Pointer to an existing grclaPluginInput_c.</param>
        public NativePluginInput(IntPtr existingNativeHandle)
            : this(existingNativeHandle, false)
        {
        }

        private NativePluginInput(IntPtr nativeHandle, bool ownsMemory)
        {
            _nativeHandle = nativeHandle;
            _ownsMemory = ownsMemory;
        }

        /// <summary>
        /// Creates an empty input that only carries the ID of an existing input in the database. Call <see cref="Select"/>
        /// to load its data, or use <see cref="SelectById"/>.
        /// </summary>
        /// <param name="inputId">ID of an existing input.</param>
        public static NativePluginInput FromId(int inputId)
        {
            return Construct(p => NativeMethods.PluginInput_CtorId(p, inputId));
        }

        /// <summary>
        /// Creates an object input from a model object in a World (view).
        /// </summary>
        /// <param name="world">World (view) of the input.</param>
        /// <param name="objectId">ID of the model object.</param>
        public static NativePluginInput ForObject(int world, int objectId)
        {
            return Construct(p => NativeMethods.PluginInput_CtorWorldObject(p, world, objectId));
        }

        /// <summary>
        /// Creates a point input from a list of points in a World (view).
        /// </summary>
        /// <param name="world">World (view) of the input.</param>
        /// <param name="points">Points of the input.</param>
        /// <exception cref="ArgumentNullException"><paramref name="points"/> is null.</exception>
        /// <exception cref="NotSupportedException">Tekla Structures 2020 (its constructor takes a toolArray_c).</exception>
        public static NativePluginInput ForPoints(int world, IList<NativeVector3> points)
        {
            if (points == null) throw new ArgumentNullException("points");
            DrawingsBuild.EnsureStdVectorBuild("NativePluginInput.ForPoints");

            return Construct(p =>
            {
                // Build a temporary std::vector<Vector_c> (first/last/end) pointing to unmanaged memory;
                // the native constructor copies the points into its own vector.
                int bytes = points.Count * VectorSize;
                IntPtr buffer = Marshal.AllocHGlobal(Math.Max(bytes, 1));
                IntPtr vec = Marshal.AllocHGlobal(3 * IntPtr.Size);
                try
                {
                    for (int i = 0; i < points.Count; i++)
                    {
                        Marshal.StructureToPtr(points[i], buffer + i * VectorSize, false);
                    }
                    IntPtr end = buffer + bytes;
                    Marshal.WriteIntPtr(vec, 0, buffer);
                    Marshal.WriteIntPtr(vec, IntPtr.Size, end);
                    Marshal.WriteIntPtr(vec, 2 * IntPtr.Size, end);
                    NativeMethods.PluginInput_CtorWorldPoints(p, world, vec);
                }
                finally
                {
                    Marshal.FreeHGlobal(vec);
                    Marshal.FreeHGlobal(buffer);
                }
            });
        }

        /// <summary>
        /// Creates an input and loads it from the drawing database (equivalent to grclaPluginInput_c::SelecById).
        /// </summary>
        /// <param name="inputId">ID of the input.</param>
        /// <returns>The loaded input, or null if the ID is not a valid input.</returns>
        public static NativePluginInput SelectById(int inputId)
        {
            if (inputId <= 0) return null;
            NativePluginInput input = FromId(inputId);
            if (input.Select()) return input;
            input.Dispose();
            return null;
        }

        /// <summary>
        /// Allocates the native object and runs <paramref name="nativeConstructor"/> on it. The wrapper only takes ownership
        /// after the native constructor succeeded, so a failed constructor never reaches the native destructor.
        /// </summary>
        private static NativePluginInput Construct(Action<IntPtr> nativeConstructor)
        {
            NativeHost.EnsureInsideTekla();
            IntPtr p = Allocate();
            try
            {
                nativeConstructor(p);
            }
            catch
            {
                Marshal.FreeHGlobal(p);
                throw;
            }
            return new NativePluginInput(p, true);
        }

        /// <summary>
        /// Releases the native memory if <see cref="Dispose()"/> was not called.
        /// </summary>
        ~NativePluginInput()
        {
            Dispose(false);
        }

        /// <summary>
        /// Calls the native destructor and frees the native memory owned by this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (_ownsMemory && _nativeHandle != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.PluginInput_Dtor(_nativeHandle);
                }
                catch { }

                Marshal.FreeHGlobal(_nativeHandle);
            }
            _nativeHandle = IntPtr.Zero;
            _disposed = true;
        }

        /// <summary>
        /// Allocates zero-filled unmanaged memory for one grclaPluginInput_c.
        /// </summary>
        private static IntPtr Allocate()
        {
            IntPtr p = Marshal.AllocHGlobal(NativeSize);
            for (int i = 0; i < NativeSize; i += 8) Marshal.WriteInt64(p, i, 0);
            return p;
        }

        #endregion

        #region Properties

        /// <summary>Pointer to the underlying grclaPluginInput_c object.</summary>
        public IntPtr NativeHandle
        {
            get { return _nativeHandle; }
        }

        private bool IsValid
        {
            get { return !_disposed && _nativeHandle != IntPtr.Zero; }
        }

        /// <summary>ID of the input in the drawing database.</summary>
        public int Id
        {
            get { return IsValid ? NativeMethods.PluginInput_GetId(_nativeHandle) : 0; }
        }

        /// <summary>ID of the plugin that owns the input (read from the database by <see cref="Id"/>); 0 if not found.</summary>
        public int PluginId
        {
            get { return IsValid ? NativeMethods.PluginInput_GetPluginId(_nativeHandle) : 0; }
        }

        /// <summary>Model object ID of an object input.</summary>
        public int ObjectId
        {
            get { return IsValid ? NativeMethods.PluginInput_GetObjectId(_nativeHandle) : 0; }
        }

        /// <summary>World (view) code of the input.</summary>
        public int World
        {
            get { return IsValid ? NativeMethods.PluginInput_GetWorld(_nativeHandle) : 0; }
        }

        /// <summary>
        /// Plugin → input relation type: PluginInputPoint1..PluginInputPointMany (by point count) or PluginInputObject.
        /// Set by Select (DBSelect) or Insert (DBInsert).
        /// </summary>
        public RelationType RelationType
        {
            get
            {
                if (!IsValid) return RelationType.All;
                int offset = DrawingsBuild.UsesToolArray ? OffsetRelationTypeToolArray : OffsetRelationType;
                return (RelationType)Marshal.ReadInt32(_nativeHandle, offset);
            }
        }

        /// <summary>Whether this is a point input (RelationType 100002..100005).</summary>
        public bool IsPointInput
        {
            get { return IsValid && NativeMethods.PluginInput_IsPointInput(_nativeHandle); }
        }

        /// <summary>Whether this is an object input (RelationType 100006).</summary>
        public bool IsObjectInput
        {
            get { return IsValid && NativeMethods.PluginInput_IsObjectInput(_nativeHandle); }
        }

        /// <summary>Number of points of the input.</summary>
        public int PointCount
        {
            get
            {
                if (!IsValid) return 0;
                if (DrawingsBuild.UsesToolArray)
                {
                    int count = Marshal.ReadInt32(_nativeHandle, OffsetToolArrayCount);
                    return count > 0 ? count : 0;
                }
                long bytes = Marshal.ReadIntPtr(_nativeHandle, OffsetPointsEnd).ToInt64()
                           - Marshal.ReadIntPtr(_nativeHandle, OffsetPointsBegin).ToInt64();
                return bytes > 0 ? (int)(bytes / VectorSize) : 0;
            }
        }

        /// <summary>
        /// Copy of the input points (read directly from the Points array at offset 0x00, like GetPoints(); both the
        /// std::vector and the toolArray_c of Tekla 2020 start with the pointer to the first element).
        /// </summary>
        public List<NativeVector3> Points
        {
            get
            {
                int count = PointCount;
                List<NativeVector3> points = new List<NativeVector3>(count);
                if (count == 0) return points;

                IntPtr first = Marshal.ReadIntPtr(_nativeHandle, OffsetPointsBegin);
                for (int i = 0; i < count; i++)
                {
                    points.Add((NativeVector3)Marshal.PtrToStructure(first + i * VectorSize, typeof(NativeVector3)));
                }
                return points;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Whether the point at <paramref name="pointIndex"/> is anchored to an object.
        /// </summary>
        /// <param name="pointIndex">Zero-based point index.</param>
        public bool GetPointAnchored(int pointIndex)
        {
            // Native code asserts when the index is out of range.
            if (!IsValid || pointIndex < 0 || pointIndex >= PointCount) return false;
            return NativeMethods.PluginInput_GetPointAnchored(_nativeHandle, pointIndex);
        }

        /// <summary>Sets the anchored state of one point.</summary>
        /// <param name="pointIndex">Zero-based point index.</param>
        /// <param name="anchored">true to anchor the point.</param>
        /// <returns>true on success; false if the index is out of range.</returns>
        public bool SetPointAnchored(int pointIndex, bool anchored)
        {
            if (!IsValid || pointIndex < 0 || pointIndex >= PointCount) return false;
            return NativeMethods.PluginInput_SetPointAnchored(_nativeHandle, pointIndex, anchored);
        }

        /// <summary>Sets the anchored state of every point.</summary>
        /// <param name="anchored">true to anchor the points.</param>
        /// <returns>true on success.</returns>
        public bool SetAllPointsAnchored(bool anchored)
        {
            return IsValid && NativeMethods.PluginInput_SetAllPointsAnchored(_nativeHandle, anchored);
        }

        /// <summary>
        /// Sets the ID; native code only accepts the ID of an object that exists in the drawing database.
        /// </summary>
        /// <param name="id">New ID.</param>
        /// <returns>true on success.</returns>
        public bool SetId(int id)
        {
            return IsValid && id > 0 && NativeMethods.PluginInput_SetId(_nativeHandle, id);
        }

        /// <summary>Sets the linked model object.</summary>
        /// <param name="objectId">ID of the model object.</param>
        /// <returns>true on success.</returns>
        public bool SetObjectId(int objectId)
        {
            return IsValid && NativeMethods.PluginInput_SetObjectId(_nativeHandle, objectId);
        }

        /// <summary>Sets the World (view).</summary>
        /// <param name="world">World (view) code.</param>
        /// <returns>true on success.</returns>
        public bool SetWorld(int world)
        {
            return IsValid && NativeMethods.PluginInput_SetWorld(_nativeHandle, world);
        }

        /// <summary>
        /// Loads the data (RelationType, points or ObjectId, World, anchored points) from the database by <see cref="Id"/>.
        /// </summary>
        /// <returns>true if the input was found and loaded.</returns>
        public bool Select()
        {
            if (!IsValid || Id <= 0) return false;
            // DBSelect uses the database object pointer without a null check: a non-existent ID would crash Tekla.
            // GetPluginId reads the same object but checks for null, so it serves as a safe probe first.
            if (NativeMethods.PluginInput_GetPluginId(_nativeHandle) == 0) return false;
            return NativeMethods.PluginInput_DBSelect(_nativeHandle);
        }

        /// <summary>
        /// Inserts the input into the database and attaches it to a plugin. Native code creates the plugin → input relation
        /// and writes the new ID into <see cref="Id"/>.
        /// </summary>
        /// <param name="pluginId">ID of the plugin that owns the input.</param>
        /// <returns>false if pluginId = 0, or if the input has no points and ObjectId = 0.</returns>
        public bool Insert(int pluginId)
        {
            if (!IsValid || pluginId <= 0) return false;
            if (PointCount == 0 && ObjectId == 0) return false;
            return NativeMethods.PluginInput_DBInsert(_nativeHandle, pluginId);
        }

        /// <summary>Writes the changes of the input to the database.</summary>
        /// <returns>true on success.</returns>
        public bool Update()
        {
            return IsValid && NativeMethods.PluginInput_DBUpdate(_nativeHandle);
        }

        /// <summary>Deletes the input from the database.</summary>
        /// <returns>true on success.</returns>
        public bool Delete()
        {
            return IsValid && NativeMethods.PluginInput_DBDelete(_nativeHandle);
        }

        /// <summary>
        /// Moves every point of the input by the vector (dx, dy, dz), in the coordinates of the input points.
        /// Call <see cref="Update"/> to save the result.
        /// </summary>
        /// <remarks>
        /// The native Move also accepts a vector in drawing coordinates and a new World, but both go through Tekla's World stack
        /// and close Tekla (assertion '!m_Stack.empty()' in grdr_world.cpp) outside the render loop, so they are not exposed.
        /// Use <see cref="SetWorld"/> to change the World.
        /// </remarks>
        /// <param name="dx">Offset along X.</param>
        /// <param name="dy">Offset along Y.</param>
        /// <param name="dz">Offset along Z.</param>
        /// <returns>true if the points were moved.</returns>
        public bool Move(double dx, double dy, double dz)
        {
            if (!IsValid) return false;
            NativeVector3 delta = new NativeVector3(dx, dy, dz);
            return NativeMethods.PluginInput_Move(_nativeHandle, ref delta, false, 0);
        }

        /// <summary>
        /// Transforms the point coordinates by a Tekla matrix (matrix ID).
        /// </summary>
        /// <param name="matrixId">ID of the Tekla matrix.</param>
        /// <returns>true on success.</returns>
        public bool Transform(int matrixId)
        {
            return IsValid && NativeMethods.PluginInput_Transform(_nativeHandle, matrixId);
        }

        /// <summary>
        /// Returns a short description: Id, ObjectId, World, RelationType and point count.
        /// </summary>
        public override string ToString()
        {
            if (!IsValid) return "NativePluginInput(disposed)";
            return "NativePluginInput(Id=" + Id + ", ObjectId=" + ObjectId + ", World=" + World
                 + ", RelationType=" + RelationType + ", Points=" + PointCount + ")";
        }

        #endregion
    }
}
