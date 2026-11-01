// =====================================================================================
// TeklaHelper.DrawingNative - NativeVector3: 3D vector shared by all native calls into Drawings.dll
// =====================================================================================

using System.Runtime.InteropServices;

namespace TeklaHelper.DrawingNative
{
    /// <summary>
    /// 3D vector compatible with Vector_c in Drawings.dll (three consecutive doubles, 0x18 bytes).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeVector3
    {
        /// <summary>X coordinate.</summary>
        public double X;

        /// <summary>Y coordinate.</summary>
        public double Y;

        /// <summary>Z coordinate.</summary>
        public double Z;

        /// <summary>
        /// Creates a vector from its three coordinates.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="z">Z coordinate.</param>
        public NativeVector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Returns the vector as "(X, Y, Z)".
        /// </summary>
        public override string ToString()
        {
            return "(" + X + ", " + Y + ", " + Z + ")";
        }
    }
}
