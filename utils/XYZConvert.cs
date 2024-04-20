using Godot;

namespace RawUtils
{
    public static class XYZConvert
    {   
        // Convert XYZ coordinates in dimension space into a flat integer.
        public static int ToIndex(int X, int Y, int Z, Vector3I dimension)
        {
            return Z + (Y * dimension.X) + (X * dimension.Y * dimension.Z);
        }
        public static int ToIndex(Vector3I XYZ, Vector3I dimension)
        {
            return ToIndex(XYZ.X, XYZ.Y, XYZ.Z, dimension);
        }
        
        // Convert an integer into XYZ coordinates in dimension space.
        public static Vector3I ToVector3I(int XYZ, Vector3I dimension)
        {
            int X = XYZ / dimension.Z / dimension.Y % dimension.X;
            int Y = XYZ / dimension.Z % dimension.Y;
            int Z = XYZ % dimension.Z;

            return new Vector3I(X, Y, Z);
        }
    }
}