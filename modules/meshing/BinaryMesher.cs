using Godot;
using RawUtils;
using System.Collections.Generic;
using static System.Numerics.BitOperations;

// !! THIS ONLY WORKS WITH CUBED CHUNK DIMENSIONS THAT ARE A POWER OF TWO. !!

// TODO - Skip the generation section for homogenous chunks.

namespace RawVoxel
{
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
        
        // A bit mask representing a "chain" of set bits generated from the specified endpoint "links", its offset, and its length.
        private struct Chain(Links links)
        {
            // Bit mask representing a chain of set bits from a larger sequence.
            public uint BitMask = 0;
            // Trailing zeros for BitMask.
            public byte Offset = (byte)TrailingZeroCount(links.RBitMask);
            // Length of the chain of set bits in BitMask.
            public byte Length = (byte)(TrailingZeroCount(links.LBitMask >> TrailingZeroCount(links.RBitMask)) + 1);
        }
        
        // Queue "chains" of set bits from the specified sequence.
        private static Queue<Chain> QueueChains(Links links)
        {
            // Create a placeholder list of chains.
            Queue<Chain> chains = [];
            
            // Generate chains from links.
            while ((links.LBitMask | links.RBitMask) != 0)
            {
                // Generate chain from links.
                Chain chain = new(links);

                // Generate chain's bit mask using its offset and length.
                for (byte bit = chain.Offset; bit < chain.Offset + chain.Length; bit ++)
                {
                    chain.BitMask |= (uint)1 << bit;
                }

                // Add chain to the list.
                chains.Enqueue(chain);

                // Clear bits from links using the chain's bit mask.
                links.LBitMask &= ~chain.BitMask;
                links.RBitMask &= ~chain.BitMask;
            }

            return chains;
        }
        
        // Generate a binary greedy mesh.
        public static void Generate(ref Chunk chunk, ref byte[] voxelTypes, ref WorldSettings worldSettings)
        {
            #region Variables

            // Store chunk diameter with a shorter name.
            int diameter = worldSettings.ChunkDiameter;
            
            // Sequences of voxel visibility bit masks, stored as [set, relative depth, relative width] with relative height encoded into each sequence's bits.
            uint[,,] voxelSequences = new uint[3, diameter, diameter];
            
            // Sequences of plane visibility bit masks, stored as [set, relative height, relative width] with relative depth encoded into each sequence's bits.
            uint[,,] planeSequences = new uint[6, diameter, diameter];
            
            // Create placeholders for relative axes to be used when generating mesh data.
            Vector3I hDirection = new();
            Vector3I wDirection = new();
            Vector3I dDirection = new();
            
            // Create placeholder lists for mesh data.
            List<Vector3> Vertices = [];
            List<Vector3> Normals = [];
            List<int> Indices = [];

            #endregion Variables
                
            // Encode visible voxel positions into sequences of bit masks.
            for (int x = 0; x < diameter; x ++)
            {
                for (int y = 0; y < diameter; y ++)
                {
                    for (int z = 0; z < diameter; z ++)
                    {
                        // Convert position to index.
                        uint voxelIndex = (uint)XYZBitShift.XYZToIndex(x, y, z, XYZBitShift.CalculateShifts(diameter));
                        
                        // Check if current voxel bit mask is true.
                        if (voxelTypes[voxelIndex] != 0)
                        {      
                            // Merge voxel bit mask into its respective sequence.
                            voxelSequences[0, y, z] |= (uint)1 << x;    // X axis voxels. // Y = relative depth // Z = relative width // X = relative height
                            voxelSequences[1, z, x] |= (uint)1 << y;    // Y axis voxels. // Z = relative depth // X = relative width // Y = relative height
                            voxelSequences[2, x, y] |= (uint)1 << z;    // Z axis voxels. // X = relative depth // Y = relative width // Z = relative height
                        }
                    }
                }
            }

            // Encode visible plane positions into sequences of bit masks.
            for (int set = 0; set < 3; set ++)
            {
                for (int depth = 0; depth < diameter; depth ++)
                {
                    for (int width = 0; width < diameter; width ++)
                    {
                        // Extract visible planes from the current sequence.
                        Links visiblePlanes = new(voxelSequences[set, depth, width]);
                        
                        // Loop through bits in visible planes bit masks.
                        for(int height = 0; height < diameter; height ++)
                        {
                            // Check if a "top" plane exists at the current relative height.
                            if ((visiblePlanes.LBitMask & (1 << height)) != 0)
                            {
                                // Merge "top" plane bit mask into its respective sequence.
                                planeSequences[(set << 1) + 0, height, width] |= (uint)1 << depth;
                            }
                            
                            // Check if a "bottom" plane exists at the current relative height.
                            if ((visiblePlanes.RBitMask & (1 << height)) != 0)
                            {
                                // Merge "bottom" plane bit mask into its respective sequence.
                                planeSequences[(set << 1) + 1, height, width] |= (uint)1 << depth;
                            }
                        }
                    }
                }
            }

            // Generate mesh data.
            for (int set = 0; set < 6; set ++)
            {
                // Set relative axes based on set. These are the same axes used to encode visible voxel positions.
                switch (set)
                {
                    case 0: case 1: // X axis planes.
                        dDirection = new(0, 1, 0);  // Y = relative depth
                        wDirection = new(0, 0, 1);  // Z = relative width
                        hDirection = new(1, 0, 0);  // X = relative height
                        break;
                    case 2: case 3: // Y axis planes.
                        dDirection = new(0, 0, 1);  // Z = relative depth
                        wDirection = new(1, 0, 0);  // X = relative width
                        hDirection = new(0, 1, 0);  // Y = relative height
                        break;
                    case 4: case 5: // Z axis planes.
                        dDirection = new(1, 0, 0);  // X = relative depth
                        wDirection = new(0, 1, 0);  // Y = relative width
                        hDirection = new(0, 0, 1);  // Z = relative height
                        break;
                }

                // Loop through sequences of planes.
                for (int height = 0; height < diameter; height ++)
                {
                    for (int width = 0; width < diameter; width ++)
                    {
                        // Generate chain links from the current sequence of planes.
                        Links links = new(planeSequences[set, height, width]);
                        
                        // Generate chains from the current sequence of chain links.
                        Queue<Chain> chains = QueueChains(links);

                        // Loop through chains and generate mesh data.
                        foreach (Chain chain in chains)
                        {
                            // Calculate initial start position of chain.
                            Vector3I start = set switch
                            {
                                0 or 2 or 4 => (hDirection * height) + (wDirection * width) + (dDirection * chain.Offset) + hDirection,
                                _           => (hDirection * height) + (wDirection * width) + (dDirection * chain.Offset),
                            };
                            
                            // Calculate initial end position of chain.
                            Vector3I end = start + wDirection + (dDirection * chain.Length);
                            
                            // Loop through neighboring sequences of planes and try to expand the current chain into them.
                            for (int nextWidth = width + 1; nextWidth < diameter; nextWidth ++)
                            {
                                // Break if the current chain is unable to expand into the next sequence of planes.
                                if ((chain.BitMask & planeSequences[set, height, nextWidth]) != chain.BitMask) break;
                                
                                // Expand the current chain into the neighboring sequence of planes.
                                end += wDirection;
                                
                                // Clear bits from the neighboring sequence of planes to prevent creating overlapping planes.
                                planeSequences[set, height, nextWidth] &= ~chain.BitMask;
                            }

                            // Generate chain mesh data.
                            Vector3I vertexA = start + (dDirection * chain.Length);
                            Vector3I vertexB = start;
                            Vector3I vertexC = end - (dDirection * chain.Length);
                            Vector3I vertexD = end;

                            int offset = Vertices.Count;

                            // Add mesh data to lists.
                            switch (set)
                            {
                                case 0: case 2: case 4:
                                    Vertices.AddRange([vertexA, vertexB, vertexC, vertexD]);
                                    Normals.AddRange([hDirection, hDirection, hDirection, hDirection]);
                                    break;
                                default:
                                    Vertices.AddRange([vertexD, vertexC, vertexB, vertexA]);
                                    Normals.AddRange([-hDirection, -hDirection, -hDirection, -hDirection]);
                                    break;
                            }

                            Indices.AddRange([0 + offset, 1 + offset, 2 + offset, 0 + offset, 2 + offset, 3 + offset]);
                        }
                    }
                }
            }
            
            // Generate mesh.
            MeshHelper.Generate(ref chunk, ref Vertices, ref Normals, ref Indices);
        }
    }
}