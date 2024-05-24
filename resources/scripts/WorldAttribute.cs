using Godot;

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class WorldAttribute : Resource
    {
        #region Exports
        
        [Export] public Curve Distribution { get; set; } = new();
        [Export] public Curve Range { get; set; } = new();

        #endregion Exports

        public WorldAttribute() {}

        public float Sample(int axisGridPosition, int axisGridDiameter)
        {
            return Range.Sample(Distribution.Sample((axisGridPosition + 0.5f) / axisGridDiameter));
        }
    }
}