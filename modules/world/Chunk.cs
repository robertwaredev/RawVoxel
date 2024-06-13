using Godot;
using RawUtils;

namespace RawVoxel
{
    [Tool]
    public partial class Chunk : MeshInstance3D
    {
        public byte[] VoxelTypes;
        
        public Chunk() {}

        // Generate voxel types.
        public static byte[] GenerateVoxels(Vector3I chunkPosition, WorldSettings worldSettings)
        {
            Biome biome = Biome.Generate(ref worldSettings, chunkPosition);
            
            byte[] VoxelTypes = new byte[worldSettings.ChunkDiameter * worldSettings.ChunkDiameter * worldSettings.ChunkDiameter];

            for (int voxelIndex = 0; voxelIndex < VoxelTypes.Length; voxelIndex ++)
            {
                Vector3I chunkDiameter = new(worldSettings.ChunkDiameter, worldSettings.ChunkDiameter, worldSettings.ChunkDiameter);
                
                Vector3I voxelPosition = XYZConvert.IndexToVector3I(voxelIndex, chunkDiameter);
                Vector3I voxelGlobalPosition = worldSettings.ChunkDiameter * chunkPosition + voxelPosition;
                
                bool voxelMask = Voxel.GenerateMask(voxelGlobalPosition, ref biome);
                byte voxelType = Voxel.GenerateType(voxelGlobalPosition, ref biome, ref worldSettings);

                if (voxelMask == true && voxelType != 0)
                {
                    VoxelTypes[voxelIndex] = voxelType;
                }
            }
            
            return VoxelTypes;
        }

        // Generate voxel types.
        public void Generate(Vector3I chunkPosition, WorldSettings worldSettings)
        {
            Position = chunkPosition * worldSettings.ChunkDiameter;
            
            Biome biome = Biome.Generate(ref worldSettings, chunkPosition);
            
            VoxelTypes = new byte[worldSettings.ChunkDiameter * worldSettings.ChunkDiameter * worldSettings.ChunkDiameter];

            for (int voxelIndex = 0; voxelIndex < VoxelTypes.Length; voxelIndex ++)
            {
                Vector3I chunkDiameter = new(worldSettings.ChunkDiameter, worldSettings.ChunkDiameter, worldSettings.ChunkDiameter);
                
                Vector3I voxelPosition = XYZConvert.IndexToVector3I(voxelIndex, chunkDiameter);
                Vector3I voxelGlobalPosition = (Vector3I)Position + voxelPosition;
                
                bool voxelMask = Voxel.GenerateMask(voxelGlobalPosition, ref biome);
                byte voxelType = Voxel.GenerateType(voxelGlobalPosition, ref biome, ref worldSettings);

                if (voxelMask == true && voxelType != 0)
                {
                    VoxelTypes[voxelIndex] = voxelType;
                }
            }
            
            Chunk chunk = this;
            
            switch (worldSettings.MeshGeneration)
            {
                case WorldSettings.MeshGenerationType.Greedy:
                    BinaryMesher.Generate(ref chunk, ref worldSettings);
                    break;
                case WorldSettings.MeshGenerationType.Standard:
                    CulledMesher.Generate(ref chunk, ref biome, ref worldSettings);
                    break;
            }
        }
    }
}
