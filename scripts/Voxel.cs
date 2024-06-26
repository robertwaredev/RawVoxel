using Godot;
using System;
using System.Drawing;
using System.Collections.Generic;

namespace RAWVoxel
{
    [GlobalClass, Tool]
    public partial class Voxel : Resource
    {
        #region Constructor

        // Godot will lose its shit if there's no constructor for resources that are marked as [Tool].
        public Voxel() {}

        #endregion Constructor
        
        #region Exports
        
        [Export] Type VoxelType;
        [Export] Godot.Color VoxelColor;
        
        #endregion Exports

        #region Variables

        public enum Type { Air, Bedrock, Stone, Dirt, Grass, Sand }
        public enum Vertex { FrontTopLeft, FrontBtmLeft, FrontTopRight, FrontBtmRight, BackTopLeft, BackBtmLeft, BackTopRight, BackBtmRight }
        public enum Face { Top, Btm, West, East, North, South }
        public enum UV { TopLeft, BtmLeft, TopRight, BtmRight }
        
        public static readonly Dictionary<Type, KnownColor> Colors = new()
        {
            {Voxel.Type.Air, KnownColor.Transparent},
            {Voxel.Type.Bedrock, KnownColor.Black},
            {Voxel.Type.Stone, KnownColor.DimGray},
            {Voxel.Type.Dirt, KnownColor.SaddleBrown},
            {Voxel.Type.Grass, KnownColor.DarkGreen},
            {Voxel.Type.Sand, KnownColor.BurlyWood},
        };
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
        public static readonly Dictionary<UV, Vector2I> UVs = new()
        {
            {UV.TopLeft,    new(0,  0)},
            {UV.BtmLeft,    new(0, -1)},
            {UV.BtmRight,   new(1, -1)},
            {UV.TopRight,   new(1,  0)}
        };
    
        #endregion Variables
    }
}