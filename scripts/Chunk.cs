using Godot;
using System;
using RawUtils;
using System.Linq;
using System.Collections.Generic;

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class Chunk : MeshInstance3D
    {
        public Chunk(World world, Material terrainMaterial)
        {
            _world = world;

            MaterialOverride = terrainMaterial;
        }

        #region Variables

        private World _world;
        private Biome _biome;
        public List<byte> VoxelIDs = new();
        
        private readonly List<Vector3> _surfaceVertices = new();
        private readonly List<Vector3> _surfaceNormals = new();
        private readonly List<Color> _surfaceColors = new();
        private readonly List<int> _surfaceIndices = new();

        #endregion Variables

        #region Functions -> Chunk

        private void SetPosition(Vector3I chunkGridPosition)
        {
            // Offset to signed chunk grid position using world radius.
            chunkGridPosition -= _world.WorldRadius;

            // Set global position using chunk dimensions.
            Position = chunkGridPosition * _world.ChunkDimension;
        }
        private void GenerateBiome(Vector3I chunkGridPosition)
        {
            //float temperatureNoise = (_world.TemperatureNoise.GetNoise1D(voxelUnsignedWorldPositionWrapped.Z) + 1) * 0.5f;
            float temperatureDistribution = _world.TemperatureDistribution.Sample((float)chunkGridPosition.Z / _world.WorldDiameter.Z);
            float temperatureRange = _world.TemperatureRange.Sample(temperatureDistribution);
            float chunkTemperature = temperatureRange;
            
            //float humidityNoise = (_world.HumidityNoise.GetNoise1D(voxelUnsignedWorldPositionWrapped.X) + 1) * 0.5f;
            float humidityDistribution = _world.HumidityDistribution.Sample((float)chunkGridPosition.X / _world.WorldDiameter.X);
            float humidityRange = _world.HumidityRange.Sample(humidityDistribution);
            float chunkHumidity = humidityRange;

            // Determine which biome the chunk belongs to.
            foreach (Biome biome in _world.BiomeLibrary.Biomes)
            {
                if (chunkTemperature <= biome.TemperatureMax && chunkTemperature >= biome.TemperatureMin)
                {
                    _biome = biome;
                }
                else
                {
                    _biome = _world.BiomeLibrary.Biomes[0];
                }
            }
        } 
        public void Generate(int chunkGridIndex)
        {
            // Extract unsigned chunk grid position using world diameter.
            Vector3I chunkGridPosition = XYZConvert.IndexToVector3I(chunkGridIndex, _world.WorldDiameter);

            SetPosition(chunkGridPosition);

            GenerateBiome(chunkGridPosition);

            RawTimer.Time(GenerateVoxelIDs, RawTimer.AppendLine.Pre);

            Update();
        }
        public void Update()
        {
            RawTimer.Time(GenerateChunkMeshSurfaceData);
            RawTimer.Time(GenerateChunkMeshSurface);
            RawTimer.Time(GenerateCollision, RawTimer.AppendLine.Post);
        }

        #endregion Functions -> Chunk
        
        #region Functions -> Voxels

        private void GenerateVoxelIDs()
        {        
            VoxelIDs.Clear();
            
            for (int x = 0; x < _world.ChunkDimension.X; x ++)
            {
                for (int y = 0; y < _world.ChunkDimension.Y; y ++)
                {
                    for (int z = 0; z < _world.ChunkDimension.Z; z ++)
                    {
                        VoxelIDs.Add((byte)GenerateVoxelID(new(x, y, z)));
                    }   
                }
            }
        }
        private int GenerateVoxelID(Vector3I voxelGridPosition)
        {
            // Return air if we want to show chunk edges and the voxel being generated is not in this chunk.
            if (_world.ShowChunkEdges && IsVoxelOutOfBounds(voxelGridPosition)) return 0;

            // Get voxel world position by adding chunk position.
            Vector3I voxelGlobalPosition = (Vector3I)Position + voxelGridPosition;

            // Sample biome density.
            float densityNoise = _biome.DensityNoise.GetNoise3Dv(voxelGlobalPosition);
            float voxelDensity = _biome.DensityCurve.Sample((densityNoise + 1) * 0.5f);

            // Return air if voxel is not dense enough to be considered solid.
            if (voxelDensity < 0.5f) return 0;

            // Sample layers for height starting from the bottom layer moving up.
            foreach (BiomeLayer biomeLayer in _biome.Layers.Reverse())
            {
                // Sample layer for height.
                float heightNoise = biomeLayer.HeightNoise.GetNoise2D(voxelGlobalPosition.X, voxelGlobalPosition.Z);
                float voxelHeight = biomeLayer.HeightCurve.Sample((heightNoise + 1) * 0.5f);

                // Check layer for height match.
                if (voxelGlobalPosition.Y <= biomeLayer.Height + voxelHeight)
                {
                    // Return the index of the matched layer's voxel ID in the world's voxel library.
                    return Array.IndexOf(_world.VoxelLibrary.Voxels, biomeLayer.VoxelType);
                }
            }

            return 0;
        }

        private int GetVoxelID(Vector3I voxelGridPosition)
        {
            // If voxel is out of bounds, generate its VoxelID.
            if (IsVoxelOutOfBounds(voxelGridPosition))
            {
                return GenerateVoxelID(voxelGridPosition);
            }
            
            // Convert voxel grid position to voxel grid index.
            int voxelGridIndex = XYZConvert.Vector3IToIndex(voxelGridPosition, _world.ChunkDimension);

            // If voxel is in bounds, check its value in VoxelIDs.
            return VoxelIDs[voxelGridIndex];
        }
        public void SetVoxelID(Vector3I voxelGridPosition, Voxel.Type voxelType)
        {
            voxelGridPosition.X = Mathf.PosMod(voxelGridPosition.X, _world.ChunkDimension.X);
            voxelGridPosition.Y = Mathf.PosMod(voxelGridPosition.Y, _world.ChunkDimension.Y);
            voxelGridPosition.Z = Mathf.PosMod(voxelGridPosition.Z, _world.ChunkDimension.Z);
            
            VoxelIDs[XYZConvert.Vector3IToIndex(voxelGridPosition, _world.ChunkDimension)] = (byte)voxelType;
        }
           
        private bool IsVoxelOutOfBounds(Vector3I voxelGridPosition)
        {
            if (voxelGridPosition.X < 0 || voxelGridPosition.X >= _world.ChunkDimension.X) return true;
            if (voxelGridPosition.Y < 0 || voxelGridPosition.Y >= _world.ChunkDimension.Y) return true;
            if (voxelGridPosition.Z < 0 || voxelGridPosition.Z >= _world.ChunkDimension.Z) return true;
            
            return false;
        }
        
        #endregion Functions -> Voxels
        
        #region Functions -> Meshing

        private void GenerateChunkMeshSurfaceData()
        {
            _surfaceVertices.Clear();
            _surfaceNormals.Clear();
            _surfaceColors.Clear();
            _surfaceIndices.Clear();
            
            for (int x = 0; x < _world.ChunkDimension.X; x ++)
            {
                for (int y = 0; y < _world.ChunkDimension.Y; y ++)
                {
                    for (int z = 0; z < _world.ChunkDimension.Z; z ++)
                    {
                        GenerateVoxelMeshSurfaceData(new(x, y, z));
                    }   
                }
            }
        }
        private void GenerateVoxelMeshSurfaceData(Vector3I voxelGridPosition)
        {
            int voxelGridIndex = XYZConvert.Vector3IToIndex(voxelGridPosition, _world.ChunkDimension);
            int voxelID = VoxelIDs[voxelGridIndex];
            Color color = _world.VoxelLibrary.Voxels[voxelID].Color;

            #region Naive Meshing

            if (voxelID == 0) return;

            if (GetVoxelID(voxelGridPosition + Vector3I.Up) == 0)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.Top, color, voxelGridPosition);
            }
            if (GetVoxelID(voxelGridPosition + Vector3I.Down) == 0)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.Btm, color, voxelGridPosition);
            }
            if (GetVoxelID(voxelGridPosition + Vector3I.Left) == 0)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.West, color, voxelGridPosition);
            }
            if (GetVoxelID(voxelGridPosition + Vector3I.Right) == 0)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.East, color, voxelGridPosition);
            }
            if (GetVoxelID(voxelGridPosition + Vector3I.Forward) == 0)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.North, color, voxelGridPosition);
            }
            if (GetVoxelID(voxelGridPosition + Vector3I.Back) == 0)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.South, color, voxelGridPosition);
            }
            
            #endregion Naive Meshing
        }
        private void GenerateFaceMeshSurfaceData(Voxel.Face face, Color color, Vector3I voxelGridPosition)
        {
            // Assign vertices for the specified face.
            Vector3I vertexA = Voxel.Vertices[Voxel.Faces[face][0]] + voxelGridPosition;
            Vector3I vertexB = Voxel.Vertices[Voxel.Faces[face][1]] + voxelGridPosition;
            Vector3I vertexC = Voxel.Vertices[Voxel.Faces[face][2]] + voxelGridPosition;
            Vector3I vertexD = Voxel.Vertices[Voxel.Faces[face][3]] + voxelGridPosition;

            // Create normal placeholder.
            Vector3I normal = new();

            // Switch normal based on the specified face.
            switch (face)
            {
                case Voxel.Face.Top:   normal = Vector3I.Up;      break;
                case Voxel.Face.Btm:   normal = Vector3I.Down;    break;
                case Voxel.Face.West:  normal = Vector3I.Left;    break;
                case Voxel.Face.East:  normal = Vector3I.Right;   break;
                case Voxel.Face.North: normal = Vector3I.Forward; break;
                case Voxel.Face.South: normal = Vector3I.Back;    break;

                default: break;
            }

            // Get the offset for indices pointers.
            int offset = _surfaceVertices.Count;

            // Add surface data for this face to their respective lists.
            _surfaceVertices.AddRange(new List<Vector3> { vertexA, vertexB, vertexC, vertexD });
            _surfaceNormals.AddRange(new List<Vector3> { normal, normal, normal, normal });
            _surfaceColors.AddRange(new List<Color> { color, color, color, color });
            _surfaceIndices.AddRange(new List<int> { 0 + offset, 1 + offset, 2 + offset, 0 + offset, 2 + offset, 3 + offset });
        }

        private void GenerateChunkMeshSurface()
        {
            // Clear existing mesh.
            if (IsInstanceValid(Mesh)) Mesh = null;
            
            // Prevent mesh creation with no surface data.
            if (_surfaceVertices.Count == 0) return;
            if (_surfaceNormals.Count == 0) return;
            if (_surfaceColors.Count == 0) return;
            if (_surfaceIndices.Count == 0) return;
            
            // Create new surace array
            Godot.Collections.Array surfaceArray = new();
            surfaceArray.Resize((int)Mesh.ArrayType.Max);

            // Pack surface data into _surfaceArray.
            surfaceArray[(int)Mesh.ArrayType.Vertex] = _surfaceVertices.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Normal] = _surfaceNormals.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Color]  = _surfaceColors.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Index]  = _surfaceIndices.ToArray();
            
            // Create new ArrayMesh.
            ArrayMesh arrayMesh = new();

            // Add surface to arrayMesh using _surfaceArray to populate its data.
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray);

            // Set mesh.
            Mesh = arrayMesh;
        }
        
        private void GenerateCollision()
        {
            // Prevent collision generation with no mesh.
            if (IsInstanceValid(Mesh) == false) return;

            // Clear collision if it exists.
            StaticBody3D collision = GetChildOrNull<StaticBody3D>(0);
            collision?.QueueFree();

            // Create collision.
            CreateTrimeshCollision();
            
            // Update navigation.
            AddToGroup("NavSource");
        }

        #endregion Functions -> Meshing
    }
}
