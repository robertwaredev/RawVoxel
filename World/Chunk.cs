using Godot;
using RawVoxel.Math.Conversions;

namespace RawVoxel.World;

[Tool]
public partial class Chunk() : MeshInstance3D
{
    public static byte[] GenerateVoxels(Vector3I chunkTruePosition, byte chunkDiameter, Biome biome, WorldSettings worldSettings)
    {
        int shifts = XYZBitShift.CalculateShifts(chunkDiameter);

        byte[] voxels = new byte[1 << shifts << shifts << shifts];

        for (int voxelIndex = 0; voxelIndex < voxels.Length; voxelIndex ++)
        {    
            Vector3I voxelGridPosition = XYZBitShift.IndexToVector3I(voxelIndex, shifts);
            Vector3I voxelTruePosition = chunkTruePosition + voxelGridPosition;
            
            bool voxelMask = Voxel.GenerateMask(voxelTruePosition, biome);

            if (voxelMask == true)
            {
                byte voxelType = Voxel.GenerateType(voxelTruePosition, biome, worldSettings);
                
                if (voxelType != 0)
                {
                    voxels[voxelIndex] = voxelType;
                }
            }
        }

        return voxels;
    }
}
