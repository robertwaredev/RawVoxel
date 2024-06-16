namespace RawVoxel.Meshing;

public static class Point
{
    public static int Encode(int vertex, int normal = 0, int uv = 0)
    {
        int encodedPoint = 0;

        encodedPoint |= vertex << 16;
        encodedPoint |= normal << 13;
        encodedPoint |= uv << 11;

        return encodedPoint;
    }
    public static int DecodeVertex(int encodedPoint)
    {
        return encodedPoint >> 16;
    }
    public static int DecodeNormal(int encodedPoint)
    {
        return encodedPoint >> 13 & 0b111;
    }
    public static int DecodeUV(int encodedPoint)
    {
        return encodedPoint >> 11 & 0b11;
    }
}