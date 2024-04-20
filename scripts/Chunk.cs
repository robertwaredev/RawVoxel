using Godot;
using RawUtils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class Chunk : MeshInstance3D
    {
        #region Constructor

        public Chunk(World world, Material terrainMaterial)
        {
            _world = world;
            MaterialOverride = terrainMaterial;
        }

        #endregion Constructor


        #region Variables -> World

        private readonly World _world;

        #endregion Variables -> World

        #region Variables -> Chunk

        private Vector3I _chunkPosition;
        private Vector3I _chunkGlobalPosition;
        private bool _chunkGenerated;

        #endregion Variables -> Chunk

        #region Variables -> Voxels

        public List<byte> VoxelTypes = new();
        private ImageTexture typeMapYX;
        private ImageTexture typeMapYZ;
        
        #endregion Variables -> Voxels

        #region Variables -> Meshing

        // Needed for this.Mesh.
        private readonly ArrayMesh _arrayMesh = new();
        private readonly Godot.Collections.Array _surfaceArray = new();
        
        // Needed for _surfaceArray.
        private readonly List<Vector3> _surfaceVertices = new();
        private readonly List<Vector3> _surfaceNormals = new();
        private readonly List<int> _surfaceIndices = new();

        #endregion Variables -> Meshing
        

        #region Functions -> Ready

        // Enter the scene tree and call setup methods.
        public override void _Ready()
        {
            SetupSurfaceArray();
            SetupMesh();
        }
        // Resize _surfaceArray to the expected size.
        private void SetupSurfaceArray()
        {
            _surfaceArray.Resize((int)Mesh.ArrayType.Max);
        }
        // Assign this MeshInstance's Mesh parameter to our _arrayMesh.
        private void SetupMesh()
        {
            Mesh = _arrayMesh;
        }
        
        #endregion Functions -> Ready

        #region Functions -> Chunk

        // Set global chunk position based on local chunk position and chunk dimensions.
        private void SetPosition(Vector3I chunkPosition)
        {
            _chunkPosition = chunkPosition;
            _chunkGlobalPosition = _chunkPosition * _world.ChunkDimension;
            Position = _chunkGlobalPosition;
        }
        
        
        // Clear all chunk parameters.
        public void Clear()
        {
            ClearVoxelTypes();
            ClearMesh();
        }
        // Generate a new chunk at the specified position.
        public void Generate(Vector3I chunkPosition)
        {
            SetPosition(chunkPosition);

            ClearVoxelTypes();
            GenerateVoxelTypes();

            Update();
        }
        // Update the chunk mesh at the specified position.
        public void Update()
        {
            GenerateShaderParameters();

            ClearMesh();
            GenerateMesh();
        }
        
        #endregion Functions -> Chunk
        
        #region Functions -> Voxels

        // Clear VoxelTypes array. Only called in GenerateChunk().
        private void ClearVoxelTypes()
        {
            VoxelTypes.Clear();
        }
        // Generate VoxelTypes array. Only called in GenerateChunk().
        private void GenerateVoxelTypes()
        {
            for (int i = 0; i < _world.ChunkDimension.X * _world.ChunkDimension.Y * _world.ChunkDimension.Z; i ++)
            {
                VoxelTypes.Add((byte)GenerateVoxelType(XYZConvert.ToVector3I(i, _world.ChunkDimension)));
            }
        }
        // Return a Voxel.Type based on voxel position.
        private int GenerateVoxelType(Vector3I voxelPosition)
        {
            // Return air if we want to show chunk edges and the voxel being generated is not in this chunk.
            if (_world.ShowChunkEdges && IsVoxelOutOfBounds(voxelPosition)) return 0;
            
            // Get voxelGlobalPosition.
            Vector3I voxelGlobalPosition = voxelPosition + _chunkGlobalPosition;

            #region Temperature
            
            // Wrap voxelGlobalPosition.Z into world coordinates.
            float voxelWorldPositionZ = Mathf.PosMod(voxelGlobalPosition.Z, _world.ChunkDimension.Z * _world.WorldDimension.Z);
            // Convert voxelWorldPositionZ to a positive range.
            float voxelAbsoluteWorldPositionZ = Mathf.Abs(voxelWorldPositionZ) * 2;

            // Sample world temperature.
            float temperatureNoise = _world.TemperatureNoise.GetNoise1D(voxelWorldPositionZ);
            float temperatureDistribution = _world.TemperatureDistribution.Sample(1.0f / voxelAbsoluteWorldPositionZ);
            float temperature = _world.TemperatureRange.Sample((temperatureNoise + 1) * 0.5f * temperatureDistribution);

            #endregion Temperature

            #region Humidity
            
            // Wrap voxelGlobalPosition.X into world coordinates.
            float voxelWorldPositionX = Mathf.PosMod(voxelGlobalPosition.X, _world.ChunkDimension.X * _world.WorldDimension.X);
            // Convert voxelWorldPositionX to a positive range.
            float voxelAbsoluteWorldPositionX = Mathf.Abs(voxelWorldPositionX) * 2;

            // Sample world humidity.
            float humidityNoise = _world.HumidityNoise.GetNoise1D(voxelWorldPositionX);
            float humidityDistribution = _world.HumidityDistribution.Sample(1.0f / voxelAbsoluteWorldPositionX);
            float humidity = _world.HumidityRange.Sample((humidityNoise + 1) * 0.5f * humidityDistribution);

            #endregion Humidity

            #region Biome

            // Create a biome placeholder with a reasonable default.
            Biome voxelBiome = _world.BiomeLibrary.Biomes[0];

            // Determine which biome type the voxel belongs to.
            // TODO - This needs work to make it more forgiving / interpolate values.
            foreach (Biome biome in _world.BiomeLibrary.Biomes)
            {
                if (temperature <= biome.TemperatureMax && temperature >= biome.TemperatureMin)
                {
                    voxelBiome = biome;
                }
            }

            #endregion Biome

            #region Density

            // Sample biome density using voxelGlobalPosition.
            // TODO - Figure out how to use bit shifting to do the 0.5f multiplication.
            float   densitySample = voxelBiome.DensityNoise.GetNoise3Dv(voxelGlobalPosition);
                    densitySample = voxelBiome.DensityCurve.Sample((densitySample + 1) * 0.5f);

            // Return air if voxel is not dense enough to be considered solid.
            if (densitySample < 0.5f) return 0;

            #endregion Density

            #region Layers

            // Sample layers for height using voxelGlobalPosition.Y starting from the bottom layer moving up.
            foreach (BiomeLayer biomeLayer in voxelBiome.Layers.Reverse())
            {
                // Sample layer for height using voxelGlobalPosition.Y.
                // TODO - Figure out how to use bit shifting to do the 0.5f multiplication.
                float   biomeLayerHeightSample = biomeLayer.HeightNoise.GetNoise2D(voxelGlobalPosition.X, voxelGlobalPosition.Z);
                        // Offset biomeLayerHeightSample to expected biomeLayer.HeightCurve sample range.
                        biomeLayerHeightSample = biomeLayer.HeightCurve.Sample((biomeLayerHeightSample + 1) * 0.5f);

                // Determine layer type based on biomeLayerHeightSample.
                if (voxelGlobalPosition.Y <= biomeLayer.Height + biomeLayerHeightSample)
                {
                    // Return the index of the voxel type from the world's VoxelLibrary
                    return Array.IndexOf(_world.VoxelLibrary.Voxels, biomeLayer.VoxelType);
                }
            }
            
            #endregion Layers

            return 0;
        }
        

        // Replace an index in the VoxelTypes array with the specified type.
        public void SetVoxelType(Vector3I voxelPosition, Voxel.Type voxelType)
        {
            voxelPosition.X = Mathf.PosMod(voxelPosition.X, _world.ChunkDimension.X);
            voxelPosition.Y = Mathf.PosMod(voxelPosition.Y, _world.ChunkDimension.Y);
            voxelPosition.Z = Mathf.PosMod(voxelPosition.Z, _world.ChunkDimension.Z);
            
            VoxelTypes[XYZConvert.ToIndex(voxelPosition, _world.ChunkDimension)] = (byte)voxelType;
        }
        // Returns a voxel type from VoxelTypes array or generates a new one if it's out of chunk bounds.
        private Voxel.Type GetVoxelType(Vector3I voxelPosition)
        {
            // If voxel is out of bounds it's also not in VoxelTypes, so we generate the value.
            // FIXME - Casting to a voxel type is not wanted long run, fin this chain of calls to support library lookups.
            if (IsVoxelOutOfBounds(voxelPosition)) return (Voxel.Type)GenerateVoxelType(voxelPosition);

            // If voxel is in bounds, we check its value in VoxelTypes.
            // Use XYZ convert to to voxelPosition into an index in the range of the chunk dimensions.
            return (Voxel.Type)VoxelTypes[XYZConvert.ToIndex(voxelPosition, _world.ChunkDimension)];
        }
        // Returns true if a voxel is not within chunk dimensions.
        private bool IsVoxelOutOfBounds(Vector3I voxelPosition)
        {
            if (voxelPosition.X < 0 || voxelPosition.X >= _world.ChunkDimension.X) return true;
            if (voxelPosition.Y < 0 || voxelPosition.Y >= _world.ChunkDimension.Y) return true;
            if (voxelPosition.Z < 0 || voxelPosition.Z >= _world.ChunkDimension.Z) return true;

            return false;
        }
        
        #endregion Functions -> Voxels

        #region Functions -> Shader

        // Send VoxelTypes to the shader.
        public void GenerateShaderParameters()
        {
            ShaderMaterial terrainShaderMaterial = MaterialOverride as ShaderMaterial;

            // Add shader parameters here using terrainShaderMaterial.SetShaderParameter("shaderArray", _listName.ToArray());
            terrainShaderMaterial.SetShaderParameter("chunkDimension", _world.ChunkDimension);
            terrainShaderMaterial.SetShaderParameter("typeMapYX", GenerateVoxelTypeMap()[0]);
            terrainShaderMaterial.SetShaderParameter("typeMapYZ", GenerateVoxelTypeMap()[1]);
        }
        // Convert VoxelTypes to an ImageTexture to be used in the shader.
        private ImageTexture[] GenerateVoxelTypeMap()
        {
            // Create Images, this works fine.
            Image typeImageYX = Image.Create(_world.ChunkDimension.Y, _world.ChunkDimension.X, true, Image.Format.Rgba8);
            Image typeImageYZ = Image.Create(_world.ChunkDimension.Y, _world.ChunkDimension.Z, true, Image.Format.Rgba8);
            
            // Loop through VoxelTypes.
            for (int i = 0; i < VoxelTypes.Count; i ++)
            {
                // Extract type, color, and position.
                Voxel.Type voxelType = (Voxel.Type)VoxelTypes[i];
                Color voxelColor = Voxel.Colors[voxelType];
                Vector3I voxelPosition = XYZConvert.ToVector3I(i, _world.ChunkDimension);
                
                // Breakpoint for catching a specific color during debug.
                if (voxelType is Voxel.Type.Dirt)
                {}

                // Create the colors as variables here for easier debugging.
                // These are meant to be sampled and added together in the shader, hence the split G value.
                Color colorYX = new(voxelColor.R, voxelColor.G / 2, 0.0f);
                Color colorYZ = new(0.0f, voxelColor.G / 2, voxelColor.B);

                // This doesn't work for some reason.
                typeImageYX.SetPixel(voxelPosition.Y, voxelPosition.X, colorYX);
                typeImageYZ.SetPixel(voxelPosition.Y, voxelPosition.Z, colorYZ);

                // So let's use random numbers and fill the rgb values to get shades of grey.
                Random random = new();
                float rColorR = (float)random.NextDouble();
                float rColorG = (float)random.NextDouble();
                float rColorB = (float)random.NextDouble();

                // This produces noise as expected, which proves that SetPixel works, but why not for my colors?
                typeImageYX.SetPixel(voxelPosition.Y, voxelPosition.X, new Color(rColorR, rColorG / 2, 0.0f));
                typeImageYZ.SetPixel(voxelPosition.Y, voxelPosition.Z, new Color(0.0f, rColorG / 2, rColorB));
            }
            
            ImageTexture typeMapYX = ImageTexture.CreateFromImage(typeImageYX);
            ImageTexture typeMapYZ = ImageTexture.CreateFromImage(typeImageYZ);
            
            typeMapYX.ResourceLocalToScene = true;
            typeMapYZ.ResourceLocalToScene = true;

            return new ImageTexture[] { typeMapYX, typeMapYZ };
        }

        #endregion Functions -> Shader
        
        #region Functions -> Meshing

        // Call all mesh clearing functions as listed below.
        private void ClearMesh()
        {
            ClearChunkMeshSurfaceData();
            ClearMeshSurfaceArray();
            ClearMeshSurface();
            ClearCollision();
        }
        // Call all mesh generation functions as listed below.
        private void GenerateMesh()
        {
            GenerateChunkMeshSurfaceData();
            GenerateMeshSurfaceArray();
            GenerateMeshSurface();
            GenerateCollision();
        }

        
        // Clear _surfaceVertices, _surfaceNormals, and _surfaceIndices arrays.
        private void ClearChunkMeshSurfaceData()
        {
            if (_surfaceVertices.Count > 0) _surfaceVertices.Clear();
            if (_surfaceNormals.Count > 0) _surfaceNormals.Clear();
            if (_surfaceIndices.Count > 0)  _surfaceIndices.Clear();
        }
        // Generate _surfaceVertices, _surfaceNormals, and _surfaceIndices arrays.
        private void GenerateChunkMeshSurfaceData()
        {
            for (int i = 0; i < VoxelTypes.Count; i++)
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

            // _surfaceArray Parameters // These need to be added in GenerateMeshSurfaceArray() and cleared in ClearMeshSurfaceArray().
            _surfaceVertices.AddRange(new List<Vector3> { vertexA, vertexB, vertexC, vertexD });
            _surfaceNormals.AddRange(new List<Vector3> { normal, normal, normal, normal });
            _surfaceIndices.AddRange(new List<int> { 0 + offset, 1 + offset, 2 + offset, 0 + offset, 2 + offset, 3 + offset });
        }


        // Clear and resize _surfaceArray.
        private void ClearMeshSurfaceArray()
        {
            _surfaceArray.Clear();
            SetupSurfaceArray();
        }
        // Pack _surfaceVertices, _surfaceNormals, and _surfaceIndices arrays into _surfaceArray.
        private void GenerateMeshSurfaceArray()
        {   
            // Early return if vertex or index arrays are empty.
            if (_surfaceVertices.Count == 0) return;
            if (_surfaceNormals.Count == 0) return;
            if (_surfaceIndices.Count == 0)  return;
            
            // Add _surfaceVertices and _surfaceIndices arrays to _surfaceArray.
            _surfaceArray[(int)Mesh.ArrayType.Vertex] = _surfaceVertices.ToArray();
            _surfaceArray[(int)Mesh.ArrayType.Normal] = _surfaceNormals.ToArray();
            _surfaceArray[(int)Mesh.ArrayType.Index] = _surfaceIndices.ToArray();
        }
        

        // Clear mesh surfaces.
        private void ClearMeshSurface()
        {
            if (_arrayMesh.GetSurfaceCount() > 0) _arrayMesh.ClearSurfaces();
        }
        // Generate mesh surface using surface array.
        private void GenerateMeshSurface()
        {
            if (_surfaceVertices.Count == 0) return;
            if (_surfaceIndices.Count == 0)  return;

            _arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, _surfaceArray);
        }


        // Clear mesh collision.
        private void ClearCollision()
        {
            StaticBody3D collision = GetChildOrNull<StaticBody3D>(0);
            
            collision?.QueueFree();
        }
        // Generate mesh collision using mesh surface.
        private void GenerateCollision()
        {
            if (_arrayMesh.GetSurfaceCount() == 0) return;

            CreateTrimeshCollision();
            AddToGroup("NavSource");
        }

        #endregion Functions -> Meshing
    }
}
