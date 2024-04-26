using Godot;

namespace RawUtils
{
    public static class XYZConvert
    {   
        // Convert XYZ coordinates in dimension space into a flat integer.
        public static int XYZToIndex(int X, int Y, int Z, Vector3I dimension)
        {
            return (X * dimension.Y * dimension.Z) + (Y * dimension.Z) + Z;
        }
        
        // Convert Vector3I in dimension space into a flat integer.
        public static int Vector3IToIndex(Vector3I XYZ, Vector3I dimension)
        {
            return XYZToIndex(XYZ.X, XYZ.Y, XYZ.Z, dimension);
        }
        
        // Convert a flat integer into a Vector3I in dimension space.
        public static Vector3I IndexToVector3I(int XYZ, Vector3I dimension)
        {
            int X = XYZ / dimension.Z / dimension.Y % dimension.X;
            int Y = XYZ / dimension.Z % dimension.Y;
            int Z = XYZ % dimension.Z;

            return new Vector3I(X, Y, Z);
        }
        
        
        // Convert XYZ coordinates in dimension space into a flat integer.
        public static int XYToIndex(int X, int Y, Vector2I dimension)
        {
            return (X * dimension.Y) + (Y);
        }
        
        // Convert Vector3I in dimension space into a flat integer.
        public static int Vector2IToIndex(Vector2I XY, Vector2I dimension)
        {
            return XYToIndex(XY.X, XY.Y, dimension);
        }
        
        // Convert a flat integer into a Vector3I in dimension space.
        public static Vector2I IndexToVector2I(int XY, Vector2I dimension)
        {
            int X = XY / dimension.Y % dimension.X;
            int Y = XY % dimension.Y;

            return new Vector2I(X, Y);
        }
    }
}