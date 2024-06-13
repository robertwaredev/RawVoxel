using Godot;
using RawUtils;

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class WorldSettings() : Resource
    {
        public enum MeshGenerationType { Greedy, Standard }
        
        #region Exports

        [ExportGroup("Libraries")]
        [Export] public Voxel[] Voxels { get; set; }
        [Export] public Biome[] Biomes { get; set; }

        [ExportGroup("Attributes")]
        [Export] public WorldAttribute Temperature { get; set; } = new();
        [Export] public WorldAttribute Humidity { get; set; } = new();

        [ExportGroup("Dimensions")]
        [Export] public bool CenterChunk = true;
        [Export] public Vector3I DrawRadius = new(1, 1, 1);
        public Vector3I DrawDiameter
        {
            get {
                    Vector3I diameter = XYZBitShift.Vector3ILeft(DrawRadius, 1);
                    if (CenterChunk) diameter += Vector3I.One;
                    return diameter;
                }
        }
        [Export] public Vector3I Radius = new(128, 128, 128);
        public Vector3I Diameter
        {
            get {
                    Vector3I diameter = XYZBitShift.Vector3ILeft(Radius, 1);
                    if (CenterChunk) diameter += Vector3I.One;
                    return diameter;
                }
        }
        [Export] public int ChunkDiameter = 32;

        [ExportGroup("Material")]
        [Export] public Material TerrainMaterial { get; set; } = new StandardMaterial3D();

        [ExportGroup("Rendering")]
        [Export] public bool ShowChunkEdges = false;
        [Export] public MeshGenerationType MeshGeneration { get; set; } = MeshGenerationType.Greedy;
        
        [ExportGroup("Threading")]
        [Export] public int GenerateFrequency = 30;

        #endregion Exports
    }
}
