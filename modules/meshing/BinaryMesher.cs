using Godot;
using RawUtils;
using System.Numerics;
using System.Collections.Generic;
using System;

// !! THIS ONLY WORKS WITH CUBED CHUNK DIMENSIONS THAT ARE A POWER OF TWO. !!

namespace RawVoxel {
    public static class BinaryMesher { 
        private struct SpanEnds
        {
            public int Top;
            public int Btm;
        }
        
        public static void Generate(VoxelContainer voxelContainer) {
            // Calculate the number of bitshifts equivalent to the chunk diameter.
            int diameter = voxelContainer.World.ChunkDiameter;
            int shifts = XYZBitShift.CalculateShifts(diameter);
            
            // Set up column arrays.
            int[,,] voxelColumnSets = new int[3, diameter, diameter];
            int[,,] faceColumnSets = new int[6, diameter, diameter];
            
            // Generate columns of voxel visibility masks for each axis.
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
                            // Merge the voxel's visibility mask into its respective column.
                            voxelColumnSets[0, y, z] |= 1 << x;
                            voxelColumnSets[1, z, x] |= 1 << y;
                            voxelColumnSets[2, x, y] |= 1 << z;
                        }
                    }
                }
            }

            // Generate two columns of face visibility masks for each column of voxel visibility masks.
            for (int voxelColumnSet = 0; voxelColumnSet < 3; voxelColumnSet ++)
            {
                for (int depth = 0; depth < diameter; depth ++)
                {
                    for (int width = 0; width < diameter; width ++)
                    {
                        // Retrieve the current voxel column.
                        int voxelColumn = voxelColumnSets[voxelColumnSet, depth, width];
                        // Generate span ends for voxel column.
                        SpanEnds spanEnds = GenerateSpanEnds(voxelColumn);

                        // Generate masks for opposite voxel faces on the current axis.
                        faceColumnSets[2 * voxelColumnSet + 0, depth, width] = spanEnds.Top;
                        faceColumnSets[2 * voxelColumnSet + 1, width, depth] = spanEnds.Btm;
                    }
                }
            }
        
            // Generate faces for each slice of columns in each face column set.
            for (int faceColumnSet =  0; faceColumnSet < 6; faceColumnSet ++)
            {
                // Set AABB expand directions for the current axis.
                Godot.Vector3 vExpandDirection = Godot.Vector3.Zero;
                Godot.Vector3 hExpandDirection = Godot.Vector3.Zero;
                
                switch (faceColumnSet)
                {
                    case 0 | 1:
                        vExpandDirection = new(0, 1, 0);
                        hExpandDirection = new(0, 0, 1);
                        break;
                    case 2 | 3:
                        vExpandDirection = new(1, 0, 0);
                        hExpandDirection = new(0, 0, 1);
                        break;
                    case 4 | 5:
                        vExpandDirection = new(0, 1, 0);
                        hExpandDirection = new(1, 0, 0);
                        break;
                }

                for (int depth = 0; depth < diameter; depth ++)
                {
                    for (int width = 0; width < diameter; width ++)
                    {
                        // Retrieve the current column of face visibility masks.
                        int faceColumn = faceColumnSets[faceColumnSet, depth, width];
                        
                        // Generate span columns for the current face column.
                        List<int> spans = GenerateSpans(faceColumn);
                    }
                }
            }
        }
        // Break a column into multiple, each containing one span from the original column.
        private static List<int> GenerateSpans(int column)
        {
            List<int> spanColumns = new();
            
            if (column == 0) return spanColumns;
            
            // Generate span ends for face column.
            SpanEnds spanEnds = GenerateSpanEnds(column);
            
            // Loop through span ends and generate spans for the column, bottom to top.
            while ((spanEnds.Top | spanEnds.Btm) > 0)
            {
                // Calculate info about the span's position and vertical size.
                int spanVerticalOffset = BitOperations.TrailingZeroCount(spanEnds.Btm);
                int spanVerticalSize = BitOperations.TrailingZeroCount(spanEnds.Top >> spanVerticalOffset) + 1;

                int spanColumn = 0;
                
                // Generate new span column with no trailing zeros.
                for (int setBit = 0; setBit < spanVerticalSize; setBit ++)
                {
                    spanColumn |= 1 << setBit;
                }
                
                // Add the proper number of trailing zeros to the span column.
                spanColumn <<= spanVerticalOffset;
                
                // Add span column to the list.
                spanColumns.Add(spanColumn);

                // Clear bits for the current span from span ends to allow info about the next span to be calculated.
                spanEnds.Top &= ~spanColumn;
                spanEnds.Btm &= ~spanColumn;
            }

            return spanColumns;
        }
        // Break a column into two, one containing start positions and the other containing the end potitions of each span in the original column.
        private static SpanEnds GenerateSpanEnds(int column)
        {
            return new SpanEnds()
            {
                Top = column & ~(column >> 1),
                Btm = column & ~(column << 1)
            };
        }
    }
}