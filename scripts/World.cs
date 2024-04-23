using Godot;
using System;
using RawUtils;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

// TODO - Thread Pool
// TODO - Convert RecycleChunks and GenerateChunks to threads.
// TODO - Fix chunk loading to always load chunks closest to focus node at surface level first.

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class World : Node3D
    {
        #region Exports -> Tools
        [ExportCategory("Tools")]

        [Export] public bool Regenerate
        {
            get { return _regenerate; }
            set { _regenerate = false; FreeChunks(); _worldGenerated = false; }
        }
        private bool _regenerate = false;

        #endregion Exports -> Tools

        #region Exports -> FocusNode
        [Export] public Node3D FocusNode
        {
            get { return _focusNode; }
            set { _focusNode = value; _worldGenerated = false; }
        }
        private Node3D _focusNode;
        
        #endregion Exports -> FocusNode

        #region Exports -> Voxel Library

        [Export] public VoxelLibrary VoxelLibrary
        {
            get { return _voxelLibrary; }
            set { _voxelLibrary = value; }
        }
        private VoxelLibrary _voxelLibrary = new();

        #endregion Exports -> Voxel Library
        
        #region Exports -> Biome Library

        [Export] public BiomeLibrary BiomeLibrary
        {
            get { return _biomeLibrary; }
            set { _biomeLibrary = value; }
        }
        private BiomeLibrary _biomeLibrary = new();

        #endregion Exports -> Biome Library

        #region Exports -> Dimensions
        [ExportGroup("Dimensions")]
        
        [Export] public Vector3I DrawRadius
        {
            get { return _drawRadius; }
            set { _drawRadius = value; }
        }
        private Vector3I _drawRadius = new(1, 0, 1);
        
        [Export] public Vector3I WorldRadius
        {
            get { return _worldRadius; }
            set { _worldRadius = value; }
        }
        private Vector3I _worldRadius = new (128, 1, 128);
        
        [Export] public Vector3I ChunkDimension
        {
            get { return _chunkDimension; }
            set { _chunkDimension = value; }
        }
        private Vector3I _chunkDimension = new (16, 256, 16);
        
        [Export] public float VoxelDimension
        {
            get { return _voxelDimension; }
            set { _voxelDimension = value; }
        }
        private float _voxelDimension = 1;
        
        #endregion Exports -> Dimensions
        
        #region Exports -> Temperature
        [ExportGroup("Temperature")]
        
        // Controls Z value for terrain generation.
        [Export] public FastNoiseLite TemperatureNoise
        {
            get { return _temperatureNoise; }
            set { _temperatureNoise = value; }
        }
        private FastNoiseLite _temperatureNoise = new();
        
        // Controls temperature distribution.
        [Export] public Curve TemperatureDistribution
        {
            get { return _temperatureDistribution; }
            set { _temperatureDistribution = value; }
        }
        private Curve _temperatureDistribution = new();
        
        // Controls temperature range.
        [Export] public Curve TemperatureRange
        {
            get { return _temperatureRange; }
            set { _temperatureRange = value; }
        }
        private Curve _temperatureRange = new();

        #endregion Exports -> Temperature
        
        #region Exports -> Humidity
        [ExportGroup("Humidity")]

        // Controls X value for terrain generation.
        [Export] public FastNoiseLite HumidityNoise
        {
            get { return _humidityNoise; }
            set { _humidityNoise = value; }
        }
        private FastNoiseLite _humidityNoise = new();
        
        // Controls humidity distribution.
        [Export] public Curve HumidityDistribution
        {
            get { return _humidityDistribution; }
            set { _humidityDistribution = value; }
        }
        private Curve _humidityDistribution = new();

        // Controls humidity range.
        [Export] public Curve HumidityRange
        {
            get { return _humidityRange; }
            set { _humidityRange = value; }
        }
        private Curve _humidityRange = new();

        #endregion Exports -> Humidity
        
        #region Exports -> Material
        [ExportGroup("Material")]

        [Export] public Material TerrainMaterial
        {
            get { return _terrainMaterial; }
            set { _terrainMaterial = value; }
        }
        private Material _terrainMaterial = GD.Load<Material>("res://addons/RawVoxel/resources/materials/ChunkShaderMaterial.tres");
        

        #endregion Exports -> Material

        #region Exports -> Rendering
        [ExportGroup("Rendering")]
        
        [Export] public bool ShowChunkEdges
        {
            get { return _showChunkEdges; }
            set { _showChunkEdges = value; }
        }
        private bool _showChunkEdges = true;
        
        #endregion Exports -> Rendering

        #region Exports -> Threading
        [ExportGroup("Threading")]
        
        [Export] public int updateFrequency
        {
            get { return _updateFrequency; }
            set { _updateFrequency = value; }
        }
        private int _updateFrequency = 100;

        #endregion Exports -> Threading

        
        #region Variables -> FocusNode
        
        private Vector3 _focusNodePosition;
        public Vector3I FocusNodeChunkPosition = Vector3I.MinValue;
        private readonly object _focusNodeLock = new();
        
        #endregion Variables -> FocusNode

        #region Variables -> World
        
        private bool _worldGenerated = false;
        private readonly object _worldGenerationLock = new();
        
        #endregion Variables -> World

        #region Variables -> Chunks (Index Based)
        
        private readonly List<int> _drawableChunkIndices = new();
        private readonly List<int> _loadableChunkIndices = new();
        private readonly List<int> _freeableChunkIndices = new();
        private readonly Dictionary<int, Chunk> _loadedChunkIndices = new();

        #endregion Variables -> Chunks (Index Based)

        #region Variables -> Chunks (Position Based)

        private readonly List<Vector3I> _drawableChunkPositions = new();
        private readonly List<Vector3I> _loadableChunkPositions = new();
        private readonly List<Vector3I> _freeableChunkPositions = new();
        private readonly Dictionary<Vector3I, Chunk> _loadedChunkPositions = new();

        #endregion Variables -> Chunks (Position Based)

        
        #region Functions -> Ready

        public override void _Ready()
        {
            ThreadStart UpdateWorldProcessStart = new(WorldProcess);
            Thread UpdateWorldThread = new(UpdateWorldProcessStart) { Name = "UpdateWorldThread" };
            
            UpdateWorldThread.Start();
        }
        public override string[] _GetConfigurationWarnings()
        {
            if (FocusNode == null)
            {
                return new string[]
                {
                    "A Focus Node must be selected in the inspector to generate the world around.",
                    "(You can select the world itself.)"
                };
            }

            return new string[]{};
        }

        #endregion Functions -> Ready

        #region Functions -> Processes

        public override void _PhysicsProcess(double delta)
        {
            TryUpdateFocusNodePosition();
        }
        private void WorldProcess()
        {
            while (IsInstanceValid(this))
            {
                if (_worldGenerated == false)
                {
                    GenerateChunks();
                }
                
                else if (TryUpdateFocusNodeChunkPosition())
                {
                    RecycleChunks();
                }
                
                Thread.Sleep(100);
            }
        }
        
        #endregion Functions -> Processes

        #region Functions -> FocusNode

        // Set FocusNodeChunkPosition to _focusNode.Position. Locked for thread access.
        private void TryUpdateFocusNodePosition()
        {
            if (_focusNode == null) return;
            
            // Lock access to _focusNodePosition for primary thread.
            lock (_focusNodeLock)
            {
                // Copy _focusNode position to _focusNodePosition so it can be checked accessed in other threads.
                _focusNodePosition = _focusNode.Position;
            }
        }
        // Set FocusNodeChunkPosition when _focusNodePosition points to a different chunk. Locked for thread access.
        private bool TryUpdateFocusNodeChunkPosition()
        {
            if (_focusNode == null) return false;
            
            Vector3I queriedFocusNodeChunkPosition;
            
            // Lock acccess to _focusNodePosition for secondary thread.
            lock (_focusNodeLock)
            {
                // Calculate queriedFocusNodeChunkPosition for the current frame.
                queriedFocusNodeChunkPosition = (Vector3I)(_focusNodePosition / _chunkDimension).Floor();
            }
            
            // Check to see if _focusNodePosition points to a different chunk. If true, update FocusNodeChunkPosition and return.
            if (FocusNodeChunkPosition != queriedFocusNodeChunkPosition)
            {
                FocusNodeChunkPosition = queriedFocusNodeChunkPosition;

                return true;
            }

            return false;
        }
        
        #endregion Functions -> FocusNode

        #region Functions -> World

        // Queue, load, and free chunks to and from the scene tree.
        private void GenerateChunks()
        {   
            // Ensure that this can't be called if RecycleChunks() is running.
            // Ensure that this can't be called again while it's still running.
            // This is to prevent crashes when making changes in the inspector that might cause overlap.
            lock (_worldGenerationLock)
            {
                TryUpdateFocusNodePosition();
                TryUpdateFocusNodeChunkPosition();

                GD.PrintS("--- Generating World at:", FocusNodeChunkPosition, "---");

                GenerateChunksViaIndices();
                
                GD.PrintS("--- World Generated at:", FocusNodeChunkPosition, "---");
                GD.Print(" ");

                _worldGenerated = true;
            }
        }
        // Reposition chunks from _freeableChunkPositions to _loadableChunkPositions.
        private void RecycleChunks()
        {   
            // Ensure that this can't be called if GenerateChunks() is running.
            // Ensure that this can't be called again while it's still running.
            // This is to prevent crashes when making changes in the inspector that might cause overlap.
            lock (_worldGenerationLock)
            {
                GD.PrintS("--- Updating World at:", FocusNodeChunkPosition, "---");
                
                RecycleChunksViaIndices();

                GD.PrintS("--- World Updated at:", FocusNodeChunkPosition, "---");
                GD.Print(" ");
            }
        }
        // Free all chunks from the scene tree.
        private void FreeChunks()
        {
            FreeChunksViaIndices();
        }
        
        #endregion Functions -> World

        #region Functions -> Chunks (Index Based)

        // Queue, load, and free chunks to and from the scene tree using index handles.
        private void GenerateChunksViaIndices()
        {   
            QueueDrawableChunkIndices();
            QueueLoadableChunkIndices();
            QueueFreeableChunkIndices();
                
            LoadQueuedChunkIndices();
            FreeQueuedChunkIndices();
        }
        // Reposition chunks from _freeableChunkIndices to _loadableChunkIndices.
        private void RecycleChunksViaIndices()
        {
            QueueDrawableChunkIndices();
            QueueLoadableChunkIndices();
            QueueFreeableChunkIndices();
            
            RecycleQueuedChunkIndices();
        }
        // Free all chunks from the scene tree using their index handles.
        private void FreeChunksViaIndices()
        {
            if (_loadedChunkIndices.Count == 0) return;

            foreach (int chunkIndex in _loadedChunkIndices.Keys)
            {
                Chunk chunk = _loadedChunkIndices[chunkIndex];
                
                _loadedChunkIndices.Remove(chunkIndex);
                
                chunk.QueueFree();
            }
        }

        
        // Add an index to its respective List for each drawable chunk index.
        private void QueueDrawableChunkIndices()
        {
            _drawableChunkIndices.Clear();

            int drawableChunkCount = (_drawRadius.X * 2 + 1) * (_drawRadius.Y * 2 + 1) * (_drawRadius.Z * 2 + 1);

            for (int drawableChunkIndex = 0; drawableChunkIndex < drawableChunkCount; drawableChunkIndex ++)
            {
                // Get the drawable diameter in chunk units.
                Vector3I drawDiameter = _drawRadius * 2 + Vector3I.One;
                
                // Extract an unsigned chunk position using drawable chunk dimensions.
                Vector3I unsignedChunkDrawablePosition = XYZConvert.IndexToVector3I(drawableChunkIndex, drawDiameter);
                // Offset to signed position using world radius.
                Vector3I signedChunkDrawablePosition = unsignedChunkDrawablePosition - _drawRadius;

                // Add signed drawable chunk position to focus node chunk position to get it's world position.
                Vector3I signedChunkWorldPosition = FocusNodeChunkPosition + signedChunkDrawablePosition;
                // Offset to signed position using world radius.
                Vector3I unsignedChunkWorldPosition = signedChunkWorldPosition + _worldRadius;
                
                // Convert chunk world position into an index.
                int chunkWorldIndex = XYZConvert.Vector3IToIndex(unsignedChunkWorldPosition, _worldRadius * 2 + Vector3I.One);
                
                // Off we go.
                _drawableChunkIndices.Add(chunkWorldIndex);
            }

            GD.PrintS("--> Drawable chunks:" + _drawableChunkIndices.Count);
        }
        // Add an index to its respective List for each loadable chunk index.
        private void QueueLoadableChunkIndices()
        {
            if (_drawableChunkIndices.Count == 0) return;
            
            _loadableChunkIndices.Clear();

            foreach (int drawableChunkIndex in _drawableChunkIndices)
            {
                if (_loadedChunkIndices.ContainsKey(drawableChunkIndex) == false)
                {
                    _loadableChunkIndices.Add(drawableChunkIndex);
                }
            }

            GD.PrintS("--> Loadable chunks:" + _loadableChunkIndices.Count);
        }
        // Add an index to its respective List for each freeable chunk index.
        private void QueueFreeableChunkIndices()
        {
            if (_loadedChunkIndices.Count == 0) return;
            
            _freeableChunkIndices.Clear();

            foreach (int loadedChunkIndex in _loadedChunkIndices.Keys)
            {
                if (_drawableChunkIndices.Contains(loadedChunkIndex) == false)
                {
                    _freeableChunkIndices.Add(loadedChunkIndex);
                }
            }

            GD.PrintS("--> Freeable chunks:" + _freeableChunkIndices.Count);
        }

        
        // Load a Chunk instance into the scene tree for each index in _loadableChunkIndices.
        private void LoadQueuedChunkIndices()
        {
            if (_loadableChunkIndices.Count == 0) return;
            
            foreach (int loadableChunkIndex in _loadableChunkIndices)
            {
                Chunk chunk = new(this, (Material)_terrainMaterial.Duplicate(true));
                
                _loadedChunkIndices.Add(loadableChunkIndex, chunk);
                
                CallDeferred(Node.MethodName.AddChild, chunk);

                Task generate = new(new Action(() => chunk.CallDeferred(nameof(Chunk.GenerateAtIndex), loadableChunkIndex)));

                generate.Start();
                generate.Wait();

                Thread.Sleep(_updateFrequency);
            }
        
            _loadableChunkIndices.Clear();
        }
        // Free a Chunk instance from the scene tree for each index in _freeableChunkIndices.
        private void FreeQueuedChunkIndices()
        {
            if (_freeableChunkIndices.Count == 0) return;

            foreach (int freeableChunkIndex in _freeableChunkIndices)
            {
                Chunk chunk = _loadedChunkIndices[freeableChunkIndex];

                _loadedChunkIndices.Remove(freeableChunkIndex);

                chunk.QueueFree();
            }
        
            _freeableChunkIndices.Clear();
        }
        // Reposition Chunk instances from _freeableChunkIndices to _drawableChunkIndices.
        private void RecycleQueuedChunkIndices()
        {
            if (_freeableChunkIndices.Count == 0) return;
            if (_loadableChunkIndices.Count == 0) return;

            foreach (int freeableChunkIndex in _freeableChunkIndices)
            {
                int loadableChunkIndex = _loadableChunkIndices.Last();
                _loadableChunkIndices.Remove(loadableChunkIndex);
                
                Chunk chunk = _loadedChunkIndices[freeableChunkIndex];
                _loadedChunkIndices.Remove(freeableChunkIndex);
                _loadedChunkIndices.Add(loadableChunkIndex, chunk);

                Task recycle = new(new Action(() => chunk.CallDeferred(nameof(Chunk.GenerateAtIndex), loadableChunkIndex)));

                recycle.Start();
                recycle.Wait();

                Thread.Sleep(_updateFrequency);
            }
            
            _freeableChunkIndices.Clear();
        }

        #endregion Functions -> Chunks (Index Based)

        #region Functions -> Chunks (Position Based)

        // Queue, load, and free chunks to and from the scene tree position handles.
        private void GenerateChunksViaPositions()
        {   
            QueueDrawableChunkPositions();
            QueueLoadableChunkPositions();
            QueueFreeableChunkPositions();
                
            LoadQueuedChunkPositions();
            FreeQueuedChunkPositions();
        }
        // Reposition chunks from _freeableChunkPositions to _loadableChunkPositions.
        private void RecycleChunksViaPositions()
        {
            QueueDrawableChunkPositions();
            QueueLoadableChunkPositions();
            QueueFreeableChunkPositions();
            
            RecycleQueuedChunkPositions();
        }
        // Free all chunks from the scene tree using their position handles.
        private void FreeChunksViaPositions()
        {
            if (_loadedChunkPositions.Count == 0) return;

            foreach (Vector3I chunkPosition in _loadedChunkPositions.Keys)
            {
                Chunk chunk = _loadedChunkPositions[chunkPosition];
                
                _loadedChunkPositions.Remove(chunkPosition);
                
                chunk.QueueFree();
            }
        }
        
        
        // Add a Vector3I to its respective List for each drawable chunk position.
        private void QueueDrawableChunkPositions()
        {
            _drawableChunkPositions.Clear();

            Vector3I drawableMin = FocusNodeChunkPosition - _drawRadius;
            Vector3I drawableMax = FocusNodeChunkPosition + _drawRadius;
            
            for (int x = drawableMin.X; x <= drawableMax.X; x++)
            {
                for (int y = drawableMin.Y; y <= drawableMax.Y; y++)
                {
                    for (int z = drawableMin.Z; z <= drawableMax.Z; z++)
                    {
                        _drawableChunkPositions.Add(new(x, y, z));
                    }
                }
            }

            GD.PrintS("--> Drawable chunks:" + _drawableChunkPositions.Count);
        }
        // Add a Vector3I to its respective List for each loadable chunk position.
        private void QueueLoadableChunkPositions()
        {
            if (_drawableChunkPositions.Count == 0) return;
            
            _loadableChunkPositions.Clear();

            foreach (Vector3I chunkPosition in _drawableChunkPositions)
            {
                if (_loadedChunkPositions.ContainsKey(chunkPosition) == false)
                {
                    _loadableChunkPositions.Add(chunkPosition);
                }
            }

            GD.PrintS("--> Loadable chunks:" + _loadableChunkPositions.Count);
        }
        // Add a Vector3I to its respective List for each freeable chunk position.
        private void QueueFreeableChunkPositions()
        {
            if (_loadedChunkPositions.Count == 0) return;
            
            _freeableChunkPositions.Clear();

            foreach (Vector3I chunkPosition in _loadedChunkPositions.Keys)
            {
                if (_drawableChunkPositions.Contains(chunkPosition) == false)
                {
                    _freeableChunkPositions.Add(chunkPosition);
                }
            }

            GD.PrintS("--> Freeable chunks:" + _freeableChunkPositions.Count);
        }


        // Load a Chunk instance into the scene tree for each Vector3I in _loadableChunkPositions.
        private void LoadQueuedChunkPositions()
        {
            if (_loadableChunkPositions.Count == 0) return;
            
            foreach (Vector3I loadableChunkPosition in _loadableChunkPositions)
            {
                Chunk chunk = new(this, (Material)_terrainMaterial.Duplicate(true));
                
                _loadedChunkPositions.Add(loadableChunkPosition, chunk);
                
                CallDeferred(Node.MethodName.AddChild, chunk);

                Task generate = new(new Action(() => chunk.CallDeferred(nameof(Chunk.GenerateAtPosition), loadableChunkPosition)));

                generate.Start();
                generate.Wait();

                Thread.Sleep(_updateFrequency);
            }
        
            _loadableChunkPositions.Clear();
        }
        // Free a Chunk instance from the scene tree for each Vector3I in _freeableChunkPositions.
        private void FreeQueuedChunkPositions()
        {
            if (_freeableChunkPositions.Count == 0) return;

            foreach (Vector3I freeableChunkPosition in _freeableChunkPositions)
            {
                Chunk chunk = _loadedChunkPositions[freeableChunkPosition];
                
                _loadedChunkPositions.Remove(freeableChunkPosition);
                
                chunk.QueueFree();
            }
        
            _freeableChunkPositions.Clear();
        }
        // Reposition Chunk instances from _freeableChunkPositions to _drawableChunkPositions.
        private void RecycleQueuedChunkPositions()
        {
            if (_loadableChunkPositions.Count == 0) return;
            if (_freeableChunkPositions.Count == 0) return;

            foreach (Vector3I loadableChunkPosition in _loadableChunkPositions)
            {
                Vector3I freeableChunkPosition = _freeableChunkPositions.First();
                Chunk chunk = _loadedChunkPositions[freeableChunkPosition];
                
                _freeableChunkPositions.Remove(freeableChunkPosition);
                _loadedChunkPositions.Remove(freeableChunkPosition);
                _loadedChunkPositions.Add(loadableChunkPosition, chunk);

                Task recycle = new(new Action(() => chunk.CallDeferred(nameof(Chunk.GenerateAtPosition), loadableChunkPosition)));

                recycle.Start();
                recycle.Wait();

                Thread.Sleep(_updateFrequency);
            }

            _loadableChunkPositions.Clear();
        }

        #endregion Functions -> Chunks (Position Based)
    
        #region Functions -> Compute Shader

        public static void SetupComputeShader()
        {
            RenderingDevice renderingDevice = RenderingServer.CreateLocalRenderingDevice();
            
            Rid shader = Shaders.CreateComputeShader(renderingDevice, "res://addons/RawVoxel/resources/shaders/WorldCompute.glsl");

            // FIXME - not passing in any data yet.
            RDUniform storageBufferUniform = Shaders.CreateStorageBufferUniform(renderingDevice, 0, Array.Empty<byte>(), 0);
            
            Godot.Collections.Array<RDUniform> uniformArray = new() { storageBufferUniform };
            
            Rid uniformSet = renderingDevice.UniformSetCreate(uniformArray, shader, 0);
            
            Shaders.SetupComputePipeline(renderingDevice, shader, uniformSet, 1, 1, 1);
        }

        #endregion Functions -> Compute Shader
    }
}
