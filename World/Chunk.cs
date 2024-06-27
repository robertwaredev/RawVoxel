using Godot;
using RawVoxel.Math;
using System.Collections;

namespace RawVoxel.World;

[Tool]
public partial class Chunk : MeshInstance3D
{
    public enum StateType : byte { Abstract, Tangible, Observed, Rendered, Tethered }

    public StateType State;
    public ImageTexture[] VoxelTypes;
    public BitArray VoxelMasks;

    public override void _Ready()
    {
        //Owner = GetParent(); // Enable to load chunks into the scene tree in editor.
        AddToGroup("NavSource");
    }

    public void GenerateVoxels(Vector3I chunkTruePosition, int chunkDiameter, int chunkBitshifts, int chunkVoxelCount, Biome biome, WorldSettings worldSettings)
    {
        VoxelMasks = new BitArray(chunkVoxelCount);
        VoxelTypes = new ImageTexture[chunkDiameter];

        for (int z = 0; z < chunkDiameter; z ++)
        {
            Image zImage = Image.Create(chunkDiameter, chunkDiameter, false, Image.Format.R8);

            for (int y = 0; y < chunkDiameter; y ++)
            {
                for (int x = 0; x < chunkDiameter; x ++)
                {
                    Vector3I voxelGridPosition = new(x, y, z);
                    Vector3I voxelTruePosition = chunkTruePosition + voxelGridPosition;

                    // Generate voxel visibility/solidity mask.
                    if (Voxel.GenerateMask(voxelTruePosition, biome) == true)
                    {
                        // Generate voxel type.
                        byte voxelType = Voxel.GenerateType(voxelTruePosition, biome, worldSettings);
                        
                        if (voxelType == 0) continue;
                        
                        VoxelMasks[XYZ.Encode(voxelGridPosition, chunkBitshifts)] = true;
                        
                        zImage.SetPixel(x, y, new(){ R8 = voxelType });
                    }
                }
            }
        
            VoxelTypes[z] = ImageTexture.CreateFromImage(zImage);
        }
    }
}
