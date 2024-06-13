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
        // Two bit masks, containing left and right ends of "spans" of bits from the sequence respectively.
        private struct Ends(uint sequence)
        {
            // "L" refers to ends of spans extracted from a sequence of bits via "left to right" order. (left-most ends)
            public uint LBitMask = sequence & ~(sequence >> 1);
            // "R" refers to ends of spans extracted from a sequence of bits via "right to left" order. (right-most ends)
            public uint RBitMask = sequence & ~(sequence << 1);
            // Clear bits from L & R where set bits in the specified bit mask mark which bits should be cleared.
            public void ClearBits(uint bitMask)
            {
                LBitMask &= ~bitMask;
                RBitMask &= ~bitMask;
            }
        }
        // A bit mask, its offset, and its length.
        private struct Span(Ends ends)
        {
            // Bit mask representing the right-most span of set bits in a sequence.
            public uint BitMask = 0;
            // Trailing zeros for BitMask.
            public byte Offset = (byte)TrailingZeroCount(ends.RBitMask);
            // Length of the span of set bits in BitMask.
            public byte Length = (byte)(TrailingZeroCount(ends.LBitMask >> TrailingZeroCount(ends.RBitMask)) + 1);
        }
        
        // Generate binary greedy mesh.
        public static void Generate(ref Chunk chunk, ref WorldSettings worldSettings)
        {
            #region Variables

            // Store chunk diameter with a shorter name.
            int diameter = worldSettings.ChunkDiameter;
            
            // Voxels stored as [set, depth, width] with height encoded into each array element's sequence of bits.
            uint[,,] voxelSequences = new uint[3, diameter, diameter];
            
            // Planes stored as [set, height, width] with depth encoded into each array element's sequence of bits.
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
                
            // Encode voxel position into sequences of bit masks, one set for each axis.
            for (int x = 0; x < diameter; x ++)
            {
                for (int y = 0; y < diameter; y ++)
                {
                    for (int z = 0; z < diameter; z ++)
                    {
                        // Convert position to index.
                        uint voxelIndex = (uint)XYZBitShift.XYZToIndex(x, y, z, XYZBitShift.CalculateShifts(diameter));
                        
                        // Check if current voxel bit mask is true.
                        if (chunk.VoxelTypes[voxelIndex] != 0)
                        {      
                            // Merge voxel bit mask into its respective sequence.
                            voxelSequences[0, y, z] |= (uint)1 << x;
                            voxelSequences[1, z, x] |= (uint)1 << y;
                            voxelSequences[2, x, y] |= (uint)1 << z;
                        }
                    }
                }
            }

            // Encode plane position into sequences of bit masks, two sets for each axis.
            for (int set = 0; set < 3; set ++)
            {
                for (int depth = 0; depth < diameter; depth ++)
                {
                    for (int width = 0; width < diameter; width ++)
                    {
                        // Extract visible planes from the current sequence.
                        Ends planes = new(voxelSequences[set, depth, width]);
                        
                        for(int bit = 0; bit < diameter; bit ++)
                        {
                            // Check if a "left" plane exists at the current bit.
                            if ((planes.LBitMask & (1 << bit)) != 0)
                            {
                                // Merge "left" plane bit mask into its respective sequence.
                                planeSequences[(set << 1) + 0, bit, width] |= (uint)1 << depth;
                            }
                            
                            // Check if a "right" plane exists at the current bit.
                            if ((planes.RBitMask & (1 << bit)) != 0)
                            {
                                // Merge "right" plane bit mask into its respective sequence.
                                planeSequences[(set << 1) + 1, bit, width] |= (uint)1 << depth;
                            }
                        }
                    }
                }
            }

            // Generate mesh data.
            for (int set = 0; set < 6; set ++)
            {
                // Set relative axes based on set. (width, height, depth)
                switch (set)
                {
                    case 0: case 1: // X axis sequences of planes.
                        wDirection = new(0, 0, 1);  // Z
                        hDirection = new(1, 0, 0);  // X
                        dDirection = new(0, 1, 0);  // Y
                        break;
                    case 2: case 3: // Y axis sequences of planes.
                        wDirection = new(1, 0, 0);  // X
                        hDirection = new(0, 1, 0);  // Y
                        dDirection = new(0, 0, 1);  // Z
                        break;
                    case 4: case 5: // Z axis sequences of planes.
                        wDirection = new(0, 1, 0);  // Y
                        hDirection = new(0, 0, 1);  // Z
                        dDirection = new(1, 0, 0);  // X
                        break;
                }

                // Loop through sequences of planes.
                for (int height = 0; height < diameter; height ++)
                {
                    for (int width = 0; width < diameter; width ++)
                    {
                        // Queue spans for the current sequence of planes.
                        Queue<Span> spans = QueueSpans(planeSequences[set, height, width]);

                        // Loop through spans and generate mesh data.
                        foreach (Span span in spans)
                        {
                            // Calculate initial start position of span.
                            Vector3I start = set switch
                            {
                                0 or 2 or 4 => (hDirection * height) + (wDirection * width) + (dDirection * span.Offset) + hDirection,
                                _           => (hDirection * height) + (wDirection * width) + (dDirection * span.Offset),
                            };
                            
                            // Calculate initial end position of span.
                            Vector3I end = start + wDirection + (dDirection * span.Length);
                            
                            // Loop through neighboring sequences of planes and try to expand the current span into them.
                            for (int neighbor = width + 1; neighbor < diameter; neighbor ++)
                            {
                                // Break if the current span is unable to expand into the next sequence of planes.
                                if ((span.BitMask & planeSequences[set, height, neighbor]) != span.BitMask) break;
                                
                                // Expand the current span into the neighboring sequence of planes.
                                end += wDirection;
                                
                                // Clear bits from the neighboring sequence of planes to prevent creating overlapping planes.
                                planeSequences[set, height, neighbor] &= ~span.BitMask;
                            }

                            // Generate span mesh data.
                            Vector3I vertexA = start + (dDirection * span.Length);
                            Vector3I vertexB = start;
                            Vector3I vertexC = end - (dDirection * span.Length);
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
        
        // Generate a list of spans.
        private static Queue<Span> QueueSpans(uint sequence)
        {
            // Early return if no bits are set.
            if (sequence == 0) return [];
            
            // Generate ends from sequence.
            Ends ends = new(sequence);

            // Create a placeholder list of spans.
            Queue<Span> spans = [];
            
            // Generate spans from ends.
            while ((ends.LBitMask | ends.RBitMask) != 0)
            {
                // Generate span from ends.
                Span span = new(ends);

                // Generate span's bit mask using its offset and length.
                for (byte bit = span.Offset; bit < span.Offset + span.Length; bit ++)
                {
                    span.BitMask |= (uint)1 << bit;
                }

                // Add span to the list.
                spans.Enqueue(span);

                // Clear bits from ends using the span's bit mask.
                ends.ClearBits(span.BitMask);
            }

            return spans;
        }
    }
}