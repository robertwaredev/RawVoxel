using Godot;
using System;
using RawUtils;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

// TODO - Thread Pool
// TODO - Fix chunk loading to always load chunks closest to focus node at surface level first.

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class World : Resource
    {
        #region Enums
        
        public enum Attribute { Temperature, Humidity }

        #endregion Enums
        
        #region Exports

        [ExportGroup("Libraries")]
        [Export] public Voxel[] Voxels { get; set; }
        [Export] public Biome[] Biomes { get; set; }

        [ExportGroup("Attributes")]
        [Export] public WorldAttribute Temperature { get; set; } = new();
        [Export] public WorldAttribute Humidity { get; set; } = new();

        [ExportGroup("Dimensions")]
        [Export] public bool CenterChunk = true;
        [Export] public Vector3I Draw = new(1, 1, 1);
        [Export] public Vector3I Radius = new(128, 128, 128);
                 public Vector3I Diameter
                 {
                    get { return Radius * 2 + Vector3I.One; }
                 }
        [Export] public Vector3I ChunkDiameter = new(16, 16, 16);

        [ExportGroup("Material")]
        [Export] public Material TerrainMaterial { get; set; } = GD.Load<Material>("res://addons/RawVoxel/resources/materials/ChunkShaderMaterial.tres");

        [ExportGroup("Rendering")]
        [Export] public bool ShowChunkEdges = false;
        
        [ExportGroup("Threading")]
        [Export] public int GenerateFrequency = 15;

        #endregion Exports
    
        public World() {}
    }
}
