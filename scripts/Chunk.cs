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

        #endregion Variables -> Chunk

        #region Variables -> Voxels

        public List<byte> VoxelIDs = new();
        private ImageTexture _idMapYX;
        private ImageTexture _idMapYZ;
        
        #endregion Variables -> Voxels

        #region Variables -> Shader

        private RenderingDevice _renderingDevice;
        private Rid _shader;

        #endregion Variables -> Shader

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
            SetupComputeShader();
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

            SubmitComputeShader();

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

        #region Functions -> Compute Shader

        public void SetupComputeShader()
        {
            // Create local rendering device.
            _renderingDevice = RenderingServer.CreateLocalRenderingDevice();
            
            // Attach _shader to rendering device and get its RID back.
            _shader = Shaders.CreateComputeShader(_renderingDevice, "res://addons/RawVoxel/resources/shaders/ChunkCompute.glsl");
        }

        public void SubmitComputeShader()
        {
            #region Texture Formats

            // Set image width and hieght for noise images.
            int width = _world.ChunkDimension.X;
            int height = _world.ChunkDimension.Z;

            // Create image format for noise textures.
            RDTextureFormat noiseFormat = new()
            {
                Width = (uint)width,
                Height = (uint)height,
                UsageBits = RenderingDevice.TextureUsageBits.CanUpdateBit | RenderingDevice.TextureUsageBits.SamplingBit,
                Format = RenderingDevice.DataFormat.R8Srgb
            };

            #endregion Texture Formats

            #region Data -> Voxels

            int voxelCount = _world.ChunkDimension.X * _world.ChunkDimension.Y * _world.ChunkDimension.Z;
            
            // Set up voxel type byte array.
            byte[] voxelIDBytes = new byte[voxelCount * sizeof(int)];

            #endregion Data -> Voxels

            #region Data -> Temperature

            // World temperature noise.
            FastNoiseLite temperatureNoise = _world.TemperatureNoise.Duplicate() as FastNoiseLite;
            //temperatureNoise.Offset = new Vector3(Position.X, 0, Position.Z);
            
            // World temperature noise image data.
            Image  temperatureImage = temperatureNoise.GetImage(width, height, normalize: false);
            byte[]  temperatureBytes = temperatureImage.GetData();

            // World temperature noise texture & sampler.
            Rid temperatureTexture = _renderingDevice.TextureCreate(noiseFormat, new RDTextureView(), new Godot.Collections.Array<byte[]> { temperatureBytes });
            Rid temperatureSampler = _renderingDevice.SamplerCreate(new() { UnnormalizedUvw = true });

            // World temperature distribution and range curves.
            CurveBytes TemperatureDistribution = Curves.GetCurveBytes(_world.TemperatureDistribution);
            CurveBytes TemperatureRange = Curves.GetCurveBytes(_world.TemperatureRange);
            
            #endregion Data -> Temperature

            #region Data -> Humidity

            // World humidity noise.
            FastNoiseLite humidityNoise = _world.HumidityNoise.Duplicate() as FastNoiseLite;
            //humidityNoise.Offset = new Vector3(Position.X, 0, Position.Z);

            // World humidity noise image data.
            Image  humidityImage = humidityNoise.GetImage(width, height, normalize: false);
            byte[]  humidityBytes = humidityImage.GetData();

            // World humidity noise texture & sampler.
            Rid humidityTexture = _renderingDevice.TextureCreate(noiseFormat, new RDTextureView(), new Godot.Collections.Array<byte[]> { humidityBytes });
            Rid humiditySampler = _renderingDevice.SamplerCreate(new() { UnnormalizedUvw = true });
            
            // World humidity distribution and range curves.
            CurveBytes HumidityDistribution = Curves.GetCurveBytes(_world.HumidityDistribution);
            CurveBytes HumidityRange = Curves.GetCurveBytes(_world.HumidityRange);
            
            #endregion Data -> Humidity            
            
            #region Data -> Biomes

            // Get biome data from world's biome library.
            Array[] biomeBytes = _world.GetBiomeBytes();

            #endregion Data -> Biomes

            #region Buffers -> Voxels

            // Create a storage buffer object for voxel IDs.
            Rid voxelIDBuffer = _renderingDevice.StorageBufferCreate((uint)voxelIDBytes.Length, voxelIDBytes);
            
            #endregion Buffers -> Voxels
            
            #region Buffers -> Temperature

            // Create storage buffer objects for world temperature distribution curve.
            Rid temperatureDistributionPointsBuffer   = _renderingDevice.StorageBufferCreate((uint)TemperatureDistribution.Points.Length, TemperatureDistribution.Points as byte[]);
            Rid temperatureDistributionTangentsBuffer = _renderingDevice.StorageBufferCreate((uint)TemperatureDistribution.Tangents.Length, TemperatureDistribution.Tangents as byte[]);
            
            // Create storage buffer objects for world temperature range curve.
            Rid temperatureRangePointsBuffer   = _renderingDevice.StorageBufferCreate((uint)TemperatureRange.Points.Length, TemperatureRange.Points as byte[]);
            Rid temperatureRangeTangentsBuffer = _renderingDevice.StorageBufferCreate((uint)TemperatureRange.Tangents.Length, TemperatureRange.Tangents as byte[]);
            
            // Create storage buffer objects for temperature min and max values for each biome.
            Rid biomeTemperatureMinBuffer = _renderingDevice.StorageBufferCreate((uint)biomeBytes[0].Length, biomeBytes[0] as byte[]);
            Rid biomeTemperatureMaxBuffer = _renderingDevice.StorageBufferCreate((uint)biomeBytes[1].Length, biomeBytes[1] as byte[]);
            
            #endregion Buffers -> Temperature

            #region Buffers -> Humidity
            
            // Create storage buffer objects for humidity distribution curve.
            Rid humidityDistributionPointsBuffer   = _renderingDevice.StorageBufferCreate((uint)HumidityDistribution.Points.Length, HumidityDistribution.Points as byte[]);
            Rid humidityDistributionTangentsBuffer = _renderingDevice.StorageBufferCreate((uint)HumidityDistribution.Tangents.Length, HumidityDistribution.Tangents as byte[]);
            
            // Create storage buffer objects for humidity range curve.
            Rid humidityRangePointsBuffer   = _renderingDevice.StorageBufferCreate((uint)HumidityRange.Points.Length, HumidityRange.Points as byte[]);
            Rid humidityRangeTangentsBuffer = _renderingDevice.StorageBufferCreate((uint)HumidityRange.Tangents.Length, HumidityRange.Tangents as byte[]);
            
            // Create storage buffer objects for humidity min and max values for each biome.
            Rid biomeHumidityMinBuffer = _renderingDevice.StorageBufferCreate((uint)biomeBytes[2].Length, biomeBytes[2] as byte[]);
            Rid biomeHumidityMaxBuffer = _renderingDevice.StorageBufferCreate((uint)biomeBytes[3].Length, biomeBytes[3] as byte[]);
            
            #endregion Buffers -> Humidity

            #region Uniforms -> Voxels

            // Create uniform for voxel IDs.
            RDUniform voxelIDsUniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 0
            };
            voxelIDsUniform.AddId(voxelIDBuffer);
            
            #endregion Uniforms -> Voxels
            
            #region Uniforms -> Temperature
            
            // Create uniforms for temperature data.
            RDUniform temperatureSamplerUniform = new()
            {
                UniformType = RenderingDevice.UniformType.SamplerWithTexture,
                Binding = 0
            };
            temperatureSamplerUniform.AddId(temperatureSampler);
            temperatureSamplerUniform.AddId(temperatureTexture);

            RDUniform temperatureDistributionPointsUniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 1
            };
            temperatureDistributionPointsUniform.AddId(temperatureDistributionPointsBuffer);

            RDUniform temperatureDistributionTangentsUniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 2
            };
            temperatureDistributionTangentsUniform.AddId(temperatureDistributionTangentsBuffer);
            
            RDUniform temperatureRangePointsUniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 3
            };
            temperatureRangePointsUniform.AddId(temperatureRangePointsBuffer);
            
            RDUniform temperatureRangeTangentsUniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 4
            };
            temperatureRangeTangentsUniform.AddId(temperatureRangeTangentsBuffer);
            
            RDUniform biomeTemperatureMinUniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 5
            };
            biomeTemperatureMinUniform.AddId(biomeTemperatureMinBuffer);

            RDUniform biomeTemperatureMaxUniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 6
            };
            biomeTemperatureMaxUniform.AddId(biomeTemperatureMaxBuffer);

            #endregion Uniforms -> Temperature

            #region Uniforms -> Humidity

            // Create uniforms for humidity data.
            RDUniform humiditySamplerUniform = new()
            {
                UniformType = RenderingDevice.UniformType.SamplerWithTexture,
                Binding = 0
            };
            humiditySamplerUniform.AddId(humiditySampler);
            humiditySamplerUniform.AddId(humidityTexture);

            RDUniform humidityDistributionPointsUniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 1
            };
            humidityDistributionPointsUniform.AddId(humidityDistributionPointsBuffer);

            RDUniform humidityDistributionTangentsUniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 2
            };
            humidityDistributionTangentsUniform.AddId(humidityDistributionTangentsBuffer);
            
            RDUniform humidityRangePointsUniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 3
            };
            humidityRangePointsUniform.AddId(humidityRangePointsBuffer);
            
            RDUniform humidityRangeTangentsUniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 4
            };
            humidityRangeTangentsUniform.AddId(humidityRangeTangentsBuffer);
            
            RDUniform biomeHumidityMinUniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 5
            };
            biomeHumidityMinUniform.AddId(biomeHumidityMinBuffer);

            RDUniform biomeHumidityMaxUniform = new()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 6
            };
            biomeHumidityMaxUniform.AddId(biomeHumidityMaxBuffer);

            #endregion Uniforms -> Humidity

            #region Uniform Sets

            // Create voxel ID uniform array.
            Godot.Collections.Array<RDUniform> voxelUniformArray = new()
            {
                voxelIDsUniform
            };
            Rid voxelUniformSet = _renderingDevice.UniformSetCreate(voxelUniformArray, _shader, 0);
            
            /// Create temperature uniform array.
            Godot.Collections.Array<RDUniform> temperatureUniformArray = new()
            {
                temperatureSamplerUniform,
                temperatureDistributionPointsUniform,
                temperatureDistributionTangentsUniform,
                temperatureRangePointsUniform,
                temperatureRangeTangentsUniform,
                biomeTemperatureMinUniform,
                biomeTemperatureMaxUniform,
            };
            Rid temperatureUniformSet = _renderingDevice.UniformSetCreate(temperatureUniformArray, _shader, 1);

            // Create humidity uniform array.
            Godot.Collections.Array<RDUniform> humidityUniformArray = new()
            {
                humiditySamplerUniform,
                humidityDistributionPointsUniform,
                humidityDistributionTangentsUniform,
                humidityRangePointsUniform,
                humidityRangeTangentsUniform,
                biomeHumidityMinUniform,
                biomeHumidityMaxUniform
            };
            Rid humidityUniformSet = _renderingDevice.UniformSetCreate(humidityUniformArray, _shader, 2);
            
            #endregion Uniform Sets

            #region Pipeline

            // Setup compute pipeline.
            Rid pipeline = _renderingDevice.ComputePipelineCreate(_shader);
            long computeList = _renderingDevice.ComputeListBegin();
            
            _renderingDevice.ComputeListBindComputePipeline(computeList, pipeline);
            _renderingDevice.ComputeListBindUniformSet(computeList, voxelUniformSet, 0);
            _renderingDevice.ComputeListBindUniformSet(computeList, temperatureUniformSet, 1);
            _renderingDevice.ComputeListBindUniformSet(computeList, humidityUniformSet, 2);
            _renderingDevice.ComputeListDispatch(computeList, 4, 4, 4);
            
            _renderingDevice.ComputeListEnd();

            // Submit the rendering device.
            _renderingDevice.Submit();

            // Sychronize the rendering device.
            _renderingDevice.Sync();

            #endregion Pipeline

            #region Results

            byte[] voxelIDBytesOut = _renderingDevice.BufferGetData(voxelIDBuffer);
            int[] voxelIDsOut = new int[voxelCount];
            Buffer.BlockCopy(voxelIDBytesOut, 0, voxelIDsOut, 0, voxelIDBytesOut.Length);
            GD.PrintS("Voxel IDs:", string.Join(", ", voxelIDsOut));

            byte[] tempDistPointsBytesOut = _renderingDevice.BufferGetData(temperatureDistributionPointsBuffer);
            float[] tempDistPointsOut = new float[_world.TemperatureDistribution.PointCount * 2];
            Buffer.BlockCopy(tempDistPointsBytesOut, 0, tempDistPointsOut, 0, tempDistPointsBytesOut.Length);
            GD.PrintS("temperatureDistributionPointBuffer:", string.Join(", ", tempDistPointsOut));

            byte[] tempDistTangentsBytesOut = _renderingDevice.BufferGetData(temperatureDistributionTangentsBuffer);
            float[] tempDistTangentsOut = new float[_world.TemperatureDistribution.PointCount * 2];
            Buffer.BlockCopy(tempDistTangentsBytesOut, 0, tempDistTangentsOut, 0, tempDistTangentsBytesOut.Length);
            GD.PrintS("temperatureDistributionTangentBuffer:", string.Join(", ", tempDistTangentsOut));

            byte[] humDistPointsBytesOut = _renderingDevice.BufferGetData(humidityDistributionPointsBuffer);
            float[] humDistPointsOut = new float[_world.TemperatureDistribution.PointCount * 2];
            Buffer.BlockCopy(humDistPointsBytesOut, 0, humDistPointsOut, 0, humDistPointsBytesOut.Length);
            GD.PrintS("humidityDistributionPointBuffer:", string.Join(", ", humDistPointsOut));

            byte[] humDistTangentsBytesOut = _renderingDevice.BufferGetData(humidityDistributionTangentsBuffer);
            float[] humDistTangentsOut = new float[_world.TemperatureDistribution.PointCount * 2];
            Buffer.BlockCopy(humDistTangentsBytesOut, 0, humDistTangentsOut, 0, humDistTangentsBytesOut.Length);
            GD.PrintS("humidityDistributionTangentBuffer:", string.Join(", ", humDistTangentsOut));
/*
            byte[] tempMinBytesOut = _renderingDevice.BufferGetData(biomeTemperatureMinBuffer);
            int[] tempMinOut = new int[_world.BiomeLibrary.Biomes.Length];
            Buffer.BlockCopy(tempMinBytesOut, 0, tempMinOut, 0, tempMinBytesOut.Length);
            GD.PrintS("biomeTemperatureMinBuffer:", string.Join(", ", tempMinOut));

            byte[] tempMaxBytesOut = _renderingDevice.BufferGetData(biomeTemperatureMaxBuffer);
            int[] tempMaxOut = new int[_world.BiomeLibrary.Biomes.Length];
            Buffer.BlockCopy(tempMaxBytesOut, 0, tempMaxOut, 0, tempMaxBytesOut.Length);
            GD.PrintS("biomeTemperatureMaxBuffer:", string.Join(", ", tempMaxOut));

            byte[] humMinBytesOut = _renderingDevice.BufferGetData(biomeHumidityMinBuffer);
            int[] humMinOut = new int[_world.BiomeLibrary.Biomes.Length];
            Buffer.BlockCopy(humMinBytesOut, 0, humMinOut, 0, humMinBytesOut.Length);
            GD.PrintS("biomeHumidityMinBuffer:", string.Join(", ", humMinOut));

            byte[] humMaxBytesOut = _renderingDevice.BufferGetData(biomeHumidityMaxBuffer);
            int[] humMaxOut = new int[_world.BiomeLibrary.Biomes.Length];
            Buffer.BlockCopy(humMaxBytesOut, 0, humMaxOut, 0, humMaxBytesOut.Length);
            GD.PrintS("biomeHumidityMaxBuffer:", string.Join(", ", humMaxOut));
*/            
            #endregion Results
/*            
            #region Cleanup
            
            _renderingDevice.FreeRid(voxelIDBuffer);
            _renderingDevice.FreeRid(voxelIDUniformSet);
            
            _renderingDevice.FreeRid(temperatureTexture);
            _renderingDevice.FreeRid(temperatureSampler);
            _renderingDevice.FreeRid(temperatureDistributionPointBuffer);
            _renderingDevice.FreeRid(temperatureDistributionTangentBuffer);
            _renderingDevice.FreeRid(temperatureRangePointBuffer);
            _renderingDevice.FreeRid(temperatureRangeTangentBuffer);
            _renderingDevice.FreeRid(biomeTemperatureMinBuffer);
            _renderingDevice.FreeRid(biomeTemperatureMaxBuffer);
            _renderingDevice.FreeRid(temperatureUniformSet);
            
            _renderingDevice.FreeRid(humidityTexture);
            _renderingDevice.FreeRid(humiditySampler);
            _renderingDevice.FreeRid(humidityDistributionPointBuffer);
            _renderingDevice.FreeRid(humidityDistributionTangentBuffer);
            _renderingDevice.FreeRid(humidityRangePointBuffer);
            _renderingDevice.FreeRid(humidityRangeTangentBuffer);
            _renderingDevice.FreeRid(biomeHumidityMinBuffer);
            _renderingDevice.FreeRid(biomeHumidityMaxBuffer);
            _renderingDevice.FreeRid(humidityUniformSet);

            _renderingDevice.FreeRid(pipeline);

            #endregion Cleanup
*/
        }
        
        #endregion Functions -> Compute Shader

        #region Functions -> Spatial Shader

        // Send VoxelIDs to the _shader.
        public void GenerateShaderParameters()
        {
            ShaderMaterial terrainShaderMaterial = MaterialOverride as ShaderMaterial;

            // Add _shader parameters here using terrainShaderMaterial.SetShaderParameter("shaderArray", _listName.ToArray());
            terrainShaderMaterial.SetShaderParameter("chunkDimension", _world.ChunkDimension);
            terrainShaderMaterial.SetShaderParameter("_idMapYX", GenerateVoxelIDMaps()[0]);
            terrainShaderMaterial.SetShaderParameter("_idMapYZ", GenerateVoxelIDMaps()[1]);
        }
        // Convert VoxelIDs to an ImageTexture to be used in the _shader.
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
                // These are meant to be sampled and added together in the _shader, hence the split G value.
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
