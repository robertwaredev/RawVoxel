using Godot;
using RawVoxel;
using System.Collections.Generic;

namespace RawUtils
{
    public static class MeshHelper
    {
        public static void Generate(ref Chunk chunk, ref List<Vector3> vertices, ref List<int> indices)
        {
            if (vertices.Count == 0) return;
            if (indices.Count  == 0) return;
            
            Godot.Collections.Array surfaceArray = [];
            surfaceArray.Resize((int)Mesh.ArrayType.Max);
            
            surfaceArray[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
            vertices.Clear();

            surfaceArray[(int)Mesh.ArrayType.Index] = indices.ToArray();
            indices.Clear();
            
            GenerateMesh(ref chunk, ref surfaceArray);
        }
        public static void Generate(ref Chunk chunk, ref List<Vector3> vertices, ref List<Vector3> normals, ref List<int> indices)
        {
            if (vertices.Count == 0) return;
            if (normals.Count == 0) return;
            if (indices.Count  == 0) return;
            
            Godot.Collections.Array surfaceArray = [];
            surfaceArray.Resize((int)Mesh.ArrayType.Max);
            
            surfaceArray[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
            
            surfaceArray[(int)Mesh.ArrayType.Normal] = normals.ToArray();

            surfaceArray[(int)Mesh.ArrayType.Index] = indices.ToArray();
            
            GenerateMesh(ref chunk, ref surfaceArray);
        }
        public static void Generate(ref Chunk chunk, ref List<Vector3> vertices, ref List<Vector3> normals, ref List<Color> colors, ref List<int> indices)
        {
            if (vertices.Count == 0) return;
            if (normals.Count == 0) return;
            if (colors.Count == 0) return;
            if (indices.Count == 0) return;
            
            Godot.Collections.Array surfaceArray = [];
            surfaceArray.Resize((int)Mesh.ArrayType.Max);
            
            surfaceArray[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
            vertices.Clear();
            
            surfaceArray[(int)Mesh.ArrayType.Normal] = normals.ToArray();
            normals.Clear();
            
            surfaceArray[(int)Mesh.ArrayType.Color] = colors.ToArray();
            colors.Clear();
        
            surfaceArray[(int)Mesh.ArrayType.Index] = indices.ToArray();
            indices.Clear();
            
            GenerateMesh(ref chunk, ref surfaceArray);
        }        
        private static void GenerateMesh(ref Chunk chunk, ref Godot.Collections.Array surfaceArray)
        {
            ArrayMesh arrayMesh = new();
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray);
            surfaceArray.Clear();

            chunk.Mesh = arrayMesh;

            StaticBody3D collision = chunk.GetChildOrNull<StaticBody3D>(0);
            collision?.QueueFree();

            chunk.CreateTrimeshCollision();
            chunk.AddToGroup("NavSource");
        }
    }
}