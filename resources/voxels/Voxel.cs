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

        #region Dictionaries
        
        public static readonly Dictionary<Vertex, Vector3I> Vertices = new()
        {
            {Vertex.FrontTopLeft,    new(0, 1, 1)},
            {Vertex.FrontBtmLeft,    new(0, 0, 1)},
            {Vertex.FrontTopRight,   new(1, 1, 1)},
            {Vertex.FrontBtmRight,   new(1, 0, 1)},
            {Vertex.BackTopLeft,     new(0, 1, 0)},
            {Vertex.BackBtmLeft,     new(0, 0, 0)},
            {Vertex.BackTopRight,    new(1, 1, 0)},
            {Vertex.BackBtmRight,    new(1, 0, 0)}
        };
        public static readonly Dictionary<Face, Vertex[]> Faces = new()
        {
            {Face.Top,      new Vertex[] {Vertex.BackTopLeft,      Vertex.BackTopRight,     Vertex.FrontTopRight,   Vertex.FrontTopLeft}},
            {Face.Btm,      new Vertex[] {Vertex.FrontBtmLeft,     Vertex.FrontBtmRight,    Vertex.BackBtmRight,    Vertex.BackBtmLeft}},
            {Face.West,     new Vertex[] {Vertex.BackTopLeft,      Vertex.FrontTopLeft,     Vertex.FrontBtmLeft,    Vertex.BackBtmLeft}},
            {Face.East,     new Vertex[] {Vertex.FrontTopRight,    Vertex.BackTopRight,     Vertex.BackBtmRight,    Vertex.FrontBtmRight}},
            {Face.North,    new Vertex[] {Vertex.BackTopRight,     Vertex.BackTopLeft,      Vertex.BackBtmLeft,     Vertex.BackBtmRight}},
            {Face.South,    new Vertex[] {Vertex.FrontTopLeft,     Vertex.FrontTopRight,     Vertex.FrontBtmRight,   Vertex.FrontBtmLeft}}
        };
        public static readonly Dictionary<Face, Vector3I> Normals = new()
        {
            {Face.Top,      Vector3I.Up},
            {Face.Btm,      Vector3I.Down},
            {Face.West,     Vector3I.Left},
            {Face.East,     Vector3I.Right},
            {Face.North,    Vector3I.Forward},
            {Face.South,    Vector3I.Back}
        };
        public static readonly Dictionary<UV, Vector2I> UVs = new()
        {
            {UV.TopLeft,    new(0,  0)},
            {UV.BtmLeft,    new(0, -1)},
            {UV.BtmRight,   new(1, -1)},
            {UV.TopRight,   new(1,  0)}
        };
    
        #endregion Dictionaries

        public Voxel() {}
        
        public static bool GenerateVisibility(Biome biome, Vector3I voxelGlobalPosition)
        {
            float densityNoise = biome.DensityNoise.GetNoise3Dv(voxelGlobalPosition);
            float voxelDensity = biome.DensityCurve.Sample((densityNoise + 1) * 0.5f);

            if (voxelDensity < 0.5f) return false;

            return true;
        }
        public static int GenerateID(World world, Biome biome, Vector3I voxelGlobalPosition)
        {
            float heightNoise = biome.HeightNoise.GetNoise2D(voxelGlobalPosition.X, voxelGlobalPosition.Z);
            
            foreach (BiomeLayer biomeLayer in biome.Layers.Reverse())
            {
                float voxelHeight = biomeLayer.HeightCurve.Sample((heightNoise + 1) * 0.5f);

                if (voxelGlobalPosition.Y <= voxelHeight)
                {
                    return Array.IndexOf(world.Voxels, biomeLayer.Voxel);
                }
            }

            // Return air as a last resort.
            return 0;
        }
        public static bool IsExternal(Chunk chunk, Vector3I gridPosition)
        {
            if (gridPosition.X < 0 || gridPosition.X >= chunk.World.ChunkDiameter.X) return true;
            if (gridPosition.Y < 0 || gridPosition.Y >= chunk.World.ChunkDiameter.Y) return true;
            if (gridPosition.Z < 0 || gridPosition.Z >= chunk.World.ChunkDiameter.Z) return true;
            
            return false;
        }
        public static bool IsVisible(Chunk chunk, Vector3I gridPosition)
        {
            if (IsExternal(chunk, gridPosition))
            {
                if (chunk.World.ShowChunkEdges) return false;

                return GenerateVisibility(chunk.Biome, gridPosition);
            }
            
            int gridIndex = XYZConvert.Vector3IToIndex(gridPosition, chunk.World.ChunkDiameter);
            
            return chunk.VoxelBits[gridIndex];
        }
        public static void SetID(Chunk chunk, Vector3I voxelGridPosition, int voxelID)
        {
            voxelGridPosition.X = Mathf.PosMod(voxelGridPosition.X, chunk.World.ChunkDiameter.X);
            voxelGridPosition.Y = Mathf.PosMod(voxelGridPosition.Y, chunk.World.ChunkDiameter.Y);
            voxelGridPosition.Z = Mathf.PosMod(voxelGridPosition.Z, chunk.World.ChunkDiameter.Z);
            
            chunk.VoxelIDs[XYZConvert.Vector3IToIndex(voxelGridPosition, chunk.World.ChunkDiameter)] = (byte)voxelID;
        }
    }
}