using Godot;
using System;

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class Biome : Resource
    {
        #region Exports
        
        [ExportCategory("Layers")]
        [Export] public BiomeLayer[] Layers { get; set; }

        [ExportGroup("Temperature")]
        [Export(PropertyHint.Range, "-1000, 1000")] public int TemperatureMin = -100;
        [Export(PropertyHint.Range, "-1000, 1000")] public int TemperatureMax = 100;

        [ExportGroup("Humidity")]
        [Export(PropertyHint.Range, "0, 100")] public int HumidityMin = 0;
        [Export(PropertyHint.Range, "0, 100")] public int HumidityMax = 100;

        [ExportGroup("Height")]
        [Export] public FastNoiseLite HeightNoise { get; set; }
        
        [ExportGroup("Density")]
        [Export] public FastNoiseLite DensityNoise { get; set; }
        [Export] public Curve DensityCurve { get; set; }

        #endregion Exports
    
        public Biome() {}
        
        public static Biome Generate(World world, Vector3I gridPosition)
        {
            //float temperatureNoise = (_world.TemperatureNoise.GetNoise1D(voxelUnsignedWorldPositionWrapped.Z) + 1) * 0.5f;
            float temperature = world.Temperature.Distribution.Sample((float)(gridPosition.Z + 0.5f) / world.Diameter.Z);
            temperature = world.Temperature.Range.Sample(temperature);
            
            //float humidityNoise = (_world.HumidityNoise.GetNoise1D(voxelUnsignedWorldPositionWrapped.X) + 1) * 0.5f;
            float humidity = world.Humidity.Distribution.Sample((float)(gridPosition.X + 0.5f) / world.Diameter.X);
            humidity = world.Humidity.Range.Sample(humidity);

            Biome thisBiome = world.Biomes[0];
            
            foreach (Biome biome in world.Biomes)
            {
                if (
                    temperature >= biome.TemperatureMin &&
                    temperature <= biome.TemperatureMax &&
                    humidity >= biome.HumidityMin &&
                    humidity <= biome.HumidityMax
                )
                
                thisBiome = biome;
            }

            return thisBiome;
        }
    }
}
