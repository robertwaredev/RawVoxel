using Godot;
using System;

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class VoxelLibrary : Resource
    {
        public VoxelLibrary() {}

        [Export] public Voxel[] Voxels
        {
            get { return _voxels; }
            set { _voxels = value; }
        }
        private Voxel[] _voxels = Array.Empty<Voxel>();
    }
}
