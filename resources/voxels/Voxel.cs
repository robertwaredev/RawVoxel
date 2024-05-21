using Godot;
using System;
using RawUtils;
using System.Linq;
using System.Collections.Generic;

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class Voxel : Resource
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
        
        public static readonly Vector3I[] Vertices = new Vector3I[]
        {
            new(0, 1, 1),
            new(0, 0, 1),
            new(1, 1, 1),
            new(1, 0, 1),
            new(0, 1, 0),
            new(0, 0, 0),
            new(1, 1, 0),
            new(1, 0, 0)
        };
        public static readonly int[][] Faces = new int[][]
        {
            new int[]{4, 6, 2, 0},
            new int[]{1, 3, 7, 5},
            new int[]{4, 0, 1, 5},
            new int[]{2, 6, 7, 3},
            new int[]{6, 4, 5, 7},
            new int[]{0, 2, 3, 1}
        };

        #endregion Constants

        public Voxel() {}
        
        public static bool GenerateMask(VoxelContainer voxelContainer, Vector3I globalPosition)
        {
            float densityNoise = voxelContainer.Biome.DensityNoise.GetNoise3Dv(globalPosition);
            float voxelDensity = voxelContainer.Biome.DensityCurve.Sample((densityNoise + 1) * 0.5f);

            if (voxelDensity < 0.5f) return false;

            return true;
        }
        public static uint GenerateType(VoxelContainer voxelContainer, Vector3I globalPosition)
        {
            float heightNoise = voxelContainer.Biome.HeightNoise.GetNoise2D(globalPosition.X, globalPosition.Z);
            
            foreach (BiomeLayer biomeLayer in voxelContainer.Biome.Layers.Reverse())
            {
                float voxelHeight = biomeLayer.HeightCurve.Sample((heightNoise + 1) * 0.5f);

                if (globalPosition.Y <= voxelHeight)
                {
                    return (uint)Array.IndexOf(voxelContainer.World.Voxels, biomeLayer.Voxel);
                }
            }

            return 0;
        }
        public static bool IsExternal(VoxelContainer voxelContainer, Vector3I gridPosition)
        {
            if (gridPosition.X < 0 || gridPosition.X >= voxelContainer.World.ChunkDiameter) return true;
            if (gridPosition.Y < 0 || gridPosition.Y >= voxelContainer.World.ChunkDiameter) return true;
            if (gridPosition.Z < 0 || gridPosition.Z >= voxelContainer.World.ChunkDiameter) return true;
            
            return false;
        }
        public static bool IsVisible(VoxelContainer voxelContainer, Vector3I gridPosition)
        {
            if (IsExternal(voxelContainer, gridPosition))
            {
                if (voxelContainer.World.ShowChunkEdges) return false;

                Vector3I globalPosition = (Vector3I)voxelContainer.Position + gridPosition;
                
                bool mask = GenerateMask(voxelContainer, globalPosition);
                uint type = GenerateType(voxelContainer, globalPosition);
                
                if (mask == true && type != 0) return true;
                else return false;
            }
            
            int chunkDiameter = voxelContainer.World.ChunkDiameter;
            int gridIndex = XYZConvert.Vector3IToIndex(gridPosition, new(chunkDiameter, chunkDiameter, chunkDiameter));
            
            return voxelContainer.VoxelMasks[gridIndex];
        }
        public static void SetType(VoxelContainer voxelContainer, Vector3I gridPosition, byte voxelType)
        {
            gridPosition.X = Mathf.PosMod(gridPosition.X, voxelContainer.World.ChunkDiameter);
            gridPosition.Y = Mathf.PosMod(gridPosition.Y, voxelContainer.World.ChunkDiameter);
            gridPosition.Z = Mathf.PosMod(gridPosition.Z, voxelContainer.World.ChunkDiameter);
            
            int chunkDiameter = voxelContainer.World.ChunkDiameter;
            int gridIndex = XYZConvert.Vector3IToIndex(gridPosition, new(chunkDiameter, chunkDiameter, chunkDiameter));
            
            voxelContainer.VoxelTypes[gridIndex] = voxelType;
        }
    }
}