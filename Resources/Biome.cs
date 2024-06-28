using Godot;

namespace RawVoxel.Resources;

[GlobalClass, Tool]
public partial class Biome() : Resource
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

    // FIXME - This is NOT the ideal way to handle biome generation.
    public static Biome Generate(Vector3I chunkGridPosition, Vector3I worldDiameter, WorldSettings worldSettings)
    {
        float temperature = worldSettings.Temperature.Distribution.Sample((float)(chunkGridPosition.Z + 0.5f) / worldDiameter.Z);
        temperature = worldSettings.Temperature.Range.Sample(temperature);

        float humidity = worldSettings.Humidity.Distribution.Sample((float)(chunkGridPosition.X + 0.5f) / worldDiameter.X);
        humidity = worldSettings.Humidity.Range.Sample(humidity);

        Biome thisBiome = worldSettings.Biomes[0];

        foreach (Biome biome in worldSettings.Biomes)
        {
            if
            (
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
