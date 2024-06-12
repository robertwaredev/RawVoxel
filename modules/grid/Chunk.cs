using Godot;
using RawUtils;

namespace RawVoxel
{
    [Tool]
    public partial class Chunk : MeshInstance3D
    {
        #region Variables
        
        public Biome Biome;
        public World World;
        public byte[] VoxelTypes;
        
        #endregion Variables
        
        public Chunk(ref World world)
        {
            World = world;
            MaterialOverride = world.TerrainMaterial;
        }

        public void Generate(Vector3I chunkPosition)
        {
            Chunk chunk = this;
            
            Position = chunkPosition * World.ChunkDiameter;
            Biome = Biome.Generate(ref World, chunkPosition);
            
            VoxelTypes = new byte[World.ChunkDiameter * World.ChunkDiameter * World.ChunkDiameter];

            for (int voxelIndex = 0; voxelIndex < VoxelTypes.Length; voxelIndex ++)
            {
                Vector3I chunkDiameter = new(World.ChunkDiameter, World.ChunkDiameter, World.ChunkDiameter);
                
                Vector3I voxelPosition = XYZConvert.IndexToVector3I(voxelIndex, chunkDiameter);
                Vector3I voxelGlobalPosition = (Vector3I)Position + voxelPosition;
                
                bool voxelMask = Voxel.GenerateMask(ref chunk, voxelGlobalPosition);
                uint voxelType = Voxel.GenerateType(ref chunk, voxelGlobalPosition);

                if (voxelMask == true && voxelType != 0)
                {
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
