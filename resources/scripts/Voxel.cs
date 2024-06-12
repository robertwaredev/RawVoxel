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
        
        public static bool GenerateMask(ref Chunk chunk, Vector3I globalPosition)
        {
            float densityNoise = chunk.Biome.DensityNoise.GetNoise3Dv(globalPosition);
            float voxelDensity = chunk.Biome.DensityCurve.Sample((densityNoise + 1) * 0.5f);

            if (voxelDensity < 0.5f) return false;

            return true;
        }
        public static uint GenerateType(ref Chunk chunk, Vector3I globalPosition)
        {
            float heightNoise = chunk.Biome.HeightNoise.GetNoise2D(globalPosition.X, globalPosition.Z);
            
            foreach (BiomeLayer biomeLayer in chunk.Biome.Layers.Reverse())
            {
                float voxelHeight = biomeLayer.HeightCurve.Sample((heightNoise + 1) * 0.5f);

                if (globalPosition.Y <= voxelHeight)
                {
                    return (uint)Array.IndexOf(chunk.World.Voxels, biomeLayer.Voxel);
                }
            }

            return 0;
        }
        public static bool IsExternal(ref Chunk chunk, Vector3I voxelPosition)
        {
            if (voxelPosition.X < 0 || voxelPosition.X >= chunk.World.ChunkDiameter) return true;
            if (voxelPosition.Y < 0 || voxelPosition.Y >= chunk.World.ChunkDiameter) return true;
            if (voxelPosition.Z < 0 || voxelPosition.Z >= chunk.World.ChunkDiameter) return true;
            
            return false;
        }
        public static bool IsVisible(ref Chunk chunk, Vector3I voxelPosition)
        {
            if (IsExternal(ref chunk, voxelPosition))
            {
                if (chunk.World.ShowChunkEdges) return false;

                Vector3I globalPosition = (Vector3I)chunk.Position + voxelPosition;
                
                bool mask = GenerateMask(ref chunk, globalPosition);
                uint type = GenerateType(ref chunk, globalPosition);
                
                if (mask == true && type != 0) return true;
                else return false;
            }
            
            int chunkDiameter = chunk.World.ChunkDiameter;
            int voxelIndex = XYZConvert.Vector3IToIndex(voxelPosition, new(chunkDiameter, chunkDiameter, chunkDiameter));
            
            if (chunk.VoxelTypes[voxelIndex] == 0)
            {
                return false;
            }

            return true;
        }
        public static void SetType(ref Chunk chunk, Vector3I voxelPosition, byte voxelType)
        {
            voxelPosition.X = Mathf.PosMod(voxelPosition.X, chunk.World.ChunkDiameter);
            voxelPosition.Y = Mathf.PosMod(voxelPosition.Y, chunk.World.ChunkDiameter);
            voxelPosition.Z = Mathf.PosMod(voxelPosition.Z, chunk.World.ChunkDiameter);
            
            int chunkDiameter = chunk.World.ChunkDiameter;
            int voxelIndex = XYZConvert.Vector3IToIndex(voxelPosition, new(chunkDiameter, chunkDiameter, chunkDiameter));
            
            chunk.VoxelTypes[voxelIndex] = voxelType;
        }
    }
}