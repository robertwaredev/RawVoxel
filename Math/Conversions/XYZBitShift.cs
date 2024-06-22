using Godot;

namespace RawVoxel.Math.Conversions;

public static class XYZBitShift
{   
    // Convert XYZ coordinates in dimension space into a flat integer.
    public static int XYZToIndex(int X, int Y, int Z, int shifts)
    {
        return (X << shifts << shifts) + (Y << shifts) + Z;
    }
    
    // Convert Vector3I in dimension space into a flat integer.
    public static int Vector3IToIndex(Vector3I XYZ, int shifts)
    {
        return XYZToIndex(XYZ.X, XYZ.Y, XYZ.Z, shifts);
    }
    
    // Convert a flat integer into a Vector3I in dimension space.
    public static Vector3I IndexToVector3I(int XYZ, int shifts)
    {
        int X = (XYZ >> shifts >> shifts) & ((1 << shifts) - 1);
        int Y = (XYZ >> shifts) & ((1 << shifts) - 1);
        int Z = (XYZ) & ((1 << shifts) - 1);

        return new Vector3I(X, Y, Z);
    }

    // Bitshift each component of a Vector3I left or right.
    public static Vector3I Vector3ILeft(Vector3I vector, int shifts)
    {
        vector.X <<= shifts;
        vector.Y <<= shifts;
        vector.Z <<= shifts;
        
        return vector;
    }
    public static Vector3I Vector3IRight(Vector3I vector, int shifts)
    {
        vector.X >>= shifts;
        vector.Y >>= shifts;
        vector.Z >>= shifts;
        
        return vector;
    }

    // Calculate the number of right bitshifts required to reduce a number to 1.
    public static int CalculateShifts(int value)
    {
        int shifts = 0;

        while (value > 1)
        {
            value >>= 1;
            shifts ++;
        }

        return shifts;
    }
}