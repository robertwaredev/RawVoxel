using Godot;
using RawUtils;
using System.Collections;

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class Chunk : VoxelContainer
    {
        public Chunk(World world)
        {
            World = world;
            MaterialOverride = world.TerrainMaterial.Duplicate() as Material;
        }

        public override void GenerateVoxels(Vector3I chunkGridPosition)
        {
            Position = chunkGridPosition * World.ChunkDiameter;
            Biome = Biome.Generate(World, chunkGridPosition);
            
            int voxelCount = World.ChunkDiameter * World.ChunkDiameter * World.ChunkDiameter;
            
            VoxelMasks = new BitArray(voxelCount);
            VoxelTypes = new byte[voxelCount];

            for (int voxelGridIndex = 0; voxelGridIndex < voxelCount; voxelGridIndex ++)
            {
                Vector3I chunkDiameter = new(World.ChunkDiameter, World.ChunkDiameter, World.ChunkDiameter);
                Vector3I voxelGridPosition = XYZConvert.IndexToVector3I(voxelGridIndex, chunkDiameter);
                Vector3I voxelGlobalPosition = (Vector3I)Position + voxelGridPosition;
                
                bool mask = Voxel.GenerateMask(this, voxelGlobalPosition);
                uint type = Voxel.GenerateType(this, voxelGlobalPosition);

                if (mask == true && type != 0)
                {
                    VoxelMasks.Set(voxelGridIndex, true);
                    VoxelTypes[voxelGridIndex] = (byte)type;
                }
            }

            //SetupShader();

            BinaryMesher.Generate(this);
            CulledMesher.Generate(this);
        }
        public void SetupShader()
        {
            NoiseTexture3D densityTexture = new()
            {
                Noise = Biome.DensityNoise,
                Width = World.ChunkDiameter,
                Height = World.ChunkDiameter,
                Depth = World.ChunkDiameter,
            };

            FastNoiseLite fastNoise = densityTexture.Noise as FastNoiseLite;
            fastNoise.Offset = Position;
            
            ShaderMaterial material = MaterialOverride as ShaderMaterial;            
            material.SetShaderParameter("density", densityTexture);
        }
    }
}
