using Godot;
using RawVoxel.World;
using Godot.Collections;

namespace RawVoxel.Meshing;
    
public static class MeshHelper
{
    public static ArrayMesh GenerateMesh(Binary.Surface[] surfaces, Chunk chunk, Material material)
    {
        ArrayMesh arrayMesh = new();
        
        for (int surfaceIndex = 0; surfaceIndex < 6; surfaceIndex ++)
        {
            Binary.Surface surface = surfaces[surfaceIndex];

            if (surface is null) continue;
            if (surface.Indices.Count == 0) continue;

            Array surfaceArray = [];
            surfaceArray.Resize((int)Mesh.ArrayType.Max);
            
            surfaceArray[(int)Mesh.ArrayType.Index] = surface.Indices.ToArray();
            
            if (surface.Vertices.Count != 0)
                surfaceArray[(int)Mesh.ArrayType.Vertex] = surface.Vertices.ToArray();

            if (surface.Colors.Count != 0) // Only used by culled mesher.
                surfaceArray[(int)Mesh.ArrayType.Color] = surface.Colors.ToArray();
        
            int surfaceArrayMeshIndex = arrayMesh.GetSurfaceCount();
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray);

            arrayMesh.SurfaceSetMaterial(surfaceArrayMeshIndex, material.Duplicate() as Material);

            if (material is not ShaderMaterial) continue;
            
            ShaderMaterial shaderMaterial = arrayMesh.SurfaceGetMaterial(surfaceArrayMeshIndex) as ShaderMaterial;
            shaderMaterial.SetShaderParameter("surfaceID", surfaceIndex);
            shaderMaterial.SetShaderParameter("voxelIDMap", chunk.VoxelTypes);
        }

        return arrayMesh;
    }
}