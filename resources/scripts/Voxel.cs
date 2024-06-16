using Godot;
using System;
using System.Linq;
using RawVoxel.Math.Conversions;

namespace RawVoxel;

[GlobalClass, Tool]
public partial class Voxel() : Resource
{
    #region Exports
    
    [Export] public Color Color;
    
    #endregion Exports

    #region Enums

    public enum Vertex { FrontTopLeft, FrontBtmLeft, FrontTopRight, FrontBtmRight, BackTopLeft, BackBtmLeft, BackTopRight, BackBtmRight }
    public enum Face { Top, Btm, West, East, North, South }
    public enum UV { TopLeft, BtmLeft, TopRight, BtmRight }
    
    #endregion Enums

    #region Constants
    
    public static readonly Vector3I[] Vertices =
    [
        new(0, 1, 1),
        new(0, 0, 1),
        new(1, 1, 1),
        new(1, 0, 1),
        new(0, 1, 0),
        new(0, 0, 0),
        new(1, 1, 0),
        new(1, 0, 0)
    ];
    public static readonly int[][] Faces =
    [
        [4, 6, 2, 0],
        [1, 3, 7, 5],
        [4, 0, 1, 5],
        [2, 6, 7, 3],
        [6, 4, 5, 7],
        [0, 2, 3, 1]
    ];

    #endregion Constants
    
    public static bool GenerateMask(Vector3I voxelTruePosition, ref Biome biome)
    {
        float densityNoise = biome.DensityNoise.GetNoise3Dv(voxelTruePosition);
        float voxelDensity = biome.DensityCurve.Sample((densityNoise + 1) * 0.5f);

        if (voxelDensity < 0.5f) return false;

        return true;
    }
    public static byte GenerateType(Vector3I voxelTruePosition, ref Biome biome, ref WorldSettings worldSettings)
    {
        float heightNoise = biome.HeightNoise.GetNoise2D(voxelTruePosition.X, voxelTruePosition.Z);
        
        foreach (BiomeLayer biomeLayer in biome.Layers.Reverse())
        {
            float voxelHeight = biomeLayer.HeightCurve.Sample((heightNoise + 1) * 0.5f);

            if (voxelTruePosition.Y <= voxelHeight)
            {
                return (byte)Array.IndexOf(worldSettings.Voxels, biomeLayer.Voxel);
            }
        }

        return 0;
    }
    public static bool IsExternal(Vector3I voxelGridPosition, ref WorldSettings worldSettings)
    {
        if (voxelGridPosition.X < 0 || voxelGridPosition.X >= worldSettings.ChunkDiameter) return true;
        if (voxelGridPosition.Y < 0 || voxelGridPosition.Y >= worldSettings.ChunkDiameter) return true;
        if (voxelGridPosition.Z < 0 || voxelGridPosition.Z >= worldSettings.ChunkDiameter) return true;
        
        return false;
    }
    public static bool IsVisible(Vector3I voxelGridPosition, Vector3I chunkTruePosition, ref byte[] voxelTypes, ref Biome biome, ref WorldSettings worldSettings)
    {
        if (IsExternal(voxelGridPosition, ref worldSettings))
        {
            if (worldSettings.ShowChunkEdges) return false;

            Vector3I voxelTruePosition = chunkTruePosition + voxelGridPosition;
            
            bool mask = GenerateMask(voxelTruePosition, ref biome);
            uint type = GenerateType(voxelTruePosition, ref biome, ref worldSettings);
            
            if (mask == true && type != 0) return true;
            
            else return false;
        }
        
        int chunkDiameter = worldSettings.ChunkDiameter;
        int voxelIndex = XYZConvert.Vector3IToIndex(voxelGridPosition, new(chunkDiameter, chunkDiameter, chunkDiameter));
        
        if (voxelTypes[voxelIndex] == 0)
        {
            return false;
        }

        return true;
    }
    public static void SetType(ref byte[] voxelTypes, Vector3I voxelPosition, byte voxelType, ref WorldSettings worldSettings)
    {
        voxelPosition.X = Mathf.PosMod(voxelPosition.X, worldSettings.ChunkDiameter);
        voxelPosition.Y = Mathf.PosMod(voxelPosition.Y, worldSettings.ChunkDiameter);
        voxelPosition.Z = Mathf.PosMod(voxelPosition.Z, worldSettings.ChunkDiameter);
        
        int chunkDiameter = worldSettings.ChunkDiameter;
        int voxelIndex = XYZConvert.Vector3IToIndex(voxelPosition, new(chunkDiameter, chunkDiameter, chunkDiameter));
        
        voxelTypes[voxelIndex] = voxelType;
    }
}
