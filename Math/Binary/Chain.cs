using System.Collections.Generic;
using static System.Numerics.BitOperations;

namespace RawVoxel.Math.Binary;

// A bit mask representing a "chain" of set bits from the specified sequence.
public struct Chain(uint bitMask, byte offset, byte length)
{
    // Bit mask representing a chain of set bits from a larger sequence.
    public uint BitMask = bitMask;
    // Trailing zeros for BitMask.
    public byte Offset = offset;
    // Length of the chain of set bits in BitMask.
    public byte Length = length;

    // Queue chains of set bits from the specified sequence.
    public static Queue<Chain> QueueChains(uint sequence)
    {
        // Create a placeholder list of chains.
        Queue<Chain> chains = [];
        
        // Generate chains from sequence.
        while (sequence != 0)
        {
            // Generate chain bit mask using its offset and length.
            byte offset = (byte)TrailingZeroCount(sequence);
            byte length = (byte)TrailingZeroCount(~(sequence >> offset));
            uint bitMask = uint.MaxValue >> (32 - length) << offset;
            
            // Create new chain.
            Chain chain = new(bitMask, offset, length);

            // Add chain to the list.
            chains.Enqueue(chain);

            // Clear bits from sequence using the chain's bit mask.
            sequence &= ~bitMask;
        }

        return chains;
    }
}
