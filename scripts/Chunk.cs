using Godot;
using RawUtils;
using System.Collections.Generic;

// TODO - Figure out why the bottom faces of chunks are rendering without showChunkEdges on.
// TODO - Implement ToUInt as an automatic fallback if chunks are too big for ToUShort.

namespace RAWVoxel
{
    [Tool]
    public partial class Chunk : MeshInstance3D
    {
        #region Constructor

        public Chunk(Vector3I chunkPosition, World world, Material terrainMaterial)
        {
            _world = world;
            SetPosition(chunkPosition);
            SetTerrainMaterial(terrainMaterial);
        }

        #endregion Constructor

        #region Variables -> Public

        public Dictionary<ushort, Voxel.Type> voxels = new();
        
        #endregion Variables -> Public
        
        #region Variables -> Setup

        private readonly World _world;
        private Vector3I _chunkPosition;
        private readonly StandardMaterial3D _terrainMaterial = new();
        
        #endregion Variables -> Setup

        #region Variables -> Meshing

        private readonly ArrayMesh _arrayMesh = new();
        private readonly Godot.Collections.Array _surfaceArray = new();
        private readonly List<Vector3> _surfaceVertices = new();
        private readonly List<Vector3> _surfaceNormals = new();
        private readonly List<Color> _surfaceColors = new();
        private readonly List<Vector2> _surfaceUVs = new();
        private readonly List<int> _surfaceIndices = new();

        #endregion Variables -> Meshing

        #region Functions -> Processes

        public override void _Ready()
        {
            SetupSurfaceArray();
            SetupMesh();
        }
        
        #endregion Functions -> Processes

        #region Functions -> Setup

        private void SetPosition(Vector3I chunkPosition)
        {
            _chunkPosition = chunkPosition;
            Position = chunkPosition * _world.ChunkDimension;
        }
        private void SetTerrainMaterial(Material terrainMaterial)
        {
            switch (terrainMaterial)
            {
                case StandardMaterial3D:
                    StandardMaterial3D terrainStandardMaterial3D = (StandardMaterial3D)terrainMaterial;
                    terrainStandardMaterial3D.VertexColorUseAsAlbedo = true;
                    MaterialOverride = terrainStandardMaterial3D;
                    break;
                
                case ShaderMaterial:
                    ShaderMaterial chunkShaderMaterial = (ShaderMaterial)terrainMaterial;
                    MaterialOverride = chunkShaderMaterial;
                    break;
                
                default:
                    MaterialOverride = _terrainMaterial;
                    break;
            }
        }
        private void SetupSurfaceArray()
        {
            _surfaceArray.Resize((int)Mesh.ArrayType.Max);
        }
        private void SetupMesh()
        {
            Mesh = _arrayMesh;
        }

        #endregion Functions -> Setup

        #region Functions -> Generate & Update

        public void GenerateChunk()
        {
            RawTimer.Time(GenerateVoxels, RawTimer.AppendLine.Pre);
            RawTimer.Time(GenerateChunkMeshSurfaceData, RawTimer.AppendLine.Post);
            
            // These have negligable time consumption.
            GenerateMeshSurfaceArray();
            GenerateMeshSurface();
            GenerateCollision();
        }
        public void UpdateChunk(Vector3I chunkPosition)
        {
            SetPosition(chunkPosition);
            
            RawTimer.Time(ClearChunk);
            
            RawTimer.Time(GenerateChunk, RawTimer.AppendLine.Both);
        }
        public void ClearChunk()
        {
            ClearVoxels();
            ClearChunkMeshSurfaceData();
            ClearMeshSurfaceArray();
            ClearMeshSurface();
            ClearCollision();
        }

        private void GenerateVoxels()
        {
            for (int x = 0; x < _world.ChunkDimension.X; x++)
            {
                for (int y = 0; y < _world.ChunkDimension.Y; y++)
                {
                    for (int z = 0; z < _world.ChunkDimension.Z; z++)
                    {
                        ushort voxelPosition = XYZConvert.ToUShort(x, y, z, _world.ChunkDimension);
                        voxels.Add(voxelPosition, GenerateVoxel(new(x, y, z)));
                    }
                }
            }
        }
        private Voxel.Type GenerateVoxel(Vector3I voxelPosition)
        {
            // Return air if we want to show chunk edges and the voxel being generated is not in this chunk.
            if (_world.ShowChunkEdges && IsVoxelOutOfBounds(voxelPosition)) return Voxel.Type.Air;
            
            // Add this chunk's position to the voxel position to get the voxel's global position.
            Vector3I globalVoxelPosition = voxelPosition + (Vector3I)Position;

            // Sample terrain density using the voxel's global position.
            float densityNoise = _world.DensityNoise.GetNoise3Dv(globalVoxelPosition);
            float densityCurve = _world.DensityCurve.Sample((densityNoise + 1) * 0.5f);
            
            // Return air if voxel is not dense enough to be considered solid.
            if (densityCurve < 0.5f) return Voxel.Type.Air;

            // Sample terrain surface using the voxel's global position.
            float surfaceNoise = _world.SurfaceNoise.GetNoise2D(globalVoxelPosition.X, globalVoxelPosition.Z);
            float surfaceCurve = _world.SurfaceCurve.Sample((surfaceNoise + 1) * 0.5f);

            // Switch voxel type based on the generated surface value.
            return globalVoxelPosition.Y switch
            {
                  0 => Voxel.Type.Bedrock,
                > 0 when globalVoxelPosition.Y <  _world.BedrockHeight + (int)(surfaceCurve * _world.BedrockHeight) => Voxel.Type.Bedrock,
                > 0 when globalVoxelPosition.Y <  _world.Layer2Height  + (int)(surfaceCurve * _world.Layer2Height)  => Voxel.Type.Stone,
                > 0 when globalVoxelPosition.Y <  _world.SurfaceHeight + (int)(surfaceCurve * _world.SurfaceHeight) => Voxel.Type.Dirt,
                > 0 when globalVoxelPosition.Y == _world.SurfaceHeight + (int)(surfaceCurve * _world.SurfaceHeight) => Voxel.Type.Grass,
                  _ => Voxel.Type.Air,
            };
        }
        private void ClearVoxels()
        {
            voxels.Clear();
        }
        
        #endregion Functions -> Generate & Update

        #region Functions -> Voxel Checking

        private Voxel.Type GetVoxel(Vector3I voxelPosition)
        {
            if (IsVoxelOutOfBounds(voxelPosition))
            {
                return GenerateVoxel(voxelPosition);
            }

            return voxels[XYZConvert.ToUShort(voxelPosition, _world.ChunkDimension)];
        }
        private bool IsVoxelOutOfBounds(Vector3I voxelPosition)
        {
            if (voxelPosition.X < 0 || voxelPosition.X >= _world.ChunkDimension.X)
            {
                return true;
            }
            if (voxelPosition.Y < 0 || voxelPosition.Y >= _world.ChunkDimension.Y)
            {
                return true;
            }
            if (voxelPosition.Z < 0 || voxelPosition.Z >= _world.ChunkDimension.Z)
            {
                return true;
            }

            return false;
        }
        
        #endregion Functions -> Voxel Checking

        #region Functions -> Meshing

        private void GenerateChunkMeshSurfaceData()
        {
            foreach (ushort voxelPosition in voxels.Keys)
            {
                GenerateVoxelMeshSurfaceData(XYZConvert.ToVector3I(voxelPosition, _world.ChunkDimension));
            }
        }
        private void GenerateVoxelMeshSurfaceData(Vector3I voxelPosition)
        {
            Voxel.Type type = GetVoxel(voxelPosition);
            System.Drawing.Color c = System.Drawing.Color.FromKnownColor(Voxel.Colors[type]);
            Color color = new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

            if (type == Voxel.Type.Air) { return; }

            if (GetVoxel(voxelPosition + Vector3I.Up) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.Top, voxelPosition, color);
            }
            if (GetVoxel(voxelPosition + Vector3I.Down) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.Btm, voxelPosition, color);
            }
            if (GetVoxel(voxelPosition + Vector3I.Left) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.West, voxelPosition, color);
            }
            if (GetVoxel(voxelPosition + Vector3I.Right) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.East, voxelPosition, color);
            }
            if (GetVoxel(voxelPosition + Vector3I.Forward) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.North, voxelPosition, color);
            }
            if (GetVoxel(voxelPosition + Vector3I.Back) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.South, voxelPosition, color);
            }
        }
        private void GenerateFaceMeshSurfaceData(Voxel.Face face, Vector3I voxelPosition, Color color)
        {
            // Assign vertices for the specified face.
            Vector3I vertexA = Voxel.Vertices[Voxel.Faces[face][0]] + voxelPosition;
            Vector3I vertexB = Voxel.Vertices[Voxel.Faces[face][1]] + voxelPosition;
            Vector3I vertexC = Voxel.Vertices[Voxel.Faces[face][2]] + voxelPosition;
            Vector3I vertexD = Voxel.Vertices[Voxel.Faces[face][3]] + voxelPosition;

            // Create normal direction placeholder.
            Vector3I normal = new();

            // Assign normal direction for the specified face.
            switch (face)
            {
                case Voxel.Face.Top: normal = Vector3I.Up; break;
                case Voxel.Face.Btm: normal = Vector3I.Down; break;
                case Voxel.Face.West: normal = Vector3I.Left; break;
                case Voxel.Face.East: normal = Vector3I.Right; break;
                case Voxel.Face.North: normal = Vector3I.Forward; break;
                case Voxel.Face.South: normal = Vector3I.Back; break;

                default: break;
            }

            // Assign UVs for the specified face.
            Vector2I uvA = Voxel.UVs[Voxel.UV.TopLeft];
            Vector2I uvB = Voxel.UVs[Voxel.UV.BtmLeft];
            Vector2I uvC = Voxel.UVs[Voxel.UV.BtmRight];
            Vector2I uvD = Voxel.UVs[Voxel.UV.TopRight];

            // Get the offset for indices pointers.
            int offset = _surfaceVertices.Count;

            // Add surface data to their respective lists.
            _surfaceVertices.AddRange(new List<Vector3> { vertexA, vertexB, vertexC, vertexD });
            _surfaceNormals.AddRange(new List<Vector3> { normal, normal, normal, normal });
            _surfaceColors.AddRange(new List<Color> { color, color, color, color });
            _surfaceUVs.AddRange(new List<Vector2> {uvA, uvB, uvC, uvD});
            _surfaceIndices.AddRange(new List<int> { 0 + offset, 1 + offset, 2 + offset, 0 + offset, 2 + offset, 3 + offset });
        }
        private void ClearChunkMeshSurfaceData()
        {
            if (_surfaceVertices.Count > 0) _surfaceVertices.Clear();
            if (_surfaceNormals.Count > 0)  _surfaceNormals.Clear();
            if (_surfaceColors.Count > 0)   _surfaceColors.Clear();
            if (_surfaceUVs.Count > 0)      _surfaceUVs.Clear();
            if (_surfaceIndices.Count > 0)  _surfaceIndices.Clear();
        }

        private void GenerateMeshSurfaceArray()
        {
            if (_surfaceVertices.Count == 0) { _surfaceArray.Clear(); return; }
            if (_surfaceNormals.Count == 0)  { _surfaceArray.Clear(); return; }
            if (_surfaceColors.Count == 0)   { _surfaceArray.Clear(); return; }
            if (_surfaceIndices.Count == 0)  { _surfaceArray.Clear(); return; }

            _surfaceArray[(int)Mesh.ArrayType.Vertex] = _surfaceVertices.ToArray();
            _surfaceArray[(int)Mesh.ArrayType.Normal] = _surfaceNormals.ToArray();
            _surfaceArray[(int)Mesh.ArrayType.Color] = _surfaceColors.ToArray();
            _surfaceArray[(int)Mesh.ArrayType.TexUV] = _surfaceUVs.ToArray();
            _surfaceArray[(int)Mesh.ArrayType.Index] = _surfaceIndices.ToArray();
        }
        private void ClearMeshSurfaceArray()
        {
            if (_surfaceArray.Count == 0) return;

            _surfaceArray.Clear();
            SetupSurfaceArray();
        }

        private void GenerateMeshSurface()
        {
            if (_surfaceArray.Count == 0)
            {
                SetupSurfaceArray();
                return;
            }

            _arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, _surfaceArray);
        }
        private void ClearMeshSurface()
        {
            if (_arrayMesh.GetSurfaceCount() == 0) return;

            _arrayMesh.ClearSurfaces();
        }

        private void GenerateCollision()
        {
            if (_arrayMesh.GetSurfaceCount() == 0) return;

            CreateTrimeshCollision();
            AddToGroup("NavSource");
        }
        private void ClearCollision()
        {
            StaticBody3D collision = GetChildOrNull<StaticBody3D>(0);
            
            collision?.QueueFree();
        }

        #endregion Functions -> Meshing
    }
}
