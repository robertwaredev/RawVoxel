namespace RawVoxel.Math.Binary;

// Two bit masks representing left-most and right-most endpoint "links" of each "chain" of set bits in the specified sequence.
public struct Links(uint sequence)
{
    // Links extracted from chains of set bits in a sequence via "left to right" order. (left-most endpoint links)
    public uint LBitMask = sequence & ~(sequence >> 1);
    // Links extracted from chains of set bits in a sequence via "right to left" order. (right-most endpoint links)
    public uint RBitMask = sequence & ~(sequence << 1);
}