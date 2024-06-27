using Godot;
using System.Collections.Generic;

namespace RawVoxel.Meshing;

public class Surface()
{
    public List<Vector3> Vertices = [];
    public List<Color> Colors = [];
    public List<int> Indices = [];
}