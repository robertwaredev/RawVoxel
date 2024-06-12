using Godot;
using RawUtils;
using System.Collections.Generic;
using System.Diagnostics;
using static System.Numerics.BitOperations;

// !! THIS ONLY WORKS WITH CUBED CHUNK DIMENSIONS THAT ARE A POWER OF TWO. !!

// TODO - Skip the generation section for homogenous chunks.

namespace RawVoxel {
    public static class BinaryMesher { 
        // Generate two bit masks from the specified "sequence" containing left and right ends of "spans" of bits from the sequence.
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
        
        // Generate binary mesh for the specified chunk.
        public static void Generate(ref Chunk chunk)
        {
            // TODO - Check if all voxels in the chunk are solid.
            /* if (chunk.VoxelMasks.HasAllSet())
            {
                return;
            } */

            #region Variables

            // Store chunk diameter with a shorter name so I have to type less.
            byte diameter = (byte)chunk.World.ChunkDiameter;
            
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
            List<Color> Colors = [];
            List<int> Indices = [];

            #endregion Variables
                
            // Generate sequences of voxel bit masks in sets, one for each axis.
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

            // Generate sequences of plane bit masks in sets, two for each axis.
            for (int set = 0; set < 3; set ++)
            {
                for (int depth = 0; depth < diameter; depth ++)
                {
                    for (int width = 0; width < diameter; width ++)
                    {
                        // Generate bit masks for left and right ends of spans in the current sequence. (visible planes/faces)
                        Ends ends = new(voxelSequences[set, depth, width]);
                        
                        // Loop thru each voxel at the given height.
                        for(int height = 0; height < diameter; height ++)
                        {
                            // Check if voxel at the current height has a "left" plane.
                            if ((ends.LBitMask & (1 << height)) != 0)
                            {
                                // Merge "left" plane bit mask into its respective sequence.
                                planeSequences[(set << 1) + 0, height, width] |= (uint)1 << depth;
                            }
                            
                            // Check if voxel at the current height has a "right" plane.
                            if ((ends.RBitMask & (1 << height)) != 0)
                            {
                                // Merge "right" plane bit mask into its respective sequence.
                                planeSequences[(set << 1) + 1, height, width] |= (uint)1 << depth;
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
                    case 0: case 1: // X axis sequences.
                        wDirection = new(0, 0, 1);  // Z
                        hDirection = new(1, 0, 0);  // X
                        dDirection = new(0, 1, 0);  // Y
                        break;
                    case 2: case 3: // Y axis sequences.
                        wDirection = new(1, 0, 0);  // X
                        hDirection = new(0, 1, 0);  // Y
                        dDirection = new(0, 0, 1);  // Z
                        break;
                    case 4: case 5: // Z axis sequences.
                        wDirection = new(0, 1, 0);  // Y
                        hDirection = new(0, 0, 1);  // Z
                        dDirection = new(1, 0, 0);  // X
                        break;
                }

                // Loop through plane sequences.
                for (int height = 0; height < diameter; height ++)
                {
                    for (int width = 0; width < diameter; width ++)
                    {
                        // Retrieve the current plane sequence.
                        uint thisPlaneSequence = planeSequences[set, height, width];
                        
                        // Generate spans for the current plane sequence.
                        List<Span> spans = GenerateSpans(thisPlaneSequence);

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
                            
                            // Loop through neighboring plane sequences and try to expand the current span into them.
                            for (int nextWidth = width + 1; nextWidth < diameter; nextWidth ++)
                            {
                                // Retrieve the next plane sequence.
                                uint nextPlaneSequence = planeSequences[set, height, nextWidth];

                                // Check if the current span can expand into the next plane sequence.
                                if ((span.BitMask & nextPlaneSequence) == span.BitMask)
                                {
                                    // Expand the current span into the neighboring plane sequence.
                                    end += wDirection;
                                    
                                    // Clear bits from the neighboring plane sequence.
                                    planeSequences[set, height, nextWidth] &= ~span.BitMask;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            // Generate span mesh data.
                            Vector3I vertexA = start + (dDirection * span.Length);
                            Vector3I vertexB = start;
                            Vector3I vertexC = end - (dDirection * span.Length);
                            Vector3I vertexD = end;

                            int offset = Vertices.Count;

                            // FIXME - Current color is just the normal direction.
                            Color color = new()
                            {
                                R = hDirection.X,
                                G = hDirection.Y,
                                B = hDirection.Z,
                            };

                            // Add mesh data to lists.
                            switch (set)
                            {
                                case 0: case 2: case 4:
                                    Vertices.AddRange([vertexA, vertexB, vertexC, vertexD]);
                                    Normals.AddRange([hDirection, hDirection, hDirection, hDirection]);
                                    Colors.AddRange([color, color, color, color]);
                                    break;
                                default:
                                    Vertices.AddRange([vertexD, vertexC, vertexB, vertexA]);
                                    Normals.AddRange([-hDirection, -hDirection, -hDirection, -hDirection]);
                                    Colors.AddRange([-color, -color, -color, -color]);
                                    break;
                            }

                            Indices.AddRange([0 + offset, 1 + offset, 2 + offset, 0 + offset, 2 + offset, 3 + offset]);
                        }
                    }
                }
            }
            
            // Generate mesh.
            MeshHelper.Generate(ref chunk, ref Vertices, ref Normals, ref Colors, ref Indices);
        }
        
        // Generate a list of spans.
        private static List<Span> GenerateSpans(uint sequence)
        {
            // Early return if no bits are set.
            if (sequence == 0) return [];
            
            // Generate ends from sequence.
            Ends ends = new(sequence);

            // Create a placeholder list of spans.
            List<Span> spans = [];
            
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
                spans.Add(span);

                // Clear bits from ends using the span's bit mask.
                ends.ClearBits(span.BitMask);
            }

            return spans;
        }
    }
}