using Godot;
using System;
using RawVoxel.Math;
using System.Collections;

namespace RawVoxel.Resources;

[GlobalClass, Tool]
public partial class Voxel() : Resource
{
    #region Exports
    
    [Export] public Color Color;
    
    #endregion Exports

    #region Enums

    public enum Vertex { FrontTopLeft, FrontBtmLeft, FrontTopRight, FrontBtmRight, BackTopLeft, BackBtmLeft, BackTopRight, BackBtmRight }
    public enum Face { West, East, Btm, Top, North, South }
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
        [4, 0, 1, 5],   // West
        [2, 6, 7, 3],   // East
        [1, 3, 7, 5],   // Btm
        [4, 6, 2, 0],   // Top
        [6, 4, 5, 7],   // North
        [0, 2, 3, 1]    // South
    ];
    public static readonly Vector3I[] Normals =
    [
        Vector3I.Left,
        Vector3I.Right,
        Vector3I.Down,
        Vector3I.Up,
        Vector3I.Forward,
        Vector3I.Back,
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
        
        int layerCount = biome.Layers.Length;

        for (int layerIndex = layerCount - 1; layerIndex >= 0; layerIndex --)
        {
            BiomeLayer biomeLayer = biome.Layers[layerIndex];
            
            float voxelHeight = biomeLayer.HeightDistribution.Sample((heightNoise + 1) * 0.5f);

            if (voxelTruePosition.Y <= voxelHeight)
            {
                return (byte)Array.IndexOf(worldSettings.Voxels, biomeLayer.Voxel);
            }
        }

        return 0;
    }
    
    public static bool IsExternal(Vector3I voxelGridPosition, int chunkDiameter)
    {
        if (voxelGridPosition.X < 0 || voxelGridPosition.X >= chunkDiameter) return true;
        if (voxelGridPosition.Y < 0 || voxelGridPosition.Y >= chunkDiameter) return true;
        if (voxelGridPosition.Z < 0 || voxelGridPosition.Z >= chunkDiameter) return true;
        
        return false;
    }
    public static bool IsVisible(Vector3I voxelGridPosition, ref BitArray voxels, int chunkDiameter)
    {
        if (IsExternal(voxelGridPosition, chunkDiameter)) return false;
        
        int voxelIndex = XYZ.Encode(voxelGridPosition, new Vector3I(chunkDiameter, chunkDiameter, chunkDiameter));

        return voxels[voxelIndex];
    }
    
    public static void SetType(ref byte[] voxelTypes, Vector3I voxelPosition, byte voxelType, byte chunkDiameter)
    {
        voxelPosition.X = Mathf.PosMod(voxelPosition.X, chunkDiameter);
        voxelPosition.Y = Mathf.PosMod(voxelPosition.Y, chunkDiameter);
        voxelPosition.Z = Mathf.PosMod(voxelPosition.Z, chunkDiameter);
        
        int voxelIndex = XYZ.Encode(voxelPosition, new Vector3I(chunkDiameter, chunkDiameter, chunkDiameter));
        
        voxelTypes[voxelIndex] = voxelType;
    }
}
