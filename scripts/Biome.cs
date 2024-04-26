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
        [ExportGroup("Temperature")]
        
        [Export(PropertyHint.Range, "-200, 200")] public int TemperatureMin
        {
            get { return _temperatureMin; }
            set { _temperatureMin = value; }
        }
        private int _temperatureMin = 60;
        
        [Export(PropertyHint.Range, "-200, 200")] public int TemperatureMax
        {
            get { return _temperatureMax; }
            set { _temperatureMax = value; }
        }
        private int _temperatureMax = 70;

        #endregion Exports -> Temperature
    
        #region Exports -> Humidity
        [ExportGroup("Humidity")]
        
        [Export(PropertyHint.Range, "0, 100")] public int HumidityMin
        {
            get { return _humidityMin; }
            set { _humidityMin = value; }
        }
        private int _humidityMin = 0;
        
        [Export(PropertyHint.Range, "0, 100")] public int HumidityMax
        {
            get { return _humidityMax; }
            set { _humidityMax = value; }
        }
        private int _humidityMax = 100;

        #endregion Exports -> Temperature

        #region Exports -> Density
        [ExportGroup("Density")]

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
