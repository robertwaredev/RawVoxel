using Godot;

namespace RawVoxel.Meshing;
    
public static class MeshHelper
{
    public static ArrayMesh GenerateMesh(Surface[] surfaces, Material material)
    {
        ArrayMesh arrayMesh = new();
        
        for (int surface = 0; surface < surfaces.Length; surface ++)
        {
            Godot.Collections.Array surfaceArray = [];
            surfaceArray.Resize((int)Mesh.ArrayType.Max);
            
            if (surfaces[surface].Vertices.Count != 0)
            {
                surfaceArray[(int)Mesh.ArrayType.Vertex] = surfaces[surface].Vertices.ToArray();
            }
            if (surfaces[surface].Normals.Count != 0)
            {
                surfaceArray[(int)Mesh.ArrayType.Normal] = surfaces[surface].Normals.ToArray();
            }
            if (surfaces[surface].Colors.Count != 0)
            {
                surfaceArray[(int)Mesh.ArrayType.Color] = surfaces[surface].Colors.ToArray();
            }
            if (surfaces[surface].Indices.Count != 0)
            {
                surfaceArray[(int)Mesh.ArrayType.Index] = surfaces[surface].Indices.ToArray();
            }
        
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray);
            arrayMesh.SurfaceSetMaterial(surface, material);
        }

        return arrayMesh;
    }
}