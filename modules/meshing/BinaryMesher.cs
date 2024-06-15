using Godot;
using RawUtils;
using System.Collections.Generic;
using System.ComponentModel;
using static System.Numerics.BitOperations;

namespace RawVoxel;

// TODO - Skip the generation section for homogenous chunks.

// X AXIS SWIZZLES     || Y AXIS SWIZZLES     || Z AXIS SWIZZLES
// =================================================================
// Z = relative width  || X = relative width  || Y = relative width
// X = relative height || Y = relative height || Z = relative height
// Y = relative depth  || Z = relative depth  || X = relative depth

public static class BinaryMesher
{ 
    // Two bit masks representing left-most and right-most endpoint "links" of each "chain" of set bits in the specified sequence.
    private struct Links(uint sequence)
    {
        // Links extracted from chains of set bits in a sequence via "left to right" order. (left-most endpoint links)
        public uint LBitMask = sequence & ~(sequence >> 1);
        // Links extracted from chains of set bits in a sequence via "right to left" order. (right-most endpoint links)
        public uint RBitMask = sequence & ~(sequence << 1);
    }
    
    // A bit mask representing a "chain" of set bits from the specified sequence.
    private struct Chain(uint bitMask, byte offset, byte length)
    {
        // Bit mask representing a chain of set bits from a larger sequence.
        public uint BitMask = bitMask;
        // Trailing zeros for BitMask.
        public byte Offset = offset;
        // Length of the chain of set bits in BitMask.
        public byte Length = length;
    }
    
    // Queue "chains" of set bits from the specified sequence.
    private static Queue<Chain> QueueChains(uint sequence)
    {
        // Create a placeholder list of chains.
        Queue<Chain> chains = [];
        
        // Generate chains from links.
        while (sequence != 0)
        {
            // Generate chain's bit mask using its offset and length.
            byte offset = (byte)TrailingZeroCount(sequence);
            byte length = (byte)TrailingZeroCount(~(sequence >> offset));
            uint bitMask = uint.MaxValue >> (32 - length) << offset;
            
            // Generate chain from links.
            Chain chain = new(bitMask, offset, length);

            // Add chain to the list.
            chains.Enqueue(chain);

            // Clear bits from links using the chain's bit mask.
            sequence &= ~bitMask;
        }

        return chains;
    }
    
    // Generate a binary greedy mesh.
    public static Surface[] GenerateSurfaces(ref byte[] voxels, ref WorldSettings worldSettings)
    {
        #region Variables

        // Store chunk diameter with a shorter name.
        int diameter = worldSettings.ChunkDiameter;
        
        // Store chunk diameter as diameter = 1 << shifts.
        int shifts = XYZBitShift.CalculateShifts(diameter);
        
        // Sequences of voxel visibility bit masks, stored as [set, relative depth, relative width] with relative height encoded into each sequence's bits.
        uint[,,] voxelSequences = new uint[3, diameter, diameter];
        
        // Sequences of plane visibility bit masks, stored as [set, relative height, relative width] with relative depth encoded into each sequence's bits.
        uint[,,] planeSequences = new uint[6, diameter, diameter];
        
        // Surface array for storing ArrayMesh data, one Surface per set.
        Surface[] surfaces = [new(), new(), new(), new(), new(), new()];

        #endregion Variables
            
        // Encode visible voxel positions into bit mask sequences.
        for (int x = 0; x < diameter; x ++)
        {
            for (int y = 0; y < diameter; y ++)
            {
                for (int z = 0; z < diameter; z ++)
                {
                    // Check if current voxel is solid.
                    if (voxels[XYZBitShift.XYZToIndex(x, y, z, shifts)] == 0) continue;

                    // Merge voxel bit mask into its respective sequence.
                    voxelSequences[0, y, z] |= (uint)1 << x;
                    voxelSequences[1, z, x] |= (uint)1 << y;
                    voxelSequences[2, x, y] |= (uint)1 << z;
                }
            }
        }

        // Encode visible plane positions into bit mask sequences.
        for (int set = 0; set < 3; set ++)
        {
            for (int depth = 0; depth < diameter; depth ++)
            {
                for (int width = 0; width < diameter; width ++)
                {
                    // Retrieve current voxel seqeuence.
                    uint voxelSequence = voxelSequences[set, depth, width];

                    // Skip if no visible voxels.
                    if (voxelSequence == 0) continue;
                        
                    // Extract visible planes from the current voxel sequence.
                    Links visiblePlanes = new(voxelSequence);
                        
                    // Loop through bits in visible planes bit masks.
                    for(int height = 0; height < diameter; height ++)
                    {
                        // Check if an "upper" plane exists at the current relative height.
                        if ((visiblePlanes.LBitMask & (1 << height)) != 0)
                        {
                            // Merge "upper" plane bit mask into its respective sequence.
                            planeSequences[(set << 1) + 0, height, width] |= (uint)1 << depth;
                        }
                        
                        // Check if an "lower" plane exists at the current relative height.
                        if ((visiblePlanes.RBitMask & (1 << height)) != 0)
                        {
                            // Merge "lower" plane bit mask into its respective sequence.
                            planeSequences[(set << 1) + 1, height, width] |= (uint)1 << depth;
                        }
                    }
                }
            }
        }
        
        // Loop through plane sequences and generate mesh data.
        for (int set = 0; set < 6; set ++)
        {
            for (int height = 0; height < diameter; height ++)
            {
                for (int width = 0; width < diameter; width ++)
                {
                    // Retrieve current plane sequence.
                    uint planeSequence = planeSequences[set, height, width];

                    // Skip if no visible planes.
                    if (planeSequence == 0) continue;
                    
                    // Generate chains from the current plane sequence.
                    Queue<Chain> chains = QueueChains(planeSequence);

                    // Loop through chains and generate mesh data.
                    foreach (Chain chain in chains)
                    {
                        // Store chain offset and length as "depth" and "length" for clarity purposes.
                        int depth = chain.Offset;
                        int length = chain.Length;

                        // Calculate chain start position.
                        Vector3I chainStart = set switch
                        {
                            0 => new Vector3I(height + 1, depth, width),
                            1 => new Vector3I(height, depth, width),

                            2 => new Vector3I(width, height + 1, depth),
                            3 => new Vector3I(width, height, depth),
                            
                            4 => new Vector3I(depth, width, height + 1),
                            _ => new Vector3I(depth, width, height),
                        };

                        // Calculate chain end position.
                        Vector3I chainEnd = set switch
                        {
                            0 or 1 => chainStart + new Vector3I(0, length, 1),
                            2 or 3 => chainStart + new Vector3I(1, 0, length),
                            _      => chainStart + new Vector3I(length, 1, 0),
                        };

                        // Loop through neighboring plane sequences and try to expand the current chain into them.
                        for (int nextWidth = width + 1; nextWidth < diameter; nextWidth ++)
                        {
                            // Retrieve the next plane sequence.
                            ref uint nextPlaneSequence = ref planeSequences[set, height, nextWidth];
                            
                            // Break if the current chain is unable to expand into the next plane sequence.
                            if ((chain.BitMask & nextPlaneSequence) != chain.BitMask) break;

                            // Expand the current chain into the neighboring plane sequence.
                            switch (set)
                            {
                                case 0: case 1:
                                    chainEnd.Z ++; break;
                                case 2: case 3:
                                    chainEnd.X ++; break;
                                default:
                                    chainEnd.Y ++; break;
                            }

                            // Clear bits from the neighboring plane sequence to prevent creating overlapping planes.
                            nextPlaneSequence &= ~chain.BitMask;
                        }

                        // Get offset for indices.
                        int vertexCount = surfaces[set].Vertices.Count;

                        // Create vertices.
                        Vector3I vertexA = chainStart;
                        Vector3I vertexB = chainStart;
                        Vector3I vertexC = chainEnd;
                        Vector3I vertexD = chainEnd;

                        // Offset vertices A & C.
                        switch (set)
                        {
                            case 0: case 1:
                                vertexA += new Vector3I(0, chain.Length, 0);
                                vertexC -= new Vector3I(0, chain.Length, 0);
                                break;
                            case 2: case 3:
                                vertexA += new Vector3I(0, 0, chain.Length);
                                vertexC -= new Vector3I(0, 0, chain.Length);
                                break;
                            default:
                                vertexA += new Vector3I(chain.Length, 0, 0);
                                vertexC -= new Vector3I(chain.Length, 0, 0);
                                break;
                        }
                        
                        // Invert vertex draw order based on set.
                        switch (set)
                        {
                            case 0: case 2: case 4:
                                surfaces[set].Vertices.AddRange([vertexA, vertexB, vertexC, vertexD]);
                                break;
                            default:
                                surfaces[set].Vertices.AddRange([vertexD, vertexC, vertexB, vertexA]);
                                break;
                        }

                        // Add indices to their list.
                        surfaces[set].Indices.AddRange([0 + vertexCount, 1 + vertexCount, 2 + vertexCount, 0 + vertexCount, 2 + vertexCount, 3 + vertexCount]);
                    }
                }
            }
        }
        
        return surfaces;
    }
}