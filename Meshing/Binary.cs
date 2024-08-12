using Godot;
using RawVoxel.Math;
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
    public static class Voxels
    {
        public static uint[,,] Generate(ref BitArray voxelMasks, int chunkBitshifts)
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
                        binaryVoxels[0, y, z] |= 1u << x;
                        binaryVoxels[1, z, x] |= 1u << y;
                        binaryVoxels[2, x, y] |= 1u << z;
                    }
                }
            }

            return binaryVoxels;
        }
    }
    public static class Planes
    {
        public static uint[,,] Generate(ref uint[,,] binaryVoxels, int chunkBitshifts, Vector3I signBasisZ, bool cullAxes = false)
        {
            int chunkDiameter = 1 << chunkBitshifts;

            uint[,,] binaryPlanes = new uint[6, chunkDiameter, chunkDiameter];

            for (int axis = 0; axis < 3; axis ++)
            {
                int visibleAxisSign = signBasisZ[axis];
                
                // Combined axis signs.
                if (visibleAxisSign == 0 || cullAxes == false)
                {
                    for (int y = 0; y < chunkDiameter; y ++)
                    {
                        for (int z = 0; z < chunkDiameter; z ++)
                        {
                            // Retrieve current voxel seqeuence.
                            uint voxelsCombinedAxis = binaryVoxels[axis, y, z];

                            // Skip if no visible voxels.
                            if (voxelsCombinedAxis == 0) continue;

                            // Extract visible planes from voxel sequence on both axis signs.
                            uint planesNegativeAxis = voxelsCombinedAxis & ~(voxelsCombinedAxis << 1);
                            uint planesPositiveAxis = voxelsCombinedAxis & ~(voxelsCombinedAxis >> 1);

                            // Loop through set bits in visible plane sequences and swizzle them into new sets.
                            while ((planesNegativeAxis | planesPositiveAxis) != 0)
                            {
                                int xNeg = TrailingZeroCount(planesNegativeAxis);
                                int xPos = TrailingZeroCount(planesPositiveAxis);
                                
                                binaryPlanes[(axis << 1) + 0, xNeg, z] |= 1u << y;
                                binaryPlanes[(axis << 1) + 1, xPos, z] |= 1u << y;
                                
                                planesNegativeAxis &= ~(1u << xNeg);
                                planesPositiveAxis &= ~(1u << xPos);
                            }
                        }
                    }
                }
                
                // Negative axis signs.
                else if (visibleAxisSign < 0)
                {
                    for (int y = 0; y < chunkDiameter; y ++)
                    {
                        for (int z = 0; z < chunkDiameter; z ++)
                        {
                            // Retrieve current voxel seqeuence.
                            uint voxelsCombinedAxis = binaryVoxels[axis, y, z];

                            // Skip if no visible voxels.
                            if (voxelsCombinedAxis == 0) continue;
                            
                            // Extract visible planes from voxel sequence on the negative axis sign.
                            uint planesNegativeAxis = voxelsCombinedAxis & ~(voxelsCombinedAxis << 1);
                            
                            // Loop through set bits in visible plane sequence and swizzle them into a new sequence.
                            while (planesNegativeAxis != 0)
                            {
                                int x = TrailingZeroCount(planesNegativeAxis);
                                binaryPlanes[(axis << 1) + 0, x, z] |= 1u << y;
                                planesNegativeAxis &= ~(1u << x);
                            }
                        }
                    }                
                }

                // Positive axis signs.
                else if (visibleAxisSign > 0)
                {
                    for (int y = 0; y < chunkDiameter; y ++)
                    {
                        for (int z = 0; z < chunkDiameter; z ++)
                        {
                            // Retrieve current voxel seqeuence.
                            uint voxelsCombinedAxis = binaryVoxels[axis, y, z];

                            // Skip if no visible voxels.
                            if (voxelsCombinedAxis == 0) continue;
                            
                            // Extract visible planes from voxel sequence on the positive axis sign.
                            uint planesPositiveAxis = voxelsCombinedAxis & ~(voxelsCombinedAxis >> 1);
                            
                            // Loop through set bits in visible plane sequence and swizzle them into a new sequence.
                            while (planesPositiveAxis != 0)
                            {
                                int x = TrailingZeroCount(planesPositiveAxis);
                                binaryPlanes[(axis << 1) + 1, x, z] |= 1u << y;
                                planesPositiveAxis &= ~(1u << x);
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

        public static Surface Generate(ref uint[,,] binaryPlanes, float voxelSize, int chunkDiameter, int axis, int sign)
        {
            Surface surface = new();

            for (int x = 0; x < chunkDiameter; x ++)
            {
                for (int z = 0; z < chunkDiameter; z ++)
                {
                    uint yAxis = binaryPlanes[(axis << 1) + sign, x, z];

                    while (yAxis != 0)
                    {
                        Chain chain = Chain.Generate(yAxis);

                        int y = chain.Offset;
                        int y_span = chain.Length;
                        
                        yAxis &= ~chain.BitMask;

                        Vector3I planeStart = axis switch
                        {
                            0 => new(x + sign, y, z),
                            1 => new(z, x + sign, y),
                            _ => new(y, z, x + sign),
                        };

                        Vector3I planeEnd = axis switch
                        {
                            0 => planeStart + new Vector3I(0, y_span, 1),
                            1 => planeStart + new Vector3I(1, 0, y_span),
                            _ => planeStart + new Vector3I(y_span, 1, 0),
                        };

                        // Loop through neighboring plane sequences and try to expand the current chain into them.
                        for (int nextZ = z + 1; nextZ < chunkDiameter; nextZ ++)
                        {
                            // Retrieve the next plane sequence.
                            ref uint nextYAxis = ref binaryPlanes[(axis << 1) + sign, x, nextZ];

                            // Break if the current chain is unable to expand into the next plane sequence.
                            if ((chain.BitMask & nextYAxis) != chain.BitMask) break;

                            // Expand the current chain into the neighboring plane sequence. (Add relative width)
                            switch (axis)
                            {
                                case 0:  planeEnd.Z ++; break;
                                case 1:  planeEnd.X ++; break;
                                default: planeEnd.Y ++; break;
                            }

                            // Clear bits from the neighboring plane sequence to prevent creating overlapping planes.
                            nextYAxis &= ~chain.BitMask;
                        }

                        // Create vertices.
                        Vector3 vertexUGridPositionA = (Vector3)planeStart * voxelSize; 
                        Vector3 vertexUGridPositionB = (Vector3)planeStart * voxelSize;
                        Vector3 vertexUGridPositionC = (Vector3)planeEnd * voxelSize; 
                        Vector3 vertexUGridPositionD = (Vector3)planeEnd * voxelSize;

                        // Offset vertices A & C. (Offset relative height)
                        switch (axis)
                        {
                            case 0:
                                vertexUGridPositionA.Y += y_span * voxelSize;
                                vertexUGridPositionC.Y -= y_span * voxelSize;
                                break;
                            case 1:
                                vertexUGridPositionA.Z += y_span * voxelSize;
                                vertexUGridPositionC.Z -= y_span * voxelSize;
                                break;
                            default:
                                vertexUGridPositionA.X += y_span * voxelSize;
                                vertexUGridPositionC.X -= y_span * voxelSize;
                                break;
                        }

                        // Get vertex count to offset indices.
                        int vertexCount = surface.Vertices.Count;
                        
                        // Switch vertex draw order.
                        switch (sign)
                        {
                            case 0:
                                surface.Vertices.AddRange([vertexUGridPositionD, vertexUGridPositionC, vertexUGridPositionB, vertexUGridPositionA]);
                                break;
                            case 1:
                                surface.Vertices.AddRange([vertexUGridPositionA, vertexUGridPositionB, vertexUGridPositionC, vertexUGridPositionD]);
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
        public static Surface[] Generate(ref BitArray voxelmasks, float voxelSize, int chunkBitshifts, Vector3I signBasisZ, bool cullAxes = false)
        {
            int chunkDiameter = 1 << chunkBitshifts;

            uint[,,] binaryVoxels = Voxels.Generate(ref voxelmasks, chunkBitshifts);
            uint[,,] binaryPlanes = Planes.Generate(ref binaryVoxels, chunkBitshifts, signBasisZ, cullAxes);
            
            Surface[] surfaces = new Surface[6];

            for (int axis = 0; axis < 3; axis ++)
            {
                int visibleAxisSign = signBasisZ[axis];
                
                if (visibleAxisSign <= 0 || cullAxes == false) // Negative axis signs.
                {
                    surfaces[(axis << 1) + 0] = Surface.Generate(ref binaryPlanes, voxelSize, chunkDiameter, axis, 0);
                }
                
                if (visibleAxisSign >= 0 || cullAxes == false) // Positive axis signs.
                {
                    surfaces[(axis << 1) + 1] = Surface.Generate(ref binaryPlanes, voxelSize, chunkDiameter, axis, 1);
                }
            }

            return surfaces;
        }
    }
}