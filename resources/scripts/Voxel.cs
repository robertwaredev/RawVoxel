using Godot;
using System;
using RawUtils;
using System.Linq;

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
        
        public static bool GenerateMask(Vector3I globalPosition, ref Biome biome)
        {
            float densityNoise = biome.DensityNoise.GetNoise3Dv(globalPosition);
            float voxelDensity = biome.DensityCurve.Sample((densityNoise + 1) * 0.5f);

            if (voxelDensity < 0.5f) return false;

            return true;
        }
        public static byte GenerateType(Vector3I globalPosition, ref Biome biome, ref WorldSettings worldSettings)
        {
            float heightNoise = biome.HeightNoise.GetNoise2D(globalPosition.X, globalPosition.Z);
            
            foreach (BiomeLayer biomeLayer in biome.Layers.Reverse())
            {
                float voxelHeight = biomeLayer.HeightCurve.Sample((heightNoise + 1) * 0.5f);

                if (globalPosition.Y <= voxelHeight)
                {
                    return (byte)Array.IndexOf(worldSettings.Voxels, biomeLayer.Voxel);
                }
            }

            return 0;
        }
        public static bool IsExternal(Vector3I voxelPosition, ref WorldSettings worldSettings)
        {
            if (voxelPosition.X < 0 || voxelPosition.X >= worldSettings.ChunkDiameter) return true;
            if (voxelPosition.Y < 0 || voxelPosition.Y >= worldSettings.ChunkDiameter) return true;
            if (voxelPosition.Z < 0 || voxelPosition.Z >= worldSettings.ChunkDiameter) return true;
            
            return false;
        }
        public static bool IsVisible(ref Chunk chunk, Vector3I voxelPosition, ref Biome biome, ref WorldSettings worldSettings)
        {
            if (IsExternal(voxelPosition, ref worldSettings))
            {
                if (worldSettings.ShowChunkEdges) return false;

                Vector3I globalPosition = (Vector3I)chunk.Position + voxelPosition;
                
                bool mask = GenerateMask(globalPosition, ref biome);
                uint type = GenerateType(globalPosition, ref biome, ref worldSettings);
                
                if (mask == true && type != 0) return true;
                
                else return false;
            }
            
            int chunkDiameter = worldSettings.ChunkDiameter;
            int voxelIndex = XYZConvert.Vector3IToIndex(voxelPosition, new(chunkDiameter, chunkDiameter, chunkDiameter));
            
            if (chunk.VoxelTypes[voxelIndex] == 0)
            {
                return false;
            }

            return true;
        }
        public static void SetType(ref Chunk chunk, Vector3I voxelPosition, byte voxelType, ref WorldSettings worldSettings)
        {
            voxelPosition.X = Mathf.PosMod(voxelPosition.X, worldSettings.ChunkDiameter);
            voxelPosition.Y = Mathf.PosMod(voxelPosition.Y, worldSettings.ChunkDiameter);
            voxelPosition.Z = Mathf.PosMod(voxelPosition.Z, worldSettings.ChunkDiameter);
            
            int chunkDiameter = worldSettings.ChunkDiameter;
            int voxelIndex = XYZConvert.Vector3IToIndex(voxelPosition, new(chunkDiameter, chunkDiameter, chunkDiameter));
            
            chunk.VoxelTypes[voxelIndex] = voxelType;
        }
    }
}