using Godot;
using RawUtils;
using System.Collections.Generic;
using System.ComponentModel;

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
            SetupTerrainMaterial(terrainMaterial);
        }

        #endregion Constructor

        
        #region Variables -> Constructor

        private readonly World _world;
        private Vector3I _chunkPosition;

        #endregion Variables -> Constructor

        #region Variables -> Voxels

        public List<Voxel.Type> voxels = new();
        
        #endregion Variables -> Voxels

        #region Variables -> Meshing

        // Needed for this.Mesh.
        private readonly ArrayMesh _arrayMesh = new();
        private readonly Godot.Collections.Array _surfaceArray = new();
        
        // Needed for _surfaceArray.
        private readonly List<Vector3> _surfaceVertices = new();
        private readonly List<int> _surfaceIndices = new();
        
        // Needed for MaterialOverride if it's a ShaderMaterial.
        private readonly List<int> _surfaceTypes = new();

        #endregion Variables -> Meshing

        
        #region Functions -> Constructor

        // Set global chunk position based on local chunk position and chunk dimensions.
        private void SetPosition(Vector3I chunkPosition)
        {
            _chunkPosition = chunkPosition;
            Position = _chunkPosition * _world.ChunkDimension;
        }
        
        // Setup material based on material type.
        private void SetupTerrainMaterial(Material terrainMaterial)
        {
            switch (terrainMaterial)
            {
                case StandardMaterial3D:
                    MaterialOverride = SetupStandardMaterial3D(terrainMaterial);
                    break;
                
                case ShaderMaterial:
                    MaterialOverride = SetupShaderMaterial(terrainMaterial);
                    break;
                
                default: break;
            }
        }
        private StandardMaterial3D SetupStandardMaterial3D(Material terrainMaterial)
        {
            StandardMaterial3D terrainStandardMaterial3D = (StandardMaterial3D)terrainMaterial;
            
            terrainStandardMaterial3D.VertexColorUseAsAlbedo = true;

            return terrainStandardMaterial3D;
        }
        private ShaderMaterial SetupShaderMaterial(Material terrainMaterial)
        {
            ShaderMaterial terrainShaderMaterial = (ShaderMaterial)terrainMaterial;
            
            terrainShaderMaterial.SetShaderParameter("chunkDimension", _world.ChunkDimension);

            return terrainShaderMaterial;
        }

        #endregion Functions -> Constructor
        
        #region Functions -> Ready

        // Resize _surfaceArray to the expected size.
        private void SetupSurfaceArray()
        {
            _surfaceArray.Resize((int)Mesh.ArrayType.Max);
        }
        
        // Assign this MeshInstance's mesh parameter to our _arrayMesh.
        private void SetupMesh()
        {
            Mesh = _arrayMesh;
        }

        // Enter the scene tree and call setup methods.
        public override void _Ready()
        {
            SetupSurfaceArray();
            SetupMesh();
        }
        
        #endregion Functions -> Ready

        #region Functions -> Chunk

        public void GenerateChunk()
        {
            RawTimer.Time(GenerateVoxelTypes, RawTimer.AppendLine.Pre);
            RawTimer.Time(GenerateChunkMeshSurfaceData, RawTimer.AppendLine.Post);
            
            // These have negligable time consumption.
            GenerateMeshSurfaceArray();
            GenerateShaderParameters();
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
            ClearVoxelTypes();
            ClearChunkMeshSurfaceData();
            ClearMeshSurfaceArray();
            ClearShaderParameters();
            ClearMeshSurface();
            ClearCollision();
        }

        #endregion Functions -> Chunk
        
        #region Functions -> Voxels

        // Generate voxel types based on position.
        private void GenerateVoxelTypes()
        {
            for (int i = 0; i < _world.ChunkDimension.X * _world.ChunkDimension.Y * _world.ChunkDimension.Z; i ++)
            {
                voxels.Add(GenerateVoxelType(XYZConvert.ToVector3I(i, _world.ChunkDimension)));
            }
        }
        private Voxel.Type GenerateVoxelType(Vector3I voxelPosition)
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
        private void ClearVoxelTypes()
        {
            voxels.Clear();
        }
        
        // Returns a voxel type from voxels array or generates a new one if it's out of chunk bounds.
        private Voxel.Type GetVoxelType(Vector3I voxelPosition)
        {
            if (IsVoxelOutOfBounds(voxelPosition)) return GenerateVoxelType(voxelPosition);

            return voxels[XYZConvert.ToIndex(voxelPosition, _world.ChunkDimension)];
        }
        private bool IsVoxelOutOfBounds(Vector3I voxelPosition)
        {
            if (voxelPosition.X < 0 || voxelPosition.X >= _world.ChunkDimension.X) return true;
            if (voxelPosition.Y < 0 || voxelPosition.Y >= _world.ChunkDimension.Y) return true;
            if (voxelPosition.Z < 0 || voxelPosition.Z >= _world.ChunkDimension.Z) return true;

            return false;
        }
        
        #endregion Functions -> Voxels

        #region Functions -> Meshing

        // Generate vertex and index arrays.
        private void GenerateChunkMeshSurfaceData()
        {
            for (int i = 0; i < voxels.Count; i++)
            {
                GenerateVoxelMeshSurfaceData(XYZConvert.ToVector3I(i, _world.ChunkDimension));
            }
        }
        private void GenerateVoxelMeshSurfaceData(Vector3I voxelPosition)
        {
            Voxel.Type type = GetVoxelType(voxelPosition);

            #region Naive Meshing

            if (type == Voxel.Type.Air) { return; }

            if (GetVoxelType(voxelPosition + Vector3I.Up) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.Top, voxelPosition, type);
            }
            if (GetVoxelType(voxelPosition + Vector3I.Down) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.Btm, voxelPosition, type);
            }
            if (GetVoxelType(voxelPosition + Vector3I.Left) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.West, voxelPosition, type);
            }
            if (GetVoxelType(voxelPosition + Vector3I.Right) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.East, voxelPosition, type);
            }
            if (GetVoxelType(voxelPosition + Vector3I.Forward) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.North, voxelPosition, type);
            }
            if (GetVoxelType(voxelPosition + Vector3I.Back) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.South, voxelPosition, type);
            }
            
            #endregion Naive Meshing
        }
        private void GenerateFaceMeshSurfaceData(Voxel.Face face, Vector3I voxelPosition, Voxel.Type type)
        {
            // Assign vertices for the specified face.
            Vector3I vertexA = Voxel.Vertices[Voxel.Faces[face][0]] + voxelPosition;
            Vector3I vertexB = Voxel.Vertices[Voxel.Faces[face][1]] + voxelPosition;
            Vector3I vertexC = Voxel.Vertices[Voxel.Faces[face][2]] + voxelPosition;
            Vector3I vertexD = Voxel.Vertices[Voxel.Faces[face][3]] + voxelPosition;

            // Get the offset for indices pointers.
            int offset = _surfaceVertices.Count;

            // Add vertices and indices to their respective lists.
            _surfaceVertices.AddRange(new List<Vector3> { vertexA, vertexB, vertexC, vertexD });
            _surfaceIndices.AddRange(new List<int> { 0 + offset, 1 + offset, 2 + offset, 0 + offset, 2 + offset, 3 + offset });
            
            // ShaderMaterial Parameters
        }
        private void ClearChunkMeshSurfaceData()
        {
            if (_surfaceVertices.Count > 0) _surfaceVertices.Clear();
            if (_surfaceIndices.Count > 0)  _surfaceIndices.Clear();
        }
        
        // Pack vertex and index arrays into one surface array.
        private void GenerateMeshSurfaceArray()
        {
            if (_surfaceVertices.Count == 0) { _surfaceArray.Clear(); return; }
            if (_surfaceIndices.Count == 0)  { _surfaceArray.Clear(); return; }
            
            _surfaceArray[(int)Mesh.ArrayType.Vertex] = _surfaceVertices.ToArray();
            _surfaceArray[(int)Mesh.ArrayType.Index] = _surfaceIndices.ToArray();
        }
        private void ClearMeshSurfaceArray()
        {
            if (_surfaceArray.Count == 0) return;

            _surfaceArray.Clear();
            SetupSurfaceArray();
        }

        // Send additional data arrays to the shader.
        private void GenerateShaderParameters()
        {
            ShaderMaterial terrainShaderMaterial = (ShaderMaterial)MaterialOverride;
            
            // Add shader parameters here using terrainShaderMaterial.SetShaderParameter();
        }
        private void ClearShaderParameters()
        {
            // Clear shader parameters here.

            return;
        }

        // Generate mesh surface using surface array.
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

        // Generate mesh collision using mesh surface.
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
