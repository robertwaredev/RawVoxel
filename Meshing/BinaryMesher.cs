using Godot;
using RawVoxel.Math.Binary;
using RawVoxel.Math.Conversions;
using System.Collections.Generic;
using RawVoxel.World;
using static System.Numerics.BitOperations;

namespace RawVoxel.Meshing;

// TODO - Skip the generation section for homogenous chunks.

// X AXIS SWIZZLES     || Y AXIS SWIZZLES     || Z AXIS SWIZZLES
// =================================================================
// Z = relative width  || X = relative width  || Y = relative width
// X = relative height || Y = relative height || Z = relative height
// Y = relative depth  || Z = relative depth  || X = relative depth

public static class BinaryMesher
{ 
    // Generate a binary greedy mesh. Takes an array of voxel types, chunk diameter, and the integer snapped signBasisZ of the player camera.
    public static Surface[] GenerateSurfaces(ref byte[] voxels, byte diameter, Vector3I signBasisZ, bool cullGeometry = false)
    {
        #region Variables

        // Store chunk diameter as diameter = 1 << shifts.
        int shifts = XYZBitShift.CalculateShifts(diameter);
        
        // Sequences of voxel visibility bit masks, stored as [axis, relative depth, relative width] with relative height encoded into each sequence's bits.
        uint[,,] voxelSequences = new uint[3, diameter, diameter];
        
        // Sequences of plane visibility bit masks, stored as [axis, relative height, relative width] with relative depth encoded into each sequence's bits.
        uint[,,] planeSequencesNegative = new uint[3, diameter, diameter]; // Negative sign axes.
        uint[,,] planeSequencesPositive = new uint[3, diameter, diameter]; // Positive sign axes.
        
        // Surface array for storing ArrayMesh data, one Surface per axis.
        Surface[] surfaces = [new(), new(), new()];

        #endregion Variables
        
        // Loop through axes and encode visible voxel positions into bit mask sequences.
        for (int x = 0; x < diameter; x ++)
        {
            for (int y = 0; y < diameter; y ++)
            {
                for (int z = 0; z < diameter; z ++)
                {
                    // Skip if current voxel is not solid.
                    if (voxels[XYZBitShift.XYZToIndex(x, y, z, shifts)] == 0) continue;

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

            // Loop through voxel sequences.
            for (int depth = 0; depth < diameter; depth ++)
            {
                for (int width = 0; width < diameter; width ++)
                {
                    // Retrieve current voxel seqeuence.
                    uint voxelSequence = voxelSequences[axis, depth, width];

                    // Skip if no visible voxels.
                    if (voxelSequence == 0) continue;

                    // Negative sign axes. Only one set is generated at a time unless neither are visible, in which case both are generated. (latter prevents thread sleep from causing visual issues)
                    if (visibleAxisSign <= 0 || cullGeometry == false)
                    {
                        // Extract visible planes from voxel sequence on the negative axis.
                        uint planeSequenceNegative = voxelSequence & ~(voxelSequence << 1);
                        
                        // Loop through set bits in visible plane sequence and swizzle them into new sets.
                        while (planeSequenceNegative != 0)
                        {
                            int height = TrailingZeroCount(planeSequenceNegative);
                            planeSequencesNegative[axis, height, width] |= (uint)1 << depth;
                            planeSequenceNegative &= (uint)~(1 << height);
                        }
                    }

                    // Positive sign axes. Only one set is generated at a time unless neither are visible, in which case both are generated. (latter prevents thread sleep from causing visual issues)
                    if (visibleAxisSign >= 0 || cullGeometry == false)
                    {
                        // Extract visible planes from voxel sequence on the positive axis.
                        uint planeSequencePositive = voxelSequence & ~(voxelSequence >> 1);
                        
                        // Loop through set bits in visible plane sequence and swizzle them into new sets.
                        while (planeSequencePositive != 0)
                        {
                            int height = TrailingZeroCount(planeSequencePositive);
                            planeSequencesPositive[axis, height, width] |= (uint)1 << depth;
                            planeSequencePositive &= (uint)~(1 << height);
                        }
                    }
                }
            }
        }
            
        // Loop through axes and generate mesh data.
        for (int axis = 0; axis < 3; axis ++)
        {
            // Determine which axis sign is visible.
            int visibleAxisSign = signBasisZ[axis];

            // Retrieve surface corresponding to the current axis.
            Surface surface = surfaces[axis];
            
            // Switch normal based on axis.
            Vector3I normal = axis switch
            {
                0 => new Vector3I(1, 0, 0),
                1 => new Vector3I(0, 1, 0),
                _ => new Vector3I(0, 0, 1),
            };
            
            // Negative sign axes. Only one set is generated at a time unless neither are visible, in which case both are generated. (latter prevents thread sleep from causing visual issues)
            if (visibleAxisSign <= 0 || cullGeometry == false)
            {
                GeneratePlanes(ref planeSequencesNegative, surface, -normal, axis, diameter);
            }
            
            // Positive sign axes. Only one set is generated at a time unless neither are visible, in which case both are generated. (latter prevents thread sleep from causing visual issues)
            if (visibleAxisSign >= 0 || cullGeometry == false)
            {
                GeneratePlanes(ref planeSequencesPositive, surface, normal, axis, diameter);
            }
        }
        
        return surfaces;
    }

    // Loop through plane sequences and generate mesh data.
    private static void GeneratePlanes(ref uint[,,] planeSequences, Surface surface, Vector3I normal, int axis, byte diameter)
    {
        // If normal direction is positive, planes need to be offset by one so they don't overlap with opposite facing planes.
        int axisOffset = normal[axis] switch
        {
            >= 0 => 1,  // Positive normals are offset by 1
            <= 0 => 0   // Negative normals
        };
        
        // Loop through plane sequences and generate mesh data.
        for (int height = 0; height < diameter; height ++)
        {
            for (int width = 0; width < diameter; width ++)
            {
                // Retrieve current plane sequence.
                uint planeSequence = planeSequences[axis, height, width];

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
                    for (int nextWidth = width + 1; nextWidth < diameter; nextWidth ++)
                    {
                        // Retrieve the next plane sequence.
                        ref uint nextPlaneSequence = ref planeSequences[axis, height, nextWidth];

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
                    Vector3I vertexA = chainStart; 
                    Vector3I vertexB = chainStart;
                    Vector3I vertexC = chainEnd; 
                    Vector3I vertexD = chainEnd;

                    // Offset vertices A & C.
                    switch (axis)
                    {
                        case 0:
                            vertexA.Y += chain.Length;
                            vertexC.Y -= chain.Length;
                            break;
                        case 1:
                            vertexA.Z += chain.Length;
                            vertexC.Z -= chain.Length;
                            break;
                        default:
                            vertexA.X += chain.Length;
                            vertexC.X -= chain.Length;
                            break;
                    }
                    
                    // Get get vertex count to offset indices.
                    int vertexCount = surface.Vertices.Count;
                    
                    // Switch vertex draw order.
                    switch (normal[axis])
                    {
                        case <= 0:
                            surface.Vertices.AddRange([vertexD, vertexC, vertexB, vertexA]);
                            break;
                        case >= 0:
                            surface.Vertices.AddRange([vertexA, vertexB, vertexC, vertexD]);
                            break;
                    }

                    // Add normals to their list.
                    surface.Normals.AddRange([normal, normal, normal, normal]);

                    // Add indices to their list.
                    surface.Indices.AddRange([0 + vertexCount, 1 + vertexCount, 2 + vertexCount, 0 + vertexCount, 2 + vertexCount, 3 + vertexCount]);
                }
            }
        }
    }
}