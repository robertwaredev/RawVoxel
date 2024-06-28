using Godot;
using RawVoxel.Resources;
using System.Collections;

namespace RawVoxel.Meshing;

public static class CulledMesher
{
    public static Binary.Surface[] GenerateSurfaces(ref BitArray voxels, int chunkBitshifts, Vector3I signBasisZ, bool cullGeometry = true)
    {
        int chunkDiameter = 1 << chunkBitshifts;

        Binary.Surface[] surfaces = [new(), new(), new(), new(), new(), new()];

        for (int axis = 0; axis < 3; axis ++)
        {
            int visibleAxisSign = signBasisZ[axis];

            // Combined axis signs.
            if (visibleAxisSign == 0 || cullGeometry == false)
            {
                for (int x = 0; x < chunkDiameter; x ++)
                {
                    for (int y = 0; y < chunkDiameter; y ++)
                    {
                        for (int z = 0; z < chunkDiameter; z ++)
                        {
                            Vector3I voxelGridPosition = new(x, y, z);

                            int surfaceIDNegative = (axis << 1) + 0;
                            int surfaceIDPositive = (axis << 1) + 1;

                            if (Voxel.IsVisible(voxelGridPosition + Voxel.Normals[surfaceIDNegative], ref voxels, chunkDiameter) == false)
                                GenerateFace(voxelGridPosition, surfaceIDNegative, surfaces[surfaceIDNegative]);

                            if (Voxel.IsVisible(voxelGridPosition + Voxel.Normals[surfaceIDPositive], ref voxels, chunkDiameter) == false)
                                GenerateFace(voxelGridPosition, surfaceIDPositive, surfaces[surfaceIDPositive]);
                        }
                    }
                }
            }

            // Negative axis signs only.
            else if (visibleAxisSign < 0)
            {
                for (int x = 0; x < chunkDiameter; x ++)
                {
                    for (int y = 0; y < chunkDiameter; y ++)
                    {
                        for (int z = 0; z < chunkDiameter; z ++)
                        {
                            Vector3I voxelGridPosition = new(x, y, z);

                            int surfaceIDNegative = (axis << 1) + 0;

                            if (Voxel.IsVisible(voxelGridPosition + Voxel.Normals[surfaceIDNegative], ref voxels, chunkDiameter) == false)
                                GenerateFace(voxelGridPosition, surfaceIDNegative, surfaces[surfaceIDNegative]);
                        }
                    }
                }
            }
            
            // Postive axis signs only.
            else if (visibleAxisSign > 0)
            {
                for (int x = 0; x < chunkDiameter; x ++)
                {
                    for (int y = 0; y < chunkDiameter; y ++)
                    {
                        for (int z = 0; z < chunkDiameter; z ++)
                        {
                            Vector3I voxelGridPosition = new(x, y, z);

                            int surfaceIDPositive = (axis << 1) + 1;

                            if (Voxel.IsVisible(voxelGridPosition + Voxel.Normals[surfaceIDPositive], ref voxels, chunkDiameter) == false)
                                GenerateFace(voxelGridPosition, surfaceIDPositive, surfaces[surfaceIDPositive]);
                        }
                    }
                }
            }
        }
    
        return surfaces;
    }

    public static void GenerateFace(Vector3I voxelGridPosition, int surfaceID, Binary.Surface surface)
    {
        int[] faceVertices = Voxel.Faces[surfaceID];
        
        Vector3I vertexA = Voxel.Vertices[faceVertices[0]] + voxelGridPosition;
        Vector3I vertexB = Voxel.Vertices[faceVertices[1]] + voxelGridPosition;
        Vector3I vertexC = Voxel.Vertices[faceVertices[2]] + voxelGridPosition;
        Vector3I vertexD = Voxel.Vertices[faceVertices[3]] + voxelGridPosition;

        int offset = surface.Vertices.Count;

        surface.Vertices.AddRange([vertexA, vertexB, vertexC, vertexD]);
        surface.Indices.AddRange([0 + offset, 1 + offset, 2 + offset, 0 + offset, 2 + offset, 3 + offset]);
    }
}
