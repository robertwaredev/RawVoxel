using Godot;

namespace RawVoxel.Meshing;
    
public static class MeshHelper
{
    public static ArrayMesh GenerateMesh(Surface[] surfaces, Material material)
    {
        ArrayMesh arrayMesh = new();
        
        for (int surfaceIndex = 0; surfaceIndex < 6; surfaceIndex ++)
        {
            Surface surface = surfaces[surfaceIndex];

            if (surface is null) continue;

            Godot.Collections.Array surfaceArray = [];
            surfaceArray.Resize((int)Mesh.ArrayType.Max);
            
            surfaceArray[(int)Mesh.ArrayType.Vertex] = surface.Vertices.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Index] = surface.Indices.ToArray();
        
            int surfaceArrayMeshIndex = arrayMesh.GetSurfaceCount();
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray);

            ShaderMaterial shaderMaterial = material.Duplicate() as ShaderMaterial;
            shaderMaterial.SetShaderParameter("surfaceNormalID", surfaceIndex);
            arrayMesh.SurfaceSetMaterial(surfaceArrayMeshIndex, shaderMaterial);
        }

        return arrayMesh;
    }
}