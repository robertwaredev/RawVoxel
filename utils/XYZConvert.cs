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

        // Print the ushort value and Vector3I value for every index in dimension.
        public static void TestDimension(Vector3I dimension)
        {
            for (int i = 0; i < dimension.X * dimension.Y * dimension.Z; i ++)
            {
                Vector3I vectorOut = XYZConvert.ToVector3I(i, dimension);
                int shortOut = XYZConvert.ToIndex(vectorOut, dimension);
                GD.PrintT(vectorOut, shortOut);
            }

            for (int x = 0; x < dimension.X; x ++)
            {
                for (int y = 0; y < dimension.X; y ++)
                {            
                    for (int z = 0; z < dimension.X; z ++)
                    {            
                        int indexOut = XYZConvert.ToIndex(new(x, y, z), dimension);
                        GD.PrintT(indexOut);
                    }
                }  
            }
        }
    }
}