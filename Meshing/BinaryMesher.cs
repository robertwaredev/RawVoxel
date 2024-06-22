using Godot;
using RawVoxel.Math.Binary;
using RawVoxel.Math.Conversions;
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

/// <summary>
/// Binary greedy meshing for voxel chunks.
/// Generate
/// THIS MESHER MERGES ALL SOLID VOXEL TYPES!
/// TEXTURING MUST BE DONE IN THE SHADER!
/// </summary>
public static class BinaryMesher
{
    /// <summary>
    /// Generate a <c>Surface</c> array containing mesh data for each axis sign.
    /// </summary>
    /// <param name="voxels">Byte array containing voxel IDs.</param>
    /// <param name="chunkDiameter">Chunk diameter in voxel units.</param>
    /// <param name="chunkBitshifts">Equivalent to chunk diameter as 1 bitshifted left.</param>
    /// <param name="signBasisZ">Signed <c>Node3D.Transform.Basis.Z</c>.</param>
    /// <param name="cullGeometry">Whether axis signs sohuld be culled based on signBasisZ.</param>
    /// <returns>A <c>Surface</c> array containing vertex and index data for each axis sign.</returns>
    public static Surface[] GenerateSurfaces(ref byte[] voxels, int chunkDiameter, int chunkBitshifts, Vector3I signBasisZ, bool cullGeometry = false)
    {
        // Sequences of voxel visibility bit masks, stored as [axis, relative depth, relative width] with relative height encoded into each sequence's bits.
        uint[,,] voxelSequences = new uint[3, chunkDiameter, chunkDiameter];
        
        // Sequences of plane visibility bit masks, stored as [axis, relative height, relative width] with relative depth encoded into each sequence's bits.
        uint[,,] planeSequences = new uint[6, chunkDiameter, chunkDiameter];
        
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
                    if (voxels[XYZBitShift.XYZToIndex(x, y, z, chunkBitshifts)] == 0) continue;

                    // Merge voxel bit mask into its respective sequence.
                    voxelSequences[0, y, z] |= (uint)1 << x;
                    voxelSequences[1, z, x] |= (uint)1 << y;
                    voxelSequences[2, x, y] |= (uint)1 << z;
                }
            }
        }
        
        // Loop through axes and encode visible plane positions into bit mask sequences.
        for (int axis = 0; axis < 3; axis ++)
        {
            // Determine which axis sign is visible.
            int visibleAxisSign = signBasisZ[axis];
            
            // Combined axis signs.
            if (visibleAxisSign == 0 || cullGeometry == false)
            {
                for (int depth = 0; depth < chunkDiameter; depth ++)
                {
                    for (int width = 0; width < chunkDiameter; width ++)
                    {
                        // Retrieve current voxel seqeuence.
                        uint voxelSequence = voxelSequences[axis, depth, width];

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
                            
                            planeSequences[(axis << 1) + 0, heightNegative, width] |= (uint)1 << depth;
                            planeSequences[(axis << 1) + 1, heightPositive, width] |= (uint)1 << depth;
                            
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
                        uint voxelSequence = voxelSequences[axis, depth, width];

                        // Skip if no visible voxels.
                        if (voxelSequence == 0) continue;
                        
                        // Extract visible planes from voxel sequence on the negative axis sign.
                        uint planeSequenceNegative = voxelSequence & ~(voxelSequence << 1);
                        
                        // Loop through set bits in visible plane sequence and swizzle them into a new sequence.
                        while (planeSequenceNegative != 0)
                        {
                            int heightNegative = TrailingZeroCount(planeSequenceNegative);
                            planeSequences[(axis << 1) + 0, heightNegative, width] |= (uint)1 << depth;
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
                        uint voxelSequence = voxelSequences[axis, depth, width];

                        // Skip if no visible voxels.
                        if (voxelSequence == 0) continue;
                        
                        // Extract visible planes from voxel sequence on the positive axis sign.
                        uint planeSequencePositive = voxelSequence & ~(voxelSequence >> 1);
                        
                        // Loop through set bits in visible plane sequence and swizzle them into a new sequence.
                        while (planeSequencePositive != 0)
                        {
                            int heightPositive = TrailingZeroCount(planeSequencePositive);
                            planeSequences[(axis << 1) + 1, heightPositive, width] |= (uint)1 << depth;
                            planeSequencePositive &= ~(uint)(1 << heightPositive);
                        }
                    }
                }                
            }
        }
            
        // Loop through axes and generate surfaces from plane sequences.
        for (int axis = 0; axis < 3; axis ++)
        {
            // Determine which axis sign is visible.
            int visibleAxisSign = signBasisZ[axis];
            
            // Negative axis signs.
            if (visibleAxisSign <= 0 || cullGeometry == false)
            {
                surfaces[(axis << 1) + 0] = GenerateSurface(ref planeSequences, chunkDiameter, axis, 0);
            }
            
            // Positive axis signs.
            if (visibleAxisSign >= 0 || cullGeometry == false)
            {
                surfaces[(axis << 1) + 1] = GenerateSurface(ref planeSequences, chunkDiameter, axis, 1);
            }
        }
    
        return surfaces;
    }

    /// <summary>
    /// Generate a <c>Surface</c> containing mesh data for the specified axis sign.
    /// </summary>
    /// <param name="planeSequences"></param>
    /// <param name="chunkDiameter">Chunk diameter in voxel units.</param>
    /// <param name="axis">X, Y, or Z axis represented as 0, 1, or 2 respectively.</param>
    /// <param name="axisOffset">Negative or positive axis sign, represented as 0 or 1 respectively.</param>
    /// <returns>A <c>Surface</c> containing vertex and index data for the specified axis sign.</returns>
    private static Surface GenerateSurface(ref uint[,,] planeSequences, int chunkDiameter, int axis, int axisOffset)
    {
        // Create surface to contain mesh data.
        Surface surface = new();

        // Loop through plane sequences and generate mesh data.
        for (int height = 0; height < chunkDiameter; height ++)
        {
            for (int width = 0; width < chunkDiameter; width ++)
            {
                // Retrieve current plane sequence.
                uint planeSequence = planeSequences[(axis << 1) + axisOffset, height, width];

                // Skip if no visible planes.
                if (planeSequence == 0) continue;
                
                // Generate chains from the current plane sequence.
                Queue<Chain> chains = Chain.QueueChains(planeSequence);

                // Loop through chains and generate mesh data.
                foreach (Chain chain in chains)
                {
                    // Store chain offset and length as "depth" and "length" for clarity purposes.
                    int depth = chain.Offset;
                    int length = chain.Length;

                    // Calculate chain start position. (Welcome to Swizzler, may I take your order?)
                    Vector3I chainStart = axis switch
                    {
                        0 => new Vector3I(height + axisOffset, depth, width),
                        1 => new Vector3I(width, height + axisOffset, depth),
                        _ => new Vector3I(depth, width, height + axisOffset),
                    };

                    // Calculate chain end position. (Swizzle me timbers!)
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
                        ref uint nextPlaneSequence = ref planeSequences[(axis << 1) + axisOffset, height, nextWidth];

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

                    // Offset vertices A & C.
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
                            //surface.UVs.AddRange([new(1, 0), new(1, 1), new(0, 1), new(0, 0)]);
                            break;
                        case 1:
                            surface.Vertices.AddRange([vertexGridPositionA, vertexGridPositionB, vertexGridPositionC, vertexGridPositionD]);
                            //surface.UVs.AddRange([new(0, 0), new(0, 1), new(1, 1), new(1, 0)]);
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