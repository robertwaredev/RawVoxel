using Godot;
using RawVoxel.Math.Conversions;

namespace RawVoxel.Meshing;

public static class CulledMesher
{
    // Generate a naive culled mesh.
    public static Surface[] GenerateSurfaces(Vector3I chunkTruePosition, byte chunkDiameter, bool showChunkEdges, ref byte[] voxelTypes, ref Biome biome, ref WorldSettings worldSettings)
    {
        Surface[] surfaces = [new(), new(), new(), new(), new(), new()];
        
        for (int voxelIndex = 0; voxelIndex < voxelTypes.Length; voxelIndex ++)
        {
            if (voxelTypes[voxelIndex] != 0)
            {
                Vector3I voxelGridPosition = XYZConvert.IndexToVector3I(voxelIndex, new(chunkDiameter, chunkDiameter, chunkDiameter));
                int voxelType = voxelTypes[voxelIndex];
                Color voxelColor = worldSettings.Voxels[voxelType].Color;
                
                GenerateVoxel(voxelGridPosition, voxelColor, chunkTruePosition, chunkDiameter, showChunkEdges, ref voxelTypes, ref surfaces, ref biome, ref worldSettings);
            }
        }

        return surfaces;
    }

    public static void GenerateVoxel(Vector3I voxelGridPosition, Color color, Vector3I chunkTruePosition, byte chunkDiameter, bool showChunkEdges, ref byte[] voxelTypes, ref Surface[] surfaces, ref Biome biome, ref WorldSettings worldSettings)
    {    
        if (Voxel.IsVisible(voxelGridPosition + Vector3I.Left, chunkTruePosition, chunkDiameter, showChunkEdges, ref voxelTypes, biome, worldSettings) == false)
            GenerateFace(voxelGridPosition, Voxel.Face.West, Vector3I.Left, color, ref surfaces[0]);
        
        if (Voxel.IsVisible(voxelGridPosition + Vector3I.Right, chunkTruePosition, chunkDiameter, showChunkEdges, ref voxelTypes, biome, worldSettings) == false)
            GenerateFace(voxelGridPosition, Voxel.Face.East, Vector3I.Right, color, ref surfaces[1]);
        
        if (Voxel.IsVisible(voxelGridPosition + Vector3I.Up, chunkTruePosition, chunkDiameter, showChunkEdges, ref voxelTypes, biome, worldSettings) == false)
            GenerateFace(voxelGridPosition, Voxel.Face.Top, Vector3I.Up, color, ref surfaces[2]);
        
        if (Voxel.IsVisible(voxelGridPosition + Vector3I.Down, chunkTruePosition, chunkDiameter, showChunkEdges, ref voxelTypes, biome, worldSettings) == false)
            GenerateFace(voxelGridPosition, Voxel.Face.Btm, Vector3I.Down, color, ref surfaces[3]);
        
        if (Voxel.IsVisible(voxelGridPosition + Vector3I.Forward, chunkTruePosition, chunkDiameter, showChunkEdges, ref voxelTypes, biome, worldSettings) == false)
            GenerateFace(voxelGridPosition, Voxel.Face.North, Vector3I.Forward, color, ref surfaces[4]);
        
        if (Voxel.IsVisible(voxelGridPosition + Vector3I.Back, chunkTruePosition, chunkDiameter, showChunkEdges, ref voxelTypes, biome, worldSettings) == false)
            GenerateFace(voxelGridPosition, Voxel.Face.South, Vector3I.Back, color, ref surfaces[5]);
    }

    public static void GenerateFace(Vector3I voxelGridPosition, Voxel.Face face, Vector3I normal, Color color, ref Surface surface)
    {
        int[] faceVertices = Voxel.Faces[(int)face];
        
        Vector3I vertexA = Voxel.Vertices[faceVertices[0]] + voxelGridPosition;
        Vector3I vertexB = Voxel.Vertices[faceVertices[1]] + voxelGridPosition;
        Vector3I vertexC = Voxel.Vertices[faceVertices[2]] + voxelGridPosition;
        Vector3I vertexD = Voxel.Vertices[faceVertices[3]] + voxelGridPosition;

        int offset = surface.Vertices.Count;

        surface.Vertices.AddRange([vertexA, vertexB, vertexC, vertexD]);
        surface.Normals.AddRange([normal, normal, normal, normal]);
        surface.Colors.AddRange([color, color, color, color]);
        surface.Indices.AddRange([0 + offset, 1 + offset, 2 + offset, 0 + offset, 2 + offset, 3 + offset]);
    }        
}
