using Godot;

namespace RawVoxel.Math.Conversions;

public static class XYZBitShift
{   
    public static int XYZToIndex(int X, int Y, int Z, int chunkBitshifts)
    {
        return (X << chunkBitshifts << chunkBitshifts) + (Y << chunkBitshifts) + Z;
    }
    public static int Vector3IToIndex(Vector3I XYZ, int chunkBitshifts)
    {
        return XYZToIndex(XYZ.X, XYZ.Y, XYZ.Z, chunkBitshifts);
    }
    public static Vector3I IndexToVector3I(int XYZ, int chunkBitshifts)
    {
        int X = (XYZ >> chunkBitshifts >> chunkBitshifts) & ((1 << chunkBitshifts) - 1);
        int Y = (XYZ >> chunkBitshifts) & ((1 << chunkBitshifts) - 1);
        int Z = (XYZ) & ((1 << chunkBitshifts) - 1);

        return new Vector3I(X, Y, Z);
    }

    public static Vector3I Vector3ILeft(Vector3I vector, int chunkBitshifts)
    {
        vector.X <<= chunkBitshifts;
        vector.Y <<= chunkBitshifts;
        vector.Z <<= chunkBitshifts;
        
        return vector;
    }
    public static Vector3I Vector3IRight(Vector3I vector, int chunkBitshifts)
    {
        vector.X >>= chunkBitshifts;
        vector.Y >>= chunkBitshifts;
        vector.Z >>= chunkBitshifts;
        
        return vector;
    }

    public static int IndexAddX(int index, int amount, int chunkBitshifts)
    {
        return index + (amount << chunkBitshifts << chunkBitshifts);
    }
    public static int IndexAddY(int index, int amount, int chunkBitshifts)
    {
        return index + (amount << chunkBitshifts);
    }
    public static int IndexAddZ(int index, int amount)
    {
        return index + amount;
    }
}