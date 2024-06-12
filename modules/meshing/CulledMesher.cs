using Godot;
using RawUtils;
using System.Collections;
using System.Collections.Generic;

namespace RawVoxel
{    
    public static class CulledMesher
    {
        public static void Generate(ref Chunk chunk)
        {
            List<Vector3> Vertices = new();
            List<Vector3> Normals = new();
            List<Color> Colors = new();
            List<int> Indices = new();
            
            void GenerateFace(Voxel.Face face, Vector3I voxelGridPosition, Vector3I normal, Color color)
            {
                int[] faceVertices = Voxel.Faces[(int)face];
                
                Vector3I vertexA = Voxel.Vertices[faceVertices[0]] + voxelGridPosition;
                Vector3I vertexB = Voxel.Vertices[faceVertices[1]] + voxelGridPosition;
                Vector3I vertexC = Voxel.Vertices[faceVertices[2]] + voxelGridPosition;
                Vector3I vertexD = Voxel.Vertices[faceVertices[3]] + voxelGridPosition;

                int offset = Vertices.Count;

                Vertices.AddRange(new List<Vector3> { vertexA, vertexB, vertexC, vertexD });
                Normals.AddRange(new List<Vector3> { normal, normal, normal, normal });
                Colors.AddRange(new List<Color> { color, color, color, color });
                Indices.AddRange(new List<int> { 0 + offset, 1 + offset, 2 + offset, 0 + offset, 2 + offset, 3 + offset });
            }        
            
            void GenerateVoxel(ref Chunk chunk, int voxelGridIndex)
            {
                int chunkDiameter = chunk.World.ChunkDiameter;
                Vector3I voxelGridPosition = XYZConvert.IndexToVector3I(voxelGridIndex, new(chunkDiameter, chunkDiameter, chunkDiameter));
                
                int voxelType = chunk.VoxelTypes[voxelGridIndex];
                Color color = chunk.World.Voxels[voxelType].Color;
                    
                if (Voxel.IsVisible(ref chunk, voxelGridPosition + Vector3I.Up) == false)
                    GenerateFace(Voxel.Face.Top, voxelGridPosition, Vector3I.Up, color);
                
                if (Voxel.IsVisible(ref chunk, voxelGridPosition + Vector3I.Down) == false)
                    GenerateFace(Voxel.Face.Btm, voxelGridPosition, Vector3I.Down, color);
                
                if (Voxel.IsVisible(ref chunk, voxelGridPosition + Vector3I.Left) == false)
                    GenerateFace(Voxel.Face.West, voxelGridPosition, Vector3I.Left, color);
                
                if (Voxel.IsVisible(ref chunk, voxelGridPosition + Vector3I.Right) == false)
                    GenerateFace(Voxel.Face.East, voxelGridPosition, Vector3I.Right, color);
                
                if (Voxel.IsVisible(ref chunk, voxelGridPosition + Vector3I.Forward) == false)
                    GenerateFace(Voxel.Face.North, voxelGridPosition, Vector3I.Forward, color);
                
                if (Voxel.IsVisible(ref chunk, voxelGridPosition + Vector3I.Back) == false)
                    GenerateFace(Voxel.Face.South, voxelGridPosition, Vector3I.Back, color);
            }
            
            for (int voxelIndex = 0; voxelIndex < chunk.VoxelTypes.Length; voxelIndex ++)
            {
                if (chunk.VoxelTypes[voxelIndex] != 0)
                    GenerateVoxel(ref chunk, voxelIndex);
            }

            MeshHelper.Generate(ref chunk, ref Vertices, ref Normals, ref Colors, ref Indices);
        }
    }
}