using Godot;

// TODO - Swap X and Z axes and test it, iterating in an XYZ loop breaks while a ZYX loop works.

namespace RawUtils
{
    public static class XYZConvert
    {   
        // Convert XYZ coordinates in dimension space into a flat integer.
        public static int ToIndex(int X, int Y, int Z, Vector3I dimension)
        {
            return X + (Y * dimension.X) + (Z * dimension.Y * dimension.X);
        }
        public static int ToIndex(Vector3I XYZ, Vector3I dimension)
        {
            return XYZ.X + (XYZ.Y * dimension.X) + (XYZ.Z * dimension.Y * dimension.X);
        }
        
        // Convert an integer into XYZ coordinates in dimension space.
        public static Vector3I ToVector3I(int XYZ, Vector3I dimension)
        {
            int X = XYZ % dimension.X;
            int Y = XYZ / dimension.X % dimension.Y;
            int Z = XYZ / dimension.X / dimension.Y % dimension.Z;

            return new Vector3I(X, Y, Z);
        }
    }
}