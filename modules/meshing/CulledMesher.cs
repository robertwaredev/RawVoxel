using Godot;
using RawUtils;
using System.Collections.Generic;

namespace RawVoxel
{
    public static class CulledMesher
    {
        // FIXME - Having these in this scope might be a problem
        public static readonly List<Vector3> Vertices = new();
        public static readonly List<Vector3> Normals = new();
        public static readonly List<Color> Colors = new();
        public static readonly List<int> Indices = new();
        
        public static void Generate(VoxelContainer voxelContainer)
        {
            for (int voxelGridIndex = 0; voxelGridIndex < voxelContainer.VoxelMasks.Length; voxelGridIndex ++)
            {
                if (voxelContainer.VoxelMasks[voxelGridIndex] == true)
                    GenerateVoxel(voxelContainer, voxelGridIndex);
            }

            if (Vertices.Count == 0) return;
            if (Normals.Count == 0) return;
            if (Colors.Count == 0) return;
            if (Indices.Count == 0) return;
            
            Godot.Collections.Array surfaceArray = new();
            surfaceArray.Resize((int)Mesh.ArrayType.Max);

            surfaceArray[(int)Mesh.ArrayType.Vertex] = Vertices.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Normal] = Normals.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Color]  = Colors.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Index]  = Indices.ToArray();
            
            Vertices.Clear();
            Normals.Clear();
            Colors.Clear();
            Indices.Clear();

            ArrayMesh arrayMesh = new();
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray);
            surfaceArray.Clear();

            voxelContainer.Mesh = arrayMesh;

            StaticBody3D collision = voxelContainer.GetChildOrNull<StaticBody3D>(0);
            collision?.QueueFree();

            voxelContainer.CreateTrimeshCollision();
            voxelContainer.AddToGroup("NavSource");
        }
        private static void GenerateVoxel(VoxelContainer voxelContainer, int voxelGridIndex)
        {
            int chunkDiameter = voxelContainer.World.ChunkDiameter;
            Vector3I voxelGridPosition = XYZConvert.IndexToVector3I(voxelGridIndex, new(chunkDiameter, chunkDiameter, chunkDiameter));
            
            int voxelType = voxelContainer.VoxelTypes[voxelGridIndex];
            Color color = voxelContainer.World.Voxels[voxelType].Color;
                
            if (Voxel.IsVisible(voxelContainer, voxelGridPosition + Vector3I.Up) == false)
                GenerateFace(Voxel.Face.Top, voxelGridPosition, Vector3I.Up, color);
            
            if (Voxel.IsVisible(voxelContainer, voxelGridPosition + Vector3I.Down) == false)
                GenerateFace(Voxel.Face.Btm, voxelGridPosition, Vector3I.Down, color);
            
            if (Voxel.IsVisible(voxelContainer, voxelGridPosition + Vector3I.Left) == false)
                GenerateFace(Voxel.Face.West, voxelGridPosition, Vector3I.Left, color);
            
            if (Voxel.IsVisible(voxelContainer, voxelGridPosition + Vector3I.Right) == false)
                GenerateFace(Voxel.Face.East, voxelGridPosition, Vector3I.Right, color);
            
            if (Voxel.IsVisible(voxelContainer, voxelGridPosition + Vector3I.Forward) == false)
                GenerateFace(Voxel.Face.North, voxelGridPosition, Vector3I.Forward, color);
            
            if (Voxel.IsVisible(voxelContainer, voxelGridPosition + Vector3I.Back) == false)
                GenerateFace(Voxel.Face.South, voxelGridPosition, Vector3I.Back, color);
        }
        private static void GenerateFace(Voxel.Face face, Vector3I voxelGridPosition, Vector3I normal, Color color)
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
    }
}