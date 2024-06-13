using Godot;
using RawUtils;
using System.Collections;
using System.Collections.Generic;

namespace RawVoxel
{    
    public static class CulledMesher
    {
        public static void Generate(ref Chunk chunk, ref Biome biome, ref WorldSettings worldSettings)
        {
            List<Vector3> Vertices = [];
            List<Vector3> Normals = [];
            List<Color> Colors = [];
            List<int> Indices = [];
            
            void GenerateFace(Voxel.Face face, Vector3I voxelGridPosition, Vector3I normal, Color color)
            {
                int[] faceVertices = Voxel.Faces[(int)face];
                
                Vector3I vertexA = Voxel.Vertices[faceVertices[0]] + voxelGridPosition;
                Vector3I vertexB = Voxel.Vertices[faceVertices[1]] + voxelGridPosition;
                Vector3I vertexC = Voxel.Vertices[faceVertices[2]] + voxelGridPosition;
                Vector3I vertexD = Voxel.Vertices[faceVertices[3]] + voxelGridPosition;

                int offset = Vertices.Count;

                Vertices.AddRange([vertexA, vertexB, vertexC, vertexD]);
                Normals.AddRange([normal, normal, normal, normal]);
                Colors.AddRange([color, color, color, color]);
                Indices.AddRange([0 + offset, 1 + offset, 2 + offset, 0 + offset, 2 + offset, 3 + offset]);
            }        
            
            void GenerateVoxel(ref Chunk chunk, int voxelGridIndex, ref WorldSettings worldSettings, ref Biome biome)
            {
                int chunkDiameter = worldSettings.ChunkDiameter;
                Vector3I voxelGridPosition = XYZConvert.IndexToVector3I(voxelGridIndex, new(chunkDiameter, chunkDiameter, chunkDiameter));
                
                int voxelType = chunk.VoxelTypes[voxelGridIndex];
                Color color = worldSettings.Voxels[voxelType].Color;
                    
                if (Voxel.IsVisible(ref chunk, voxelGridPosition + Vector3I.Left, ref biome, ref worldSettings) == false)
                    GenerateFace(Voxel.Face.West, voxelGridPosition, Vector3I.Left, color);
                
                if (Voxel.IsVisible(ref chunk, voxelGridPosition + Vector3I.Right, ref biome, ref worldSettings) == false)
                    GenerateFace(Voxel.Face.East, voxelGridPosition, Vector3I.Right, color);
                
                if (Voxel.IsVisible(ref chunk, voxelGridPosition + Vector3I.Up, ref biome, ref worldSettings) == false)
                    GenerateFace(Voxel.Face.Top, voxelGridPosition, Vector3I.Up, color);
                
                if (Voxel.IsVisible(ref chunk, voxelGridPosition + Vector3I.Down, ref biome, ref worldSettings) == false)
                    GenerateFace(Voxel.Face.Btm, voxelGridPosition, Vector3I.Down, color);
                
                if (Voxel.IsVisible(ref chunk, voxelGridPosition + Vector3I.Forward, ref biome, ref worldSettings) == false)
                    GenerateFace(Voxel.Face.North, voxelGridPosition, Vector3I.Forward, color);
                
                if (Voxel.IsVisible(ref chunk, voxelGridPosition + Vector3I.Back, ref biome, ref worldSettings) == false)
                    GenerateFace(Voxel.Face.South, voxelGridPosition, Vector3I.Back, color);
            }
            
            for (int voxelIndex = 0; voxelIndex < chunk.VoxelTypes.Length; voxelIndex ++)
            {
                if (chunk.VoxelTypes[voxelIndex] != 0)
                    GenerateVoxel(ref chunk, voxelIndex, ref worldSettings, ref biome);
            }

            MeshHelper.Generate(ref chunk, ref Vertices, ref Normals, ref Colors, ref Indices);
        }
    }
}