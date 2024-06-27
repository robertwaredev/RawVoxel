using Godot;
using System.Diagnostics;

namespace RawVoxel.Math;

public static class XYZ
{   
    // Normal versions.
    public static int Encode(int X, int Y, int Z, Vector3I diameter)
    {
        Debug.Assert(diameter.X > 0 && diameter.Y > 0 && diameter.Z > 0, "XYZ.Encode() only works with positive diameter values!");

        return (X * diameter.Y * diameter.Z) + (Y * diameter.Z) + Z;
    }
    public static int Encode(Vector3I XYZ, Vector3I diameter)
    {
        return Encode(XYZ.X, XYZ.Y, XYZ.Z, diameter);
    }
    public static Vector3I Decode(int XYZ, Vector3I diameter, bool signedRange = false)
    {
        Debug.Assert(diameter.X > 0 && diameter.Y > 0 && diameter.Z > 0, "XYZ.Decode() only works with positive diameter values!");

        int X = XYZ / diameter.Z / diameter.Y;
        int Y = XYZ / diameter.Z;
        int Z = XYZ;

        return new Vector3I()
        {
            X = Wrap(X, diameter.X, signedRange),
            Y = Wrap(Y, diameter.Y, signedRange),
            Z = Wrap(Z, diameter.Z, signedRange)
        };
    }

    // Bitshift versions.
    public static int Encode(int X, int Y, int Z, int shifts)
    {
        Debug.Assert(shifts > 0, "XYZ.Encode() only works with positive shift values!");

        return (X << shifts << shifts) + (Y << shifts) + Z;
    }
    public static int Encode(Vector3I XYZ, int shifts)
    {
        return Encode(XYZ.X, XYZ.Y, XYZ.Z, shifts);
    }
    public static Vector3I Decode(int index, int shifts, bool signedRange = false)
    {
        Debug.Assert(shifts > 0, "XYZ.Decode() only works with positive shift values!");

        int X = index >> shifts >> shifts;
        int Y = index >> shifts;
        int Z = index;

        return new Vector3I()
        {
            X = Wrap(X, 1 << shifts, signedRange),
            Y = Wrap(Y, 1 << shifts, signedRange),
            Z = Wrap(Z, 1 << shifts, signedRange)
        };
    }

    public static Vector3I LShift(Vector3I vector, int shifts)
    {
        Debug.Assert(shifts > 0, "XYZ.LShift() only works with positive shift values!");

        vector.X <<= shifts;
        vector.Y <<= shifts;
        vector.Z <<= shifts;
        
        return vector;
    }
    public static Vector3I RShift(Vector3I vector, int shifts)
    {
        Debug.Assert(shifts > 0, "XYZ.RShift() only works with positive shift values!");

        vector.X >>= shifts;
        vector.Y >>= shifts;
        vector.Z >>= shifts;
        
        return vector;
    }

    public static int Wrap(int index, int range, bool signedRange = false)
    {
        Debug.Assert(range > 0, "XYZ.Wrap() only works with positive range values!");

        int remainder = index & (range - 1);

        int wrappedIndex;

        if (signedRange)
        {
            wrappedIndex = (remainder < -range) ? remainder + (range * 2) : remainder;
        }
        else
        {
            wrappedIndex = (remainder < 0) ? remainder + range : remainder;
        }

        return wrappedIndex;
    }
    public static Vector3I Wrap(Vector3I XYZ, int range, bool signedRange = false)
    {
        return new()
        {
          X = Wrap(XYZ.X, range, signedRange),
          Y = Wrap(XYZ.Y, range, signedRange),
          Z = Wrap(XYZ.Z, range, signedRange),
        };
    }
    public static Vector3I Wrap(Vector3I XYZ, Vector3I range, bool signedRange = false)
    {
        return new()
        {
          X = Wrap(XYZ.X, range.X, signedRange),
          Y = Wrap(XYZ.Y, range.Y, signedRange),
          Z = Wrap(XYZ.Z, range.Z, signedRange),
        };
    }
    
    public static void TestSignedEncoding(Vector3I radius, Vector3I diameter)
    {
        GD.PrintS("SIGNED ENCODING TEST");
        GD.Print("===============================");
        
        for (int x = 0; x < diameter.X; x ++)
        {
            for (int y = 0; y < diameter.Y; y ++)
            {
                for (int z = 0; z < diameter.Z; z ++)
                {
                    Vector3I i = new Vector3I(x, y, z) - radius;
                    
                    int index = Encode(i, diameter);

                    Vector3I o = Decode(index, diameter, true);
                    
                    GD.Print("");
                    GD.PrintT("=>:", i);
                    GD.PrintT("<=:", o);
                    GD.Print("");

                    Debug.Assert(i == o, "TEST FAILED!");
                }
            }
        }
    }
}