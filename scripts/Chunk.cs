using Godot;
using RawUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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

        private int _chunkIndex;
        private Vector3I _chunkPosition;
        private Vector3I _chunkGlobalPosition;
        private bool _chunkGenerated;

        #endregion Variables -> Chunk

        #region Variables -> Voxels

        public List<byte> VoxelIDs = new();
        private ImageTexture _idMapYX;
        private ImageTexture _idMapYZ;
        
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
        private void SetIndex(int chunkIndex)
        {
            _chunkIndex = chunkIndex;

            _chunkPosition = XYZConvert.IndexToVector3I(_chunkIndex, _world.WorldRadius * 2 + Vector3I.One) - _world.WorldRadius;

            _chunkGlobalPosition = _chunkPosition * _world.ChunkDimension;

            Position = _chunkGlobalPosition;
        }
        // Set global chunk position based on local chunk position and chunk dimensions.
        private void SetPosition(Vector3I chunkPosition)
        {
            _chunkPosition = chunkPosition;
            
            _chunkGlobalPosition = _chunkPosition * _world.ChunkDimension;

            Position = _chunkGlobalPosition;
        }
        

        // Generate a new chunk at the specified position.
        public void GenerateAtIndex(int chunkIndex)
        {
            SetIndex(chunkIndex);

            ClearVoxelIDs();
            GenerateVoxelIDs();

            Update();
        }
        // Generate a new chunk at the specified position.
        public void GenerateAtPosition(Vector3I chunkPosition)
        {
            SetPosition(chunkPosition);

            ClearVoxelIDs();
            GenerateVoxelIDs();

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

        // Clear VoxelIDs array. Only called in GenerateChunk().
        private void ClearVoxelIDs()
        {
            VoxelIDs.Clear();
        }
        // Generate voxel IDs. Only called in GenerateChunk().
        private void GenerateVoxelIDs()
        {
            for (int i = 0; i < _world.ChunkDimension.X * _world.ChunkDimension.Y * _world.ChunkDimension.Z; i ++)
            {
                VoxelIDs.Add((byte)GenerateVoxelID(XYZConvert.IndexToVector3I(i, _world.ChunkDimension)));
            }
        }
        // Return a voxel ID from _world.VoxelLibrary based on voxel position.
        private int GenerateVoxelID(Vector3I voxelPosition)
        {
            // Return air if we want to show chunk edges and the voxel being generated is not in this chunk.
            if (_world.ShowChunkEdges && IsVoxelOutOfBounds(voxelPosition)) return 0;

            // Create a biome placeholder with a reasonable default.
            Biome voxelBiome = _world.BiomeLibrary.Biomes[0];
            
            #region Positioning

            // Get the radius of the chunk in voxel units.
            Vector3I chunkRadiusAsVoxelUnits = new()
            {
                X = _world.ChunkDimension.X >> 1,
                Y = _world.ChunkDimension.Y >> 1,
                Z = _world.ChunkDimension.Z >> 1
            };
            
            // Get the radius of the world in voxel units.
            Vector3I worldRadiusAsVoxelUnits = _world.ChunkDimension * _world.WorldRadius + chunkRadiusAsVoxelUnits;
            
            // Get the diameter of the world in voxel units.
            Vector3I worldDiameterAsVoxelUnits = new()
            {
                X = worldRadiusAsVoxelUnits.X << 1,
                Y = worldRadiusAsVoxelUnits.Y << 1,
                Z = worldRadiusAsVoxelUnits.Z << 1
            };

            
            // Get chunk world position in a negative to positive range.
            Vector3 chunkSignedWorldPosition = _chunkGlobalPosition - chunkRadiusAsVoxelUnits;
   
            // Get chunk world position in a positive range.
            Vector3 chunkUnsignedWorldPosition = (chunkSignedWorldPosition + worldRadiusAsVoxelUnits) * 0.5f;

            // Get wrapped chunk world position in a positive range.
            Vector3 chunkUnsignedWorldPositionWrapped = chunkUnsignedWorldPosition % worldDiameterAsVoxelUnits;
            
            // Get wrapped chunk world position in a negative to positive range.
            Vector3 chunkSignedWorldPositionWrapped = chunkUnsignedWorldPositionWrapped - worldRadiusAsVoxelUnits;
            

            // Get voxel world position in a negative to positive range.
            Vector3 voxelSignedWorldPosition = voxelPosition + chunkSignedWorldPosition;
            
            // Get voxel world position in an unsigned value.
            Vector3 voxelUnsignedWorldPosition = voxelPosition + chunkUnsignedWorldPosition;
            
            // Get wrapped voxel world position in a positive range.
            Vector3 voxelUnsignedWorldPositionWrapped = voxelPosition + chunkUnsignedWorldPositionWrapped;
            
            // Get wrapped voxel world position in a negative to positive range.
            Vector3 voxelSignedWorldPositionWrapped = voxelPosition + chunkSignedWorldPositionWrapped;
            
            
            // Normalize wrapped, unsigned voxel world position to a 0 - 1 range.
            Vector3 voxelUnsignedWorldPositionWrappedNormalized = voxelUnsignedWorldPositionWrapped / worldDiameterAsVoxelUnits;

            #endregion Positioning

            #region Temperature

            //float temperatureNoise = (_world.TemperatureNoise.GetNoise1D(voxelUnsignedWorldPositionWrapped.Z) + 1) * 0.5f;
            float temperatureDistribution = _world.TemperatureDistribution.Sample(voxelUnsignedWorldPositionWrappedNormalized.Z);
            float temperatureRange = _world.TemperatureRange.Sample(temperatureDistribution);
            float voxelTemperature = temperatureRange;

            #endregion Temperature

            #region Humidity
            
            //float humidityNoise = (_world.HumidityNoise.GetNoise1D(voxelUnsignedWorldPositionWrapped.X) + 1) * 0.5f;
            float humidityDistribution = _world.HumidityDistribution.Sample(voxelUnsignedWorldPositionWrappedNormalized.X);
            float humidityRange = _world.HumidityRange.Sample(humidityDistribution);
            float voxelHumidity = humidityRange;

            #endregion Humidity

            #region Biome

            // FIXME - This needs work to make it more forgiving / interpolate values.
            // Determine which biome the voxel belongs to.
            foreach (Biome biome in _world.BiomeLibrary.Biomes)
            {
                if (
                    voxelTemperature <= biome.TemperatureMax
                    && voxelTemperature >= biome.TemperatureMin
                )
                {
                    voxelBiome = biome;
                }
            }

            #endregion Biome

            #region Density

            // Sample biome density.
            float densityNoise = voxelBiome.DensityNoise.GetNoise3Dv(voxelSignedWorldPosition);
            float voxelDensity = voxelBiome.DensityCurve.Sample((densityNoise + 1) * 0.5f);

            // Return air if voxel is not dense enough to be considered solid.
            if (voxelDensity < 0.5f) return 0;

            #endregion Density

            #region Height

            // TODO - Figure out a way to reduce the amount of bloat this can cause.
            // Sample layers for height starting from the bottom layer moving up.
            foreach (BiomeLayer biomeLayer in voxelBiome.Layers.Reverse())
            {
                // Sample layer for height.
                float heightNoise = biomeLayer.HeightNoise.GetNoise2D(voxelSignedWorldPosition.X, voxelSignedWorldPosition.Z);
                float voxelHeight = biomeLayer.HeightCurve.Sample((heightNoise + 1) * 0.5f);

                // Check layer for height match.
                if (voxelSignedWorldPosition.Y <= biomeLayer.Height + voxelHeight)
                {
                    // Return the index of the matched layer's voxel ID in the world's voxel library.
                    return Array.IndexOf(_world.VoxelLibrary.Voxels, biomeLayer.VoxelType);
                }
            }
            
            #endregion Height

            return 0;
        }
        

        // Returns a voxel type from VoxelIDs array or generates a new one if it's out of chunk bounds.
        private int GetVoxelID(Vector3I voxelPosition)
        {
            // If voxel is out of bounds it's also not in VoxelIDs, so we generate the value.
            // FIXME - Casting to a voxel type is not wanted long run, fin this chain of calls to support library lookups.
            if (IsVoxelOutOfBounds(voxelPosition)) return GenerateVoxelID(voxelPosition);

            // If voxel is in bounds, we check its value in VoxelIDs.
            // Use XYZ convert to to voxelPosition into an index in the range of the chunk dimensions.
            return VoxelIDs[XYZConvert.Vector3IToIndex(voxelPosition, _world.ChunkDimension)];
        }
        // Replace an index in the VoxelIDs array with the specified type.
        public void SetVoxelID(Vector3I voxelPosition, Voxel.Type voxelType)
        {
            voxelPosition.X = Mathf.PosMod(voxelPosition.X, _world.ChunkDimension.X);
            voxelPosition.Y = Mathf.PosMod(voxelPosition.Y, _world.ChunkDimension.Y);
            voxelPosition.Z = Mathf.PosMod(voxelPosition.Z, _world.ChunkDimension.Z);
            
            VoxelIDs[XYZConvert.Vector3IToIndex(voxelPosition, _world.ChunkDimension)] = (byte)voxelType;
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

        #region Functions -> Spatial Shader

        // Send VoxelIDs to the shader.
        public void GenerateShaderParameters()
        {
            ShaderMaterial terrainShaderMaterial = MaterialOverride as ShaderMaterial;

            // Add shader parameters here using terrainShaderMaterial.SetShaderParameter("shaderArray", _listName.ToArray());
            terrainShaderMaterial.SetShaderParameter("chunkDimension", _world.ChunkDimension);
            terrainShaderMaterial.SetShaderParameter("_idMapYX", GenerateVoxelIDMaps()[0]);
            terrainShaderMaterial.SetShaderParameter("_idMapYZ", GenerateVoxelIDMaps()[1]);
        }
        // Convert VoxelIDs to an ImageTexture to be used in the shader.
        private ImageTexture[] GenerateVoxelIDMaps()
        {
            // Create Images, this works fine.
            Image typeImageYX = Image.Create(_world.ChunkDimension.Y, _world.ChunkDimension.X, true, Image.Format.Rgba8);
            Image typeImageYZ = Image.Create(_world.ChunkDimension.Y, _world.ChunkDimension.Z, true, Image.Format.Rgba8);
            
            // Loop through VoxelIDs.
            for (int i = 0; i < VoxelIDs.Count; i ++)
            {
                // Extract type, color, and position.
                Voxel.Type voxelType = (Voxel.Type)VoxelIDs[i];
                Color voxelColor = Voxel.Colors[voxelType];
                Vector3I voxelPosition = XYZConvert.IndexToVector3I(i, _world.ChunkDimension);
                
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
            
            ImageTexture _idMapYX = ImageTexture.CreateFromImage(typeImageYX);
            ImageTexture _idMapYZ = ImageTexture.CreateFromImage(typeImageYZ);
            
            _idMapYX.ResourceLocalToScene = true;
            _idMapYZ.ResourceLocalToScene = true;

            return new ImageTexture[] { _idMapYX, _idMapYZ };
        }

        #endregion Functions -> Spatial Shader
        
        #region Functions -> Meshing

        // Call all mesh clearing functions in the proper order.
        private void ClearMesh()
        {
            ClearChunkMeshSurfaceData();
            ClearMeshSurfaceArray();
            ClearMeshSurface();
            ClearCollision();
        }
        // Call all mesh generation functions in the proper order.
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
            for (int i = 0; i < _world.ChunkDimension.X * _world.ChunkDimension.Y * _world.ChunkDimension.Z; i ++)
            {
                GenerateVoxelMeshSurfaceData(XYZConvert.IndexToVector3I(i, _world.ChunkDimension));
            }
        }
        private void GenerateVoxelMeshSurfaceData(Vector3I voxelPosition)
        {
            int voxelID = GetVoxelID(voxelPosition);

            #region Naive Meshing

            if (voxelID == 0) { return; }

            // TODO - Figure out how to index positions in list without vector math.

            if (GetVoxelID(voxelPosition + Vector3I.Up) == 0)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.Top, voxelPosition);
            }
            if (GetVoxelID(voxelPosition + Vector3I.Down) == 0)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.Btm, voxelPosition);
            }
            if (GetVoxelID(voxelPosition + Vector3I.Left) == 0)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.West, voxelPosition);
            }
            if (GetVoxelID(voxelPosition + Vector3I.Right) == 0)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.East, voxelPosition);
            }
            if (GetVoxelID(voxelPosition + Vector3I.Forward) == 0)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.North, voxelPosition);
            }
            if (GetVoxelID(voxelPosition + Vector3I.Back) == 0)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.South, voxelPosition);
            }
            
            #endregion Naive Meshing
        }
        private void GenerateFaceMeshSurfaceData(Voxel.Face face, Vector3I voxelPosition)
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

            // Add surface data for this face to their respective lists.
            // These lists need to be converted to arrays in GenerateMeshSurfaceArray() and cleared in ClearMeshSurfaceArray().
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
            // Early return if any of the data arrays are empty.
            if (_surfaceVertices.Count == 0) return;
            if (_surfaceNormals.Count == 0) return;
            if (_surfaceIndices.Count == 0)  return;
            
            // Add _surfaceVertices and _surfaceIndices arrays to _surfaceArray.
            _surfaceArray[(int)Mesh.ArrayType.Vertex] = _surfaceVertices.ToArray();
            _surfaceArray[(int)Mesh.ArrayType.Normal] = _surfaceNormals.ToArray();
            _surfaceArray[(int)Mesh.ArrayType.Index] = _surfaceIndices.ToArray();
        }
        

        // Check the mesh for surfaces and clear them if any.
        private void ClearMeshSurface()
        {
            if (_arrayMesh.GetSurfaceCount() > 0) _arrayMesh.ClearSurfaces();
        }
        // Generate mesh surface using surface array.
        private void GenerateMeshSurface()
        {
            // Early return if any of the data arrays are empty.
            if (_surfaceVertices.Count == 0) return;
            if (_surfaceNormals.Count == 0) return;
            if (_surfaceIndices.Count == 0)  return;

            // Add surface to _arrayMesh using _surfaceArray to populate its data.
            _arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, _surfaceArray);
        }


        // Clear mesh collision nodes.
        private void ClearCollision()
        {
            StaticBody3D collision = GetChildOrNull<StaticBody3D>(0);
            
            collision?.QueueFree();
        }
        // Generate mesh collision nodes using mesh surface.
        private void GenerateCollision()
        {
            if (_arrayMesh.GetSurfaceCount() == 0) return;

            CreateTrimeshCollision();
            // TODO - Check if this really needs to be called every time the mesh is rebuilt.
            AddToGroup("NavSource");
        }

        #endregion Functions -> Meshing
    }
}
