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
    
    public static bool GenerateMask(Vector3I voxelTruePosition, Biome biome)
    {
        float densityNoise = biome.DensityNoise.GetNoise3Dv(voxelTruePosition);
        float voxelDensity = biome.DensityCurve.Sample((densityNoise + 1) * 0.5f);

        if (voxelDensity < 0.5f) return false;

        return true;
    }
    public static byte GenerateType(Vector3I voxelTruePosition, Biome biome, WorldSettings worldSettings)
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
    public static bool IsExternal(Vector3I voxelGridPosition, byte chunkDiameter)
    {
        if (voxelGridPosition.X < 0 || voxelGridPosition.X >= chunkDiameter) return true;
        if (voxelGridPosition.Y < 0 || voxelGridPosition.Y >= chunkDiameter) return true;
        if (voxelGridPosition.Z < 0 || voxelGridPosition.Z >= chunkDiameter) return true;
        
        return false;
    }
    public static bool IsVisible(Vector3I voxelGridPosition, Vector3I chunkTruePosition, byte chunkDiameter, bool showChunkEdges, ref byte[] voxelTypes, Biome biome, WorldSettings worldSettings)
    {
        if (IsExternal(voxelGridPosition, chunkDiameter))
        {
            if (showChunkEdges) return false;

            Vector3I voxelTruePosition = chunkTruePosition + voxelGridPosition;
            
            bool mask = GenerateMask(voxelTruePosition, biome);
            uint type = GenerateType(voxelTruePosition, biome, worldSettings);
            
            if (mask == true && type != 0) return true;
            
            else return false;
        }
        
        int voxelIndex = XYZConvert.Vector3IToIndex(voxelGridPosition, new(chunkDiameter, chunkDiameter, chunkDiameter));
        
        if (voxelTypes[voxelIndex] == 0)
        {
            return false;
        }

        return true;
    }
    public static void SetType(ref byte[] voxelTypes, Vector3I voxelPosition, byte voxelType, byte chunkDiameter)
    {
        voxelPosition.X = Mathf.PosMod(voxelPosition.X, chunkDiameter);
        voxelPosition.Y = Mathf.PosMod(voxelPosition.Y, chunkDiameter);
        voxelPosition.Z = Mathf.PosMod(voxelPosition.Z, chunkDiameter);
        
        int voxelIndex = XYZConvert.Vector3IToIndex(voxelPosition, new(chunkDiameter, chunkDiameter, chunkDiameter));
        
        voxelTypes[voxelIndex] = voxelType;
    }
}
