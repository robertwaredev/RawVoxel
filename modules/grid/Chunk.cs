using Godot;
using RawUtils;
using System.Collections;
using System.Collections.Specialized;

namespace RawVoxel
{
    [Tool]
    public partial class Chunk : MeshInstance3D
    {
        #region Variables
        
        public Biome Biome;
        public World World;
        public BitArray VoxelMasks;
        public byte[] VoxelTypes;
        
        #endregion Variables
        
        public Chunk(ref World world)
        {
            World = world;
            MaterialOverride = world.TerrainMaterial.Duplicate() as Material;
        }

        public void Generate(Vector3I chunkPosition)
        {
            Chunk chunk = this;
            
            Position = chunkPosition * World.ChunkDiameter;
            Biome = Biome.Generate(ref World, chunkPosition);
            
            int voxelCount = World.ChunkDiameter * World.ChunkDiameter * World.ChunkDiameter;
            
            VoxelMasks = new BitArray(voxelCount);
            VoxelTypes = new byte[voxelCount];

            for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex ++)
            {
                Vector3I chunkDiameter = new(World.ChunkDiameter, World.ChunkDiameter, World.ChunkDiameter);
                
                Vector3I voxelPosition = XYZConvert.IndexToVector3I(voxelIndex, chunkDiameter);
                Vector3I voxelGlobalPosition = (Vector3I)Position + voxelPosition;
                
                bool voxelMask = Voxel.GenerateMask(ref chunk, voxelGlobalPosition);
                uint voxelType = Voxel.GenerateType(ref chunk, voxelGlobalPosition);

                if (voxelMask == true && voxelType != 0)
                {
                    VoxelMasks[voxelIndex] = true;
                    VoxelTypes[voxelIndex] = (byte)voxelType;
                }
            }
            
            Update();
        }
        public void Update()
        {
            Chunk chunk = this;
            
            switch (World.MeshGeneration)
            {
                case World.MeshGenerationType.Standard:
                    CulledMesher.Generate(ref chunk);
                    break;
                case World.MeshGenerationType.Greedy:
                    BinaryMesher.Generate(ref chunk);
                    break;
            }
        }
    }
}
