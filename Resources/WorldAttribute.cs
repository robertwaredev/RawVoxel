using Godot;

namespace RawVoxel.Resources;

[GlobalClass, Tool]
public partial class WorldAttribute() : Resource
{
    #region Exports
    
    [Export] public Curve Distribution { get; set; } = new();
    [Export] public Curve Range { get; set; } = new();

    #endregion Exports

    public float Sample(uint axisUGridPosition, uint axisUGridDiameter)
    {
        return Range.Sample(Distribution.Sample((axisUGridPosition + 0.5f) / axisUGridDiameter));
    }
}
