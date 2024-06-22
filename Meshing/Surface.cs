using Godot;
using System.Collections.Generic;

namespace RawVoxel.Meshing;

[Tool]
public class Surface()
{
    public List<Vector3> Vertices = [];
    public List<int> Indices = [];
}