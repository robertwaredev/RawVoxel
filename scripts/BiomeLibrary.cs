using Godot;
using System;

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class BiomeLibrary : Resource
    {
        #region Constructor

        public BiomeLibrary() {}

        #endregion Constructor

        #region Exports

        [Export] public Biome[] Biomes
        {
            get { return _biomes; }
            set { _biomes = value; }
        }
        private Biome[] _biomes = Array.Empty<Biome>();
        
        #endregion Exports
    }
}
