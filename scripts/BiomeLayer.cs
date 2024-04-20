using Godot;
using RawUtils;
using System;


namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class BiomeLayer : Resource
    {
        #region Constructor

        public BiomeLayer() {}

        #endregion Constructor

        #region Exports -> Layer

        // The primary type for this layer.
        [Export] public Voxel VoxelType
        {
            get { return _voxelType; }
            set { _voxelType = value; }
        }
        private Voxel _voxelType = new();

        [Export] public int Height
        {
            get { return _height; }
            set { _height = value; }
        }
        private int _height = 128;
        
        #endregion Exports -> Layer
        
        #region Exports -> Noise

        // Controls height sampling for this layer. This sampled in 2D.
        [Export] public FastNoiseLite HeightNoise
        {
            get { return _heightNoise; }
            set { _heightNoise = value; }
        }
        private FastNoiseLite _heightNoise = new();
        
        #endregion Exports -> Noise
        
        #region Exports -> Curves

        // Modifies HeightNoise value.
        // X represents input height and Y represents output height.
        [Export] public Curve HeightCurve
        {
            get { return _heightCurve; }
            set { _heightCurve = value; }
        }
        private Curve _heightCurve = new();
        
        #endregion Exports -> Curves

        #region Exports -> Layers
        [ExportGroup("Layers")]
        
        // Fill layers for this layer.
        // These should be calibrated to occur less frequently than the primary type.
        [Export] public BiomeLayer[] Layers
        {
            get { return _layers; }
            set { _layers = value; }
        }
        private BiomeLayer[] _layers = Array.Empty<BiomeLayer>();
        
        #endregion Exports -> Layers
    }
}
