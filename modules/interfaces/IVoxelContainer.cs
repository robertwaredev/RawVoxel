using System.Collections;
using Godot;

namespace RawVoxel
{
    interface IVoxelContainer
    {
        World World { get; set;}
        Biome Biome { get; set; }
        BitArray VoxelBits { get; set; }
        byte[] VoxelIDs { get; set; }

        void GenerateVoxels(Vector3I chunkGridPosition);
        void GenerateMesh()
        {
            GenerateMeshData();
            GenerateMeshSurface();
            GenerateMeshCollision();
        }
        void GenerateMeshData();
        void GenerateMeshSurface();
        void GenerateMeshCollision();
    }
}