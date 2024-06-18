using System.Collections;
using Godot;
using RawVoxel.Math.Conversions;

namespace RawVoxel.World;

[Tool]
public partial class Chunk() : MeshInstance3D
{
    public enum ChunkContents { Empty, Solid, Varied}

    public ChunkContents Contents = ChunkContents.Empty;
    public BitArray VoxelMasks;
    public byte[] VoxelTypes;

    public void GenerateVoxels(Vector3I chunkTruePosition, byte chunkDiameter, Biome biome, WorldSettings worldSettings)
    {
        int shifts = XYZBitShift.CalculateShifts(chunkDiameter);

        VoxelMasks = new(1 << shifts << shifts << shifts);

        VoxelTypes = new byte[1 << shifts << shifts << shifts];

        for (int voxelIndex = 0; voxelIndex < VoxelTypes.Length; voxelIndex ++)
        {    
            Vector3I voxelTruePosition = chunkTruePosition + XYZBitShift.IndexToVector3I(voxelIndex, shifts);

            if (Voxel.GenerateMask(voxelTruePosition, biome) == true)
            {
                byte voxelType = Voxel.GenerateType(voxelTruePosition, biome, worldSettings);
                
                if (voxelType != 0)
                {
                    VoxelMasks[voxelIndex] = true;
                    VoxelTypes[voxelIndex] = voxelType;
                }
            }
        }

        if (VoxelMasks.HasAnySet()) Contents = ChunkContents.Varied;
        if (VoxelMasks.HasAllSet()) Contents = ChunkContents.Solid;
    }
}
