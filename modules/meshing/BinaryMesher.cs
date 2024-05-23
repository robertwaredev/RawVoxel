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
        }
        
        public static void Generate(VoxelContainer voxelContainer) {
            // Calculate the number of bitshifts equivalent to the chunk diameter.
            int chunkDiameter = voxelContainer.World.ChunkDiameter;
            int shifts = XYZBitShift.CalculateShifts(chunkDiameter);
            
            // Set up mask arrays.
            int[,,] axisMasks = new int[3, chunkDiameter, chunkDiameter];
            int[,,] faceMasks = new int[6, chunkDiameter, chunkDiameter];
            
            // Generate "columns" of axis visibility masks as 32 bit integers.
            for (int x = 0; x < chunkDiameter; x ++)
            {
                for (int y = 0; y < chunkDiameter; y ++)
                {
                    for (int z = 0; z < chunkDiameter; z ++)
                    {
                        // Check if current voxel's visibility mask is true.
                        if (voxelContainer.VoxelMasks[(x << shifts << shifts) + (y << shifts) + z] == true)
                        {      
                            // Merge the voxel's visibility mask into its respective "columns" in the axisMasks array.
                            axisMasks[0, y, z] |= 1 << x;
                            axisMasks[1, z, x] |= 1 << y;
                            axisMasks[2, x, y] |= 1 << z;
                        }
                    }
                }
            }

            // Generate "columns" of face visibility masks as 32 bit integers.
            for (int axis = 0; axis < 3; axis ++)
            {
                for (int top = 0; top < chunkDiameter; top ++)
                {
                    for (int btm = 0; btm < chunkDiameter; btm ++)
                    {
                        // Retrieve the current axis' column mask.
                        int axisMask = axisMasks[axis, top, btm];
                        
                        // TODO - Instead of storing face masks, could they be used to generate face mesh data on the fly and then discarded?
                        
                        // Generate the face mask "columns" in the faceMasks array for "top" and "bottom" faces.
                        faceMasks[2 * axis + 0, top, btm] = axisMask & ~(axisMask << 1);    // TOP -> BTM
                        faceMasks[2 * axis + 1, btm, top] = axisMask & ~(axisMask >> 1);    // BTM -> TOP
                    }
                }
            }
        
            // Generate top and bottom faces for each column.
            for (int axis =  0; axis < 3; axis ++)
            {
                // Set face's AABB expand directions for the current axis.
                Godot.Vector3 vExpandDirection = Godot.Vector3.Zero;
                Godot.Vector3 hExpandDirection = Godot.Vector3.Zero;
                switch (axis)
                {
                    case 0: // X
                        vExpandDirection = new(0, 1, 0);
                        hExpandDirection = new(0, 0, 1);
                        break;
                    case 1: // Y
                        vExpandDirection = new(1, 0, 0);
                        hExpandDirection = new(0, 0, 1);
                        break;
                    case 2: // Z
                        vExpandDirection = new(0, 1, 0);
                        hExpandDirection = new(1, 0, 0);
                        break;
                }
                
                // Generate faces.
                for (int top = 0; top < chunkDiameter - 1; top ++)
                {
                    for (int btm = 0; btm < chunkDiameter - 1; btm ++)
                    {
                        // Retrieve "colummn" mask.
                        int mask = faceMasks[2 * axis + 0, top, btm];

                        // Generate mask spans.
                        List<Span> spans = GenerateSpans(mask);
                    }
                }
            }
        }
    
        private static List<Span> GenerateSpans(int mask)
        {
            List<Span> spans = new();
            
            if (mask == 0) return spans;
            
            int mergedMasks = 0;
            byte bitshiftOffset = 0;
            
            // Generate spans.
            while (~mergedMasks != 0)
            {
                // Create a new span.
                Span span = new()
                {
                    Mask = 0,
                    Offset = bitshiftOffset
                };
                
                // Add new span to the spans list.
                spans.Add(span);

                // Bitshift the mask to the current span.
                mask >>= bitshiftOffset;
                
                // Get the inverted mask's trailing zero count.
                int trailingZeroCount = BitOperations.TrailingZeroCount(~mask);

                // Generate the span's mask.
                if (trailingZeroCount > 0)
                {
                    for (int zeroCount = 0; zeroCount < trailingZeroCount; zeroCount ++)
                    {
                        span.Mask |= 1 << zeroCount;
                    }
                }

                // Merge inverted mask with span mask.
                mergedMasks = ~mask | span.Mask;
                
                // Update the number of bitshifts required to offset to the next span.
                bitshiftOffset += (byte)BitOperations.TrailingZeroCount(~mergedMasks);
            }

            return spans;
        }
    }
}