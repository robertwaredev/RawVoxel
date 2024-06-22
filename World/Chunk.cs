using Godot;
using System;
using System.Collections;
using RawVoxel.Math.Conversions;

namespace RawVoxel.World;

[Tool]
public partial class Chunk : MeshInstance3D
{
    [Flags] public enum State : byte
    {
        Cullable = 1,
        Composed = 2,
        Complete = 4
    }

    public byte States = 0;
    public byte[] VoxelTypes;

    public override void _EnterTree()
    {
        AddToGroup("NavSource");
    }

    public void GenerateVoxels(Vector3I chunkTruePosition, int chunkBitshifts, int chunkVoxelCount, Biome biome, WorldSettings worldSettings)
    {
        BitArray voxelMasks = new(chunkVoxelCount);

        VoxelTypes = new byte[chunkVoxelCount];

        for (int voxelGridIndex = 0; voxelGridIndex < chunkVoxelCount; voxelGridIndex ++)
        {    
            Vector3I voxelTruePosition = chunkTruePosition + XYZBitShift.IndexToVector3I(voxelGridIndex, chunkBitshifts);

            if (Voxel.GenerateMask(voxelTruePosition, biome) == true)
            {
                byte voxelType = Voxel.GenerateType(voxelTruePosition, biome, worldSettings);
                
                if (voxelType != 0)
                {
                    voxelMasks[voxelGridIndex] = true;
                    VoxelTypes[voxelGridIndex] = voxelType;
                }
            }
        }

        if (voxelMasks.HasAnySet()) States |= (byte)State.Composed;
    }

    public static bool IsInFrustum(Vector3 chunkTruePosition, int chunkRadius, int chunkDiameter, Camera3D camera)
    {
        if (camera == null) return true;

        Vector3I chunkCenterPosition = (Vector3I)chunkTruePosition + new Vector3I(chunkRadius, chunkRadius, chunkRadius);
        Vector3I chunkFrustumPosition = chunkCenterPosition - (Vector3I)camera.Transform.Basis.Z.Sign() * new Vector3I(chunkDiameter, chunkDiameter, chunkDiameter) * 2;

        if (camera.IsPositionInFrustum(chunkFrustumPosition)) return true;

        return false;
    }
    public static Vector3I GetGridPosition(int gridIndex, Vector3I worldRadius, Vector3I worldDiameter)
    {
        return XYZConvert.IndexToVector3I(gridIndex, worldDiameter) - worldRadius;
    }
    public static Vector3I GetTruePosition(Vector3I chunkGridPosition, int chunkBitshifts)
    {
        return XYZBitShift.Vector3ILeft(chunkGridPosition, chunkBitshifts);
    }
}
