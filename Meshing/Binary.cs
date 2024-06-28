using Godot;
using RawVoxel.Math;
using RawVoxel.Resources;
using System.Collections;
using System.Collections.Generic;
using static System.Numerics.BitOperations;

namespace RawVoxel.Meshing;

// TODO - Chunk edge detection in neighboring chunks.
// TODO - LODs

// X AXIS SWIZZLES     || Y AXIS SWIZZLES     || Z AXIS SWIZZLES
// =================================================================
// Z = relative width  || X = relative width  || Y = relative width
// X = relative height || Y = relative height || Z = relative height
// Y = relative depth  || Z = relative depth  || X = relative depth

public static class Binary
{
    public struct Chain
    {
        public uint BitMask;
        public byte Offset;
        public byte Length;

        public static Chain Generate(uint sequence)
        {
            byte offset = (byte)TrailingZeroCount(sequence);
            byte length = (byte)TrailingZeroCount(~(sequence >> offset));
            uint bitMask = uint.MaxValue >> (32 - length) << offset;

            return new()
            {
                BitMask = bitMask,
                Offset = offset,
                Length = length,
            };
        }
    }
    public static class Chains
    {
        public static Queue<Chain> Generate(uint sequence)
        {
            Queue<Chain> chains = [];
            
            // Generate chains from sequence.
            while (sequence != 0)
            {
                // Create new chain.
                Chain chain = Chain.Generate(sequence);

                // Add chain to the list.
                chains.Enqueue(chain);

                // Clear bits from sequence using the chain's bit mask.
                sequence &= ~chain.BitMask;
            }

            return chains;
        }
    }
    public static class Voxels
    {
        public static uint[,,] Generate(ref BitArray voxelMasks, int chunkBitshifts, Biome biome)
        {
            int chunkDiameter = 1 << chunkBitshifts;

            uint[,,] binaryVoxels = new uint[3, chunkDiameter, chunkDiameter];

            for (int x = 0; x < chunkDiameter; x ++)
            {
                for (int y = 0; y < chunkDiameter; y ++)
                {
                    for (int z = 0; z < chunkDiameter; z ++)
                    {
                        // Skip if current voxel is not solid.
                        if (voxelMasks[XYZ.Encode(x, y, z, chunkBitshifts)] == false) continue;

                        // Merge voxel bit mask into its respective sequence.
                        binaryVoxels[0, y, z] |= (uint)1 << x;
                        binaryVoxels[1, z, x] |= (uint)1 << y;
                        binaryVoxels[2, x, y] |= (uint)1 << z;
                    }
                }
            }

            return binaryVoxels;
        }
    }
    public static class Planes
    {
        public static uint[,,] Generate(ref uint[,,] binaryVoxels, int chunkDiameter, Vector3I signBasisZ, bool cullAxes = false)
        {
            uint[,,] binaryPlanes = new uint[6, chunkDiameter, chunkDiameter];

            for (int axis = 0; axis < 3; axis ++)
            {
                int visibleAxisSign = signBasisZ[axis];
                
                // Combined axis signs.
                if (visibleAxisSign == 0 || cullAxes == false)
                {
                    for (int depth = 0; depth < chunkDiameter; depth ++)
                    {
                        for (int width = 0; width < chunkDiameter; width ++)
                        {
                            // Retrieve current voxel seqeuence.
                            uint voxelSequence = binaryVoxels[axis, depth, width];

                            // Skip if no visible voxels.
                            if (voxelSequence == 0) continue;

                            // Extract visible planes from voxel sequence on both axis signs.
                            uint planeSequenceNegative = voxelSequence & ~(voxelSequence << 1);
                            uint planeSequencePositive = voxelSequence & ~(voxelSequence >> 1);

                            // Loop through set bits in visible plane sequences and swizzle them into new sets.
                            while ((planeSequenceNegative | planeSequencePositive) != 0)
                            {
                                int heightNegative = TrailingZeroCount(planeSequenceNegative);
                                int heightPositive = TrailingZeroCount(planeSequencePositive);
                                
                                binaryPlanes[(axis << 1) + 0, heightNegative, width] |= (uint)1 << depth;
                                binaryPlanes[(axis << 1) + 1, heightPositive, width] |= (uint)1 << depth;
                                
                                planeSequenceNegative &= ~(uint)(1 << heightNegative);
                                planeSequencePositive &= ~(uint)(1 << heightPositive);
                            }
                        }
                    }
                }
                
                // Negative axis signs.
                else if (visibleAxisSign < 0)
                {
                    for (int depth = 0; depth < chunkDiameter; depth ++)
                    {
                        for (int width = 0; width < chunkDiameter; width ++)
                        {
                            // Retrieve current voxel seqeuence.
                            uint voxelSequence = binaryVoxels[axis, depth, width];

                            // Skip if no visible voxels.
                            if (voxelSequence == 0) continue;
                            
                            // Extract visible planes from voxel sequence on the negative axis sign.
                            uint planeSequenceNegative = voxelSequence & ~(voxelSequence << 1);
                            
                            // Loop through set bits in visible plane sequence and swizzle them into a new sequence.
                            while (planeSequenceNegative != 0)
                            {
                                int heightNegative = TrailingZeroCount(planeSequenceNegative);
                                binaryPlanes[(axis << 1) + 0, heightNegative, width] |= (uint)1 << depth;
                                planeSequenceNegative &= ~(uint)(1 << heightNegative);
                            }
                        }
                    }                
                }

                // Positive axis signs.
                else if (visibleAxisSign > 0)
                {
                    for (int depth = 0; depth < chunkDiameter; depth ++)
                    {
                        for (int width = 0; width < chunkDiameter; width ++)
                        {
                            // Retrieve current voxel seqeuence.
                            uint voxelSequence = binaryVoxels[axis, depth, width];

                            // Skip if no visible voxels.
                            if (voxelSequence == 0) continue;
                            
                            // Extract visible planes from voxel sequence on the positive axis sign.
                            uint planeSequencePositive = voxelSequence & ~(voxelSequence >> 1);
                            
                            // Loop through set bits in visible plane sequence and swizzle them into a new sequence.
                            while (planeSequencePositive != 0)
                            {
                                int heightPositive = TrailingZeroCount(planeSequencePositive);
                                binaryPlanes[(axis << 1) + 1, heightPositive, width] |= (uint)1 << depth;
                                planeSequencePositive &= ~(uint)(1 << heightPositive);
                            }
                        }
                    }                
                }
            }

            return binaryPlanes;
        }
    }
    public class Surface
    {
        public List<Vector3> Vertices = [];
        public List<Color> Colors = [];
        public List<int> Indices = [];

        public static Surface Generate(ref uint[,,] binaryPlanes, int chunkDiameter, int axis, int axisOffset)
        {
            Surface surface = new();

            for (int height = 0; height < chunkDiameter; height ++)
            {
                for (int width = 0; width < chunkDiameter; width ++)
                {
                    uint planeSequence = binaryPlanes[(axis << 1) + axisOffset, height, width];

                    if (planeSequence == 0) continue;
                    
                    Queue<Chain> chains = Chains.Generate(planeSequence);

                    foreach (Chain chain in chains)
                    {
                        int depth = chain.Offset;
                        int length = chain.Length;

                        Vector3I chainStart = axis switch
                        {
                            0 => new Vector3I(height + axisOffset, depth, width),
                            1 => new Vector3I(width, height + axisOffset, depth),
                            _ => new Vector3I(depth, width, height + axisOffset),
                        };

                        Vector3I chainEnd = axis switch
                        {
                            0 => chainStart + new Vector3I(0, length, 1),
                            1 => chainStart + new Vector3I(1, 0, length),
                            _ => chainStart + new Vector3I(length, 1, 0),
                        };

                        // Loop through neighboring plane sequences and try to expand the current chain into them.
                        for (int nextWidth = width + 1; nextWidth < chunkDiameter; nextWidth ++)
                        {
                            // Retrieve the next plane sequence.
                            ref uint nextPlaneSequence = ref binaryPlanes[(axis << 1) + axisOffset, height, nextWidth];

                            // Break if the current chain is unable to expand into the next plane sequence.
                            if ((chain.BitMask & nextPlaneSequence) != chain.BitMask) break;

                            // Expand the current chain into the neighboring plane sequence. (Add relative width)
                            switch (axis)
                            {
                                case 0:  chainEnd.Z ++; break;
                                case 1:  chainEnd.X ++; break;
                                default: chainEnd.Y ++; break;
                            }

                            // Clear bits from the neighboring plane sequence to prevent creating overlapping planes.
                            nextPlaneSequence &= ~chain.BitMask;
                        }

                        // Create vertices.
                        Vector3I vertexGridPositionA = chainStart; 
                        Vector3I vertexGridPositionB = chainStart;
                        Vector3I vertexGridPositionC = chainEnd; 
                        Vector3I vertexGridPositionD = chainEnd;

                        // Offset vertices A & C. (Offset relative height)
                        switch (axis)
                        {
                            case 0:
                                vertexGridPositionA.Y += chain.Length;
                                vertexGridPositionC.Y -= chain.Length;
                                break;
                            case 1:
                                vertexGridPositionA.Z += chain.Length;
                                vertexGridPositionC.Z -= chain.Length;
                                break;
                            default:
                                vertexGridPositionA.X += chain.Length;
                                vertexGridPositionC.X -= chain.Length;
                                break;
                        }

                        // Get get vertex count to offset indices.
                        int vertexCount = surface.Vertices.Count;
                        
                        // Switch vertex draw order.
                        switch (axisOffset)
                        {
                            case 0:
                                surface.Vertices.AddRange([vertexGridPositionD, vertexGridPositionC, vertexGridPositionB, vertexGridPositionA]);
                                break;
                            case 1:
                                surface.Vertices.AddRange([vertexGridPositionA, vertexGridPositionB, vertexGridPositionC, vertexGridPositionD]);
                                break;
                        }

                        // Add indices to their list.
                        surface.Indices.AddRange([0 + vertexCount, 1 + vertexCount, 2 + vertexCount, 0 + vertexCount, 2 + vertexCount, 3 + vertexCount]);
                    }
                }
            }

            return surface;
        }
    }
    public static class Surfaces
    {
        public static Surface[] Generate(ref uint[,,] binaryPlanes, int chunkDiameter, Vector3I signBasisZ, bool cullAxes = false)
        {
            Surface[] surfaces = new Surface[6];

            for (int axis = 0; axis < 3; axis ++)
            {
                int visibleAxisSign = signBasisZ[axis];
                
                if (visibleAxisSign <= 0 || cullAxes == false) // Negative axis signs.
                {
                    surfaces[(axis << 1) + 0] = Surface.Generate(ref binaryPlanes, chunkDiameter, axis, 0);
                }
                
                if (visibleAxisSign >= 0 || cullAxes == false) // Positive axis signs.
                {
                    surfaces[(axis << 1) + 1] = Surface.Generate(ref binaryPlanes, chunkDiameter, axis, 1);
                }
            }

            return surfaces;
        }
    }
    
    public static class Mesher // The original, merged function.
    {
        public static Surface[] GenerateSurfaces(ref BitArray voxelMasks, int chunkBitshifts, Vector3I signBasisZ, bool cullAxes = true)
        {
            int chunkDiameter = 1 << chunkBitshifts;

            // Sequences of voxel visibility bit masks, stored as [axis, relative depth, relative width] with relative height encoded into each sequence's bits.
            uint[,,] binaryVoxels = new uint[3, chunkDiameter, chunkDiameter];
            
            // Sequences of plane visibility bit masks, stored as [axis, relative height, relative width] with relative depth encoded into each sequence's bits.
            uint[,,] binaryPlanes = new uint[6, chunkDiameter, chunkDiameter];
            
            // Surface array for storing ArrayMesh data, one Surface per axis sign.
            Surface[] surfaces = new Surface[6];
            
            // Loop through axes and encode visible voxel positions into bit mask sequences.
            for (int x = 0; x < chunkDiameter; x ++)
            {
                for (int y = 0; y < chunkDiameter; y ++)
                {
                    for (int z = 0; z < chunkDiameter; z ++)
                    {
                        // Skip if current voxel is not solid.
                        if (voxelMasks[XYZ.Encode(x, y, z, chunkBitshifts)] == false) continue;

                        // Merge voxel bit mask into its respective sequence.
                        binaryVoxels[0, y, z] |= (uint)1 << x;
                        binaryVoxels[1, z, x] |= (uint)1 << y;
                        binaryVoxels[2, x, y] |= (uint)1 << z;
                    }
                }
            }
            
            // Loop through axes and encode visible plane positions into bit mask sequences.
            for (int axis = 0; axis < 3; axis ++)
            {
                // Determine which axis sign is visible.
                int visibleAxisSign = signBasisZ[axis];
                
                // Combined axis signs.
                if (visibleAxisSign == 0 || cullAxes == false)
                {
                    for (int depth = 0; depth < chunkDiameter; depth ++)
                    {
                        for (int width = 0; width < chunkDiameter; width ++)
                        {
                            // Retrieve current voxel seqeuence.
                            uint voxelSequence = binaryVoxels[axis, depth, width];

                            // Skip if no visible voxels.
                            if (voxelSequence == 0) continue;

                            // Extract visible planes from voxel sequence on both axis signs.
                            uint planeSequenceNegative = voxelSequence & ~(voxelSequence << 1);
                            uint planeSequencePositive = voxelSequence & ~(voxelSequence >> 1);

                            // Loop through set bits in visible plane sequences and swizzle them into new sets.
                            while ((planeSequenceNegative | planeSequencePositive) != 0)
                            {
                                int heightNegative = TrailingZeroCount(planeSequenceNegative);
                                int heightPositive = TrailingZeroCount(planeSequencePositive);
                                
                                binaryPlanes[(axis << 1) + 0, heightNegative, width] |= (uint)1 << depth;
                                binaryPlanes[(axis << 1) + 1, heightPositive, width] |= (uint)1 << depth;
                                
                                planeSequenceNegative &= ~(uint)(1 << heightNegative);
                                planeSequencePositive &= ~(uint)(1 << heightPositive);
                            }
                        }
                    }
                }
                
                // Negative axis signs.
                else if (visibleAxisSign < 0)
                {
                    for (int depth = 0; depth < chunkDiameter; depth ++)
                    {
                        for (int width = 0; width < chunkDiameter; width ++)
                        {
                            // Retrieve current voxel seqeuence.
                            uint voxelSequence = binaryVoxels[axis, depth, width];

                            // Skip if no visible voxels.
                            if (voxelSequence == 0) continue;
                            
                            // Extract visible planes from voxel sequence on the negative axis sign.
                            uint planeSequenceNegative = voxelSequence & ~(voxelSequence << 1);
                            
                            // Loop through set bits in visible plane sequence and swizzle them into a new sequence.
                            while (planeSequenceNegative != 0)
                            {
                                int heightNegative = TrailingZeroCount(planeSequenceNegative);
                                binaryPlanes[(axis << 1) + 0, heightNegative, width] |= (uint)1 << depth;
                                planeSequenceNegative &= ~(uint)(1 << heightNegative);
                            }
                        }
                    }                
                }

                // Positive axis signs.
                else if (visibleAxisSign > 0)
                {
                    for (int depth = 0; depth < chunkDiameter; depth ++)
                    {
                        for (int width = 0; width < chunkDiameter; width ++)
                        {
                            // Retrieve current voxel seqeuence.
                            uint voxelSequence = binaryVoxels[axis, depth, width];

                            // Skip if no visible voxels.
                            if (voxelSequence == 0) continue;
                            
                            // Extract visible planes from voxel sequence on the positive axis sign.
                            uint planeSequencePositive = voxelSequence & ~(voxelSequence >> 1);
                            
                            // Loop through set bits in visible plane sequence and swizzle them into a new sequence.
                            while (planeSequencePositive != 0)
                            {
                                int heightPositive = TrailingZeroCount(planeSequencePositive);
                                binaryPlanes[(axis << 1) + 1, heightPositive, width] |= (uint)1 << depth;
                                planeSequencePositive &= ~(uint)(1 << heightPositive);
                            }
                        }
                    }                
                }
            }
                
            // Loop through axes and generate surfaces from plane bit mask sequences.
            for (int axis = 0; axis < 3; axis ++)
            {
                // Determine which axis sign is visible.
                int visibleAxisSign = signBasisZ[axis];
                
                // Negative axis signs.
                if (visibleAxisSign <= 0 || cullAxes == false)
                {
                    surfaces[(axis << 1) + 0] = Surface.Generate(ref binaryPlanes, chunkDiameter, axis, 0);
                }
                
                // Positive axis signs.
                if (visibleAxisSign >= 0 || cullAxes == false)
                {
                    surfaces[(axis << 1) + 1] = Surface.Generate(ref binaryPlanes, chunkDiameter, axis, 1);
                }
            }
        
            return surfaces;
        }
    }
}