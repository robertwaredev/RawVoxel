using Godot;
using RawVoxel.Math;
using RawVoxel.Resources;
using System.Collections;

namespace RawVoxel.World;

[Tool]
public partial class Chunk : MeshInstance3D
{
    public enum StateType : byte { Abstract, Tangible, Observed, Rendered, Tethered }

    public StateType State;
    public BitArray VoxelMasks;
    public ImageTexture[] VoxelTypes;

    public override void _Ready()
    {
        //Owner = GetParent(); // Enable to load chunks into the scene tree in editor. DONT leave enabled.
        AddToGroup("NavSource");
    }

    public void GenerateVoxels(Vector3I chunkSTruePosition, int chunkBitshifts, Biome biome, WorldSettings worldSettings)
    {
        int chunkDiameter = 1 << chunkBitshifts;

        VoxelMasks = new(chunkDiameter << chunkBitshifts << chunkBitshifts);
        VoxelTypes = new ImageTexture[chunkDiameter];

        for (int z = 0; z < chunkDiameter; z ++)
        {
            Image zImage = Image.Create(chunkDiameter, chunkDiameter, false, Image.Format.R8);

            for (int y = 0; y < chunkDiameter; y ++)
            {
                for (int x = 0; x < chunkDiameter; x ++)
                {
                    Vector3I voxelUGridPosition = new(x, y, z);
                    Vector3I voxelSTruePosition = chunkSTruePosition + voxelUGridPosition;

                    if (Voxel.GenerateMask(voxelSTruePosition, biome) == false) continue;
                    
                    byte voxelType = Voxel.GenerateType(voxelSTruePosition, biome, worldSettings);
                    
                    if (voxelType == 0) continue;
                    
                    VoxelMasks[XYZ.Encode(voxelUGridPosition, chunkBitshifts)] = true;
                    
                    zImage.SetPixel(x, y, new(){ R8 = voxelType });
                }
            }
        
            VoxelTypes[z] = ImageTexture.CreateFromImage(zImage);
        }
    }
}
