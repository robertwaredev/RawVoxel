using Godot;
using System.Collections;

namespace RawVoxel
{
    public abstract partial class VoxelContainer : MeshInstance3D
    {
        public World World;
        public Biome Biome;
        public BitArray VoxelMasks;
        public byte[] VoxelTypes;
        
        public abstract void GenerateVoxels(Vector3I chunkGridPosition);
    }
}