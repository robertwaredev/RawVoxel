using Godot;

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class BiomeLayer : Resource
    {
        public BiomeLayer() {}

        // The primary type for this layer.
        [Export] public Voxel VoxelType
        {
            get { return _voxelType; }
            set { _voxelType = value; ResourceName = VoxelType.Name;}
        }
        private Voxel _voxelType = new();

        // The initial height for this layer.
        [Export] public int Height
        {
            get { return _height; }
            set { _height = value; }
        }
        private int _height = 128;

        // Modifies biome height noise value.
        // X represents input height and Y represents output height.
        [Export] public Curve HeightCurve
        {
            get { return _heightCurve; }
            set { _heightCurve = value; }
        }
        private Curve _heightCurve = new();
    }
}
