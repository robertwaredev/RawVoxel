using Godot;

namespace RawVoxel;

[GlobalClass, Tool]
public partial class BiomeLayer() : Resource
{
    #region Exports

    [Export] public Voxel Voxel { get; set;}
    [Export] public Curve HeightDistribution { get; set; }

    #endregion Exports
}