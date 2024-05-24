using Godot;
using RawUtils;
using System.Numerics;
using System.Collections.Generic;
using System;

// !! THIS ONLY WORKS WITH CUBED CHUNK DIMENSIONS THAT ARE A POWER OF TWO. !!

namespace RawVoxel {
    public static class BinaryMesher { 
        private struct Span
        {
            public int Mask;
            public byte Offset;

            public void GenerateMask(int trailingZeroCount)
            {
                Mask = 0;
                
                if (trailingZeroCount > 0)
                {
                    for (int zeroCount = 0; zeroCount < trailingZeroCount; zeroCount ++)
                    {
                        Mask |= 1 << zeroCount;
                    }
                }
            }
        }
        
        public static void Generate(VoxelContainer voxelContainer) {
            // Calculate the number of bitshifts equivalent to the chunk diameter.
            int diameter = voxelContainer.World.ChunkDiameter;
            int shifts = XYZBitShift.CalculateShifts(diameter);
            
            // Set up mask column arrays.
            int[,,] voxelMasks = new int[3, diameter, diameter];
            int[,,] faceMasks = new int[6, diameter, diameter];
            
            // Generate columns of voxel visibility masks as 32 bit integers.
            for (int x = 0; x < diameter; x ++)
            {
                for (int y = 0; y < diameter; y ++)
                {
                    for (int z = 0; z < diameter; z ++)
                    {
                        // Check if current voxel's visibility mask is true.
                        int voxelIndex = XYZBitShift.XYZToIndex(x, y, z, shifts);
                        if (voxelContainer.VoxelMasks[voxelIndex] == true)
                        {      
                            // Merge the voxel's visibility mask into its respective mask columns.
                            voxelMasks[0, y, z] |= 1 << x;
                            voxelMasks[1, z, x] |= 1 << y;
                            voxelMasks[2, x, y] |= 1 << z;
                        }
                    }
                }
            }

            // Generate columns of voxel face visibility masks as 32 bit integers.
            for (int axis = 0; axis < 3; axis ++)
            {
                for (int top = 0; top < diameter; top ++)
                {
                    for (int btm = 0; btm < diameter; btm ++)
                    {
                        // Retrieve the current column mask.
                        int voxelMask = voxelMasks[axis, top, btm];
                        
                        // Generate masks for the two opposite voxel faces on the current axis.
                        faceMasks[2 * axis + 0, top, btm] = voxelMask & ~(voxelMask << 1);
                        faceMasks[2 * axis + 1, btm, top] = voxelMask & ~(voxelMask >> 1);
                    }
                }
            }
        
            // Generate faces for each span in the column.
            for (int axis =  0; axis < 6; axis ++)
            {
                /*
                // Set AABB expand directions for the current axis.
                Godot.Vector3 vExpandDirection = Godot.Vector3.Zero;
                Godot.Vector3 hExpandDirection = Godot.Vector3.Zero;
                switch (axis)
                {
                    case 0 | 1: // X
                        vExpandDirection = new(0, 1, 0);
                        hExpandDirection = new(0, 0, 1);
                        break;
                    case 2 | 3: // Y
                        vExpandDirection = new(1, 0, 0);
                        hExpandDirection = new(0, 0, 1);
                        break;
                    case 4 | 5: // Z
                        vExpandDirection = new(0, 1, 0);
                        hExpandDirection = new(1, 0, 0);
                        break;
                }
                */

                for (int width = 0; width < diameter; width ++)
                {
                    // Create a new set of spans to track across columns.
                    List<Span> sliceSpans = new();
                    
                    for (int depth = 0; depth < diameter; depth ++)
                    {
                        // Retrieve face colummn visibility mask.
                        int mask = faceMasks[axis, width, depth];

                        // Generate spans from mask.
                        List<Span> maskSpans = GenerateSpans(mask);

                        // Early return if no spans were generated.
                        if (maskSpans.Count == 0) return;


                    }
                }
            }
        }
    
        private static List<Span> GenerateSpans(int mask)
        {
            List<Span> spans = new();
            
            if (mask == 0) return spans;
            
            int mergedMasks = 0;
            byte spanOffset = 0;
            
            // Generate spans.
            while (~mergedMasks != 0)
            {
                // Create a new span.
                Span span = new()
                {
                    Mask = 0,
                    Offset = spanOffset
                };
                
                // Add new span to the spans list.
                spans.Add(span);

                // Bitshift the mask to the current span.
                mask >>= spanOffset;
                
                // Get the inverted mask's trailing zero count.
                int trailingZeroCount = BitOperations.TrailingZeroCount(~mask);

                // Generate the span's mask.
                span.GenerateMask(trailingZeroCount);

                // Merge inverted mask with span mask.
                mergedMasks = ~mask | span.Mask;
                
                // Update the number of bitshifts required to offset to the next span.
                spanOffset += (byte)BitOperations.TrailingZeroCount(~mergedMasks);
            }

            return spans;
        }
    }
}