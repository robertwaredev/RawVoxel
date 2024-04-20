using Godot;
using System;

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class Biome : Resource
    {
        #region Constructor

        public Biome() {}
        
        #endregion Constructor

        
        #region Exports -> Layers
        [ExportCategory("Layers")]
        
        [Export] public BiomeLayer[] Layers
        {
            get { return _layers; }
            set { _layers = value; }
        }
        private BiomeLayer[] _layers = Array.Empty<BiomeLayer>();

        #endregion Exports -> Layers
        
        #region Exports -> Temperature
        [ExportCategory("Temperature")]
        
        [Export(PropertyHint.Range, "-200, 200")] public float TemperatureMin
        {
            get { return _temperatureMin; }
            set { _temperatureMin = value; }
        }
        private float _temperatureMin = 60;
        
        [Export(PropertyHint.Range, "-200, 200")] public float TemperatureMax
        {
            get { return _temperatureMax; }
            set { _temperatureMax = value; }
        }
        private float _temperatureMax = 70;

        #endregion Exports -> Temperature
    
        #region Exports -> Humidity
        [ExportCategory("Humidity")]
        
        [Export(PropertyHint.Range, "0, 100")] public float HumidityMin
        {
            get { return _humidityMin; }
            set { _humidityMin = value; }
        }
        private float _humidityMin = 0;
        
        [Export(PropertyHint.Range, "0, 100")] public float HumidityMax
        {
            get { return _humidityMax; }
            set { _humidityMax = value; }
        }
        private float _humidityMax = 100;

        #endregion Exports -> Temperature

        #region Exports -> Density
        [ExportCategory("Density")]

        // Controls density sampling across all layers. This sampled in 3D.
        [Export] public FastNoiseLite DensityNoise
        {
            get { return _densityNoise; }
            set { _densityNoise = value; }
        }
        private FastNoiseLite _densityNoise = new();

        // Modifies DensityNoise across all layers.
        // X represents density chance and Y represents output height.
        [Export] public Curve DensityCurve
        {
            get { return _densityCurve; }
            set { _densityCurve = value; }
        }
        private Curve _densityCurve = new();

        #endregion Exports -> Density
    }
}
