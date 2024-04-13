using Godot;

namespace RawUtils
{
    public static class XYZConvert
    {
        // Convert XYZ coordinates in dimension space into a ushort.
        public static ushort ToUShort(int X, int Y, int Z, Vector3I dimension)
        {
            ushort XYZUShort = (ushort)(X + (Y * dimension.X) + (Z * dimension.Y * dimension.X));

            return XYZUShort;
        }
        public static ushort ToUShort(Vector3I XYZ, Vector3I dimension)
        {
            ushort XYZUShort = (ushort)(XYZ.X + (XYZ.Y * dimension.X) + (XYZ.Z * dimension.Y * dimension.X));

            return XYZUShort;
        }
        
        // Convert XYZ coordinates in dimension space into a uint.
        public static uint ToUInt(int X, int Y, int Z, Vector3I dimension)
        {
            int XYZUInt = X + (Y * dimension.X) + (Z * dimension.Y * dimension.X);

            return (uint)(XYZUInt);
        }
        public static uint ToUInt(Vector3I XYZ, Vector3I dimension)
        {
            int XYZUInt = XYZ.X + (XYZ.Y * dimension.X) + (XYZ.Z * dimension.Y * dimension.X);

            return (uint)(XYZUInt);
        }
        
        // Convert an integer into XYZ coordinates in dimension space.
        public static Vector3I ToVector3I(int XYZ, Vector3I dimension)
        {
            int X = XYZ % dimension.X;
            int Y = XYZ / dimension.X % dimension.Y;
            int Z = XYZ / dimension.X / dimension.Y % dimension.Z;

            return new Vector3I(X, Y, Z);
        }

        // Print the ushort value and Vector3I value for every index in dimension.
        public static void TestDimension(Vector3I dimension)
        {
            for (int i = 0; i < dimension.X * dimension.Y * dimension.Z; i++)
            {
                Vector3I vectorOut = XYZConvert.ToVector3I(i, dimension);
                ushort shortOut = XYZConvert.ToUShort(vectorOut, dimension);
                GD.PrintS(vectorOut, shortOut);
            }
        }
    }
}