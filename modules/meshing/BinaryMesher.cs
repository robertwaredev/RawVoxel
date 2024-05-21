using Godot;
using RawUtils;
using System;
using System.Collections.Generic;

// !! THIS ONLY WORKS WITH CUBED CHUNK DIMENSIONS THAT ARE A POWER OF TWO. !!

namespace RawVoxel {
    public static class BinaryMesher { 
        // FIXME - Having these in this scope will become a problem
        public static readonly List<Vector3> Vertices = new();
        public static readonly List<Vector3> Normals = new();
        public static readonly List<Color> Colors = new();
        public static readonly List<int> Indices = new();
        
        public static void Generate(VoxelContainer voxelContainer) {
            // Calculate the required bitshifts based on the chunk diameter.
            int chunkDiameter = voxelContainer.World.ChunkDiameter;
            int shifts = XYZBitShift.CalculateShifts(chunkDiameter);
            
            // Setup mask arrays.
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
                        // Retrieve the current axis' "column" mask.
                        int axisMask = axisMasks[axis, top, btm];
                        
                        // TODO - Instead of storing face masks, could they be used to generate face mesh data on the fly and then discarded?
                        
                        // Generate the face mask "columns" in the faceMasks array for "top" and "bottom" faces.
                        faceMasks[2 * axis + 0, top, btm] = axisMask & ~(axisMask << 1);    // TOP -> BTM
                        faceMasks[2 * axis + 1, btm, top] = axisMask & ~(axisMask >> 1);    // BTM -> TOP
                    }
                }
            }
        
            // Generate "top" and "bottom" faces for each "column".
            for (int axis =  0; axis < 3; axis ++)
            {
                for (int top = 0; top < chunkDiameter - 1; top ++)
                {
                    for (int btm = 0; btm < chunkDiameter - 1; btm ++)
                    {
                        int thisFaceMaskTop = faceMasks[2 * axis + 0, top, btm];
                        int thisFaceMaskBtm = faceMasks[2 * axis + 1, btm, top];
                        
                        int nextFaceMaskTop = faceMasks[2 * axis + 0, top + 1, btm];
                        int nextFaceMaskBtm = faceMasks[2 * axis + 1, btm + 1, top];
                        
                        Span<bool> masks = thisFaceMaskTop;
                        
                        if ((thisFaceMaskTop & nextFaceMaskTop) >= thisFaceMaskTop)
                        {
                        }

                        if ((thisFaceMaskBtm & nextFaceMaskBtm) >= thisFaceMaskBtm)
                        {

                        }
                    }
                }
            }
        }
    }
}