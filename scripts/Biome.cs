using Godot;

namespace RAWVoxel
{
    [GlobalClass]
    public partial class Biome : Resource
    {
        [Export] public Voxel SurfaceType { get; set; }
        [Export] public int SurfaceHeight { get; set; }
        [Export] public Voxel Layer3Type { get; set; }
        [Export] public int Layer3Height { get; set; }
        [Export] public Voxel Layer2Type { get; set; }
        [Export] public int Layer2Height { get; set; }
        [Export] public Voxel Layer1Type { get; set; }
        [Export] public int Layer1Height { get; set; }
        [Export] public Voxel BedrockType { get; set; }
        [Export] public int BedrockHeight { get; set; }
    }
}
