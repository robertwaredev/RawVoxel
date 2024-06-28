using Godot;

namespace RawVoxel.Resources;

[GlobalClass, Tool]
public partial class WorldSettings() : Resource
{
    #region Exports

    [ExportGroup("Libraries")]
    [Export] public Voxel[] Voxels { get; set; }
    [Export] public Biome[] Biomes { get; set; }

    [ExportGroup("Attributes")]
    [Export] public WorldAttribute Temperature { get; set; }
    [Export] public WorldAttribute Humidity { get; set; }

    #endregion Exports
}
