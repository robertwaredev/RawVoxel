using Godot;
using System.Collections.Generic;

namespace RawVoxel.Meshing;

[Tool]
public class Surface()
{
    public List<Vector3> Vertices = [];
    public List<Vector3> Normals = [];
    public List<Color> Colors = [];
    public List<int> Indices = [];
}