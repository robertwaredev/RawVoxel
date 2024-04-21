using Godot;
using System;
using RawUtils;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

// TODO - Thread Pool
// TODO - Convert Update and Generate to threads.
// TODO - Fix chunk loading to always load chunks closest to focus node at surface level first.

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class World : Node3D
    {
        #region Exports -> Tools

        [Export] public bool Regenerate
        {
            get { return _regenerate; }
            set { _regenerate = false; Clear(); Generate(); }
        }
        private bool _regenerate = false;

        #endregion Exports -> Tools

        #region Exports -> FocusNode
        [Export] public Node3D FocusNode
        {
            get { return _focusNode; }
            set { _focusNode = value; Generate(); }
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
        
        [Export] public bool Threading
        {
            get { return _threading; }
            set { _threading = value; }
        }
        private bool _threading = true;
        
        [Export] public int updateFrequency
        {
            get { return _updateFrequency; }
            set { _updateFrequency = value; }
        }
        private int _updateFrequency = 100;

        #endregion Exports -> Threading

        
        #region Variables -> FocusNode
        
        private Vector3 _focusNodePosition;
        private Vector3I _focusNodeChunkPosition = Vector3I.MinValue;
        private readonly object _focusNodeLock = new();
        
        #endregion Variables -> FocusNode

        #region Variables -> World
        
        private bool _worldGenerated = false;
        private readonly object _worldGenerationLock = new();
        
        #endregion Variables -> World

        #region Variables -> Chunks

        private readonly List<Vector3I> _drawableChunkPositions = new();
        private readonly List<Vector3I> _loadableChunkPositions = new();
        private readonly List<Vector3I> _freeableChunkPositions = new();
        private readonly Dictionary<Vector3I, Chunk> _loadedChunks = new();

        #endregion Variables -> Chunks

        
        #region Functions -> Ready

        public override void _Ready()
        {
            if (_threading && _worldGenerated)
            {
                ThreadStart UpdateWorldProcessStart = new(UpdateWorldProcess);
                Thread UpdateWorldThread = new(UpdateWorldProcessStart) { Name = "UpdateWorldThread" };
                
                UpdateWorldThread.Start();
            }
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

        // Regularly call TryUpdateFocusNodePosition() on the main thread.
        public override void _PhysicsProcess(double delta)
        {
            if (_worldGenerated) TryUpdateFocusNodePosition();
            
            if (_threading) return;
            
            if (TryUpdateFocusNodeChunkPosition())
            {
                Update();
            }
        }
        // Regularly call TryUpdateFocusNodeChunkPosition() on the second thread and if call Update() if successful.
        private void UpdateWorldProcess()
        {
            while (IsInstanceValid(this))
            {
                if (TryUpdateFocusNodeChunkPosition())
                {
                    Update();
                }
                
                Thread.Sleep(100);
            }
        }
        
        #endregion Functions -> Processes

        #region Functions -> FocusNode

        // Set _focusNodeChunkPosition to _focusNode.Position. Locked for thread access.
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
        // Set _focusNodeChunkPosition when _focusNodePosition points to a different chunk. Locked for thread access.
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
            
            // Check to see if _focusNodePosition points to a different chunk. If true, update _focusNodeChunkPosition and return.
            if (_focusNodeChunkPosition != queriedFocusNodeChunkPosition)
            {
                _focusNodeChunkPosition = queriedFocusNodeChunkPosition;

                return true;
            }

            return false;
        }
        
        #endregion Functions -> FocusNode

        #region Functions -> World

        // Free all chunks from the scene tree.
        private void Clear()
        {
            if (_loadedChunks.Count == 0) return;

            foreach (Vector3I chunkPosition in _loadedChunks.Keys)
            {
                Chunk chunk = _loadedChunks[chunkPosition];
                
                _loadedChunks.Remove(chunkPosition);
                
                chunk.QueueFree();
            }
        }
        // Queue, load, and free chunks to and from the scene tree.
        private void Generate()
        {   
            // Ensure that this can't be called if Update() is running.
            // Ensure that this can't be called again while it's still running.
            // This is to prevent crashes when making changes in the inspector that might cause overlap.
            lock (_worldGenerationLock)
            {
                _worldGenerated = false;
            
                TryUpdateFocusNodePosition();
                TryUpdateFocusNodeChunkPosition();

                GD.PrintS("--- Generating World at:", _focusNodeChunkPosition, "---");

                QueueDrawableChunkPositions();
                QueueLoadableChunkPositions();
                QueueFreeableChunkPositions();
                
                LoadQueuedChunks();
                FreeQueuedChunks();
                
                GD.PrintS("--- World Generated at:", _focusNodeChunkPosition, "---");
                GD.Print(" ");

                _worldGenerated = true;
            }
        }
        // Reposition chunks from _freeableChunkPositions to _loadableChunkPositions.
        private void Update()
        {
            // Ensure that this can't be called if Generate() is running.
            // Ensure that this can't be called again while it's still running.
            // This is to prevent crashes when making changes in the inspector that might cause overlap.
            lock (_worldGenerationLock)
            {
                GD.PrintS("--- Updating World at:", _focusNodeChunkPosition, "---");

                QueueDrawableChunkPositions();
                QueueLoadableChunkPositions();
                QueueFreeableChunkPositions();
                
                RecycleQueuedChunks();
                
                GD.PrintS("--- World Updated at:", _focusNodeChunkPosition, "---");
                GD.Print(" ");
            }
        }
        
        #endregion Functions -> World

        #region Functions -> Chunks

        // Add a new Vector3I to its respective List for each drawable chunk position.
        private void QueueDrawableChunkPositions()
        {
            _drawableChunkPositions.Clear();

            Vector3I drawableCenter = _focusNodeChunkPosition;
            
            int drawableXMin = drawableCenter.X - _drawRadius.X;
            int drawableXMax = drawableCenter.X + _drawRadius.X;
            
            int drawableYMin = drawableCenter.Y - _drawRadius.Y;
            int drawableYMax = drawableCenter.Y + _drawRadius.Y;

            int drawableZMin = drawableCenter.Z - _drawRadius.Z;
            int drawableZMax = drawableCenter.Z + _drawRadius.Z;
            
            for (int x = drawableXMin; x <= drawableXMax; x++)
            {
                for (int y = drawableYMin; y <= drawableYMax; y++)
                {
                    for (int z = drawableZMin; z <= drawableZMax; z++)
                    {
                        _drawableChunkPositions.Add(new(x, y, z));
                    }
                }
            }

            GD.PrintS("--> Drawable chunks:" + _drawableChunkPositions.Count);
        }
        // Add a new Vector3I to its respective List for each loadable chunk position.
        private void QueueLoadableChunkPositions()
        {
            if (_drawableChunkPositions.Count == 0) return;
            
            _loadableChunkPositions.Clear();

            foreach (Vector3I chunkPosition in _drawableChunkPositions)
            {
                if (_loadedChunks.ContainsKey(chunkPosition) == false)
                {
                    _loadableChunkPositions.Add(chunkPosition);
                }
            }

            GD.PrintS("--> Loadable chunks:" + _loadableChunkPositions.Count);
        }
        // Add a new Vector3I to its respective List for each freeable chunk position.
        private void QueueFreeableChunkPositions()
        {
            if (_loadedChunks.Count == 0) return;
            
            _freeableChunkPositions.Clear();

            foreach (Vector3I chunkPosition in _loadedChunks.Keys)
            {
                if (_drawableChunkPositions.Contains(chunkPosition) == false)
                {
                    _freeableChunkPositions.Add(chunkPosition);
                }
            }

            GD.PrintS("--> Freeable chunks:" + _freeableChunkPositions.Count);
        }


        // Load a Chunk instance into the scene tree for each Vector3I in _loadableChunkPositions.
        private void LoadQueuedChunks()
        {
            if (_loadableChunkPositions.Count == 0) return;
            
            foreach (Vector3I loadableChunkPosition in _loadableChunkPositions)
            {
                //Chunk chunk = new(this, GD.Load<Material>(_terrainMaterial.ResourcePath));
                Chunk chunk = new(this, (Material)_terrainMaterial.Duplicate(true));
                
                _loadedChunks.Add(loadableChunkPosition, chunk);
                
                CallDeferred(Node.MethodName.AddChild, chunk);

                Task generate = new(new Action(() => chunk.CallDeferred(nameof(Chunk.Generate), loadableChunkPosition)));

                generate.Start();
                generate.Wait();
            }
        
            _loadableChunkPositions.Clear();
        }
        // Free a Chunk instance from the scene tree for each Vector3I in _freeableChunkPositions.
        private void FreeQueuedChunks()
        {
            if (_freeableChunkPositions.Count == 0) return;

            foreach (Vector3I freeableChunkPosition in _freeableChunkPositions)
            {
                Chunk chunk = _loadedChunks[freeableChunkPosition];
                
                _loadedChunks.Remove(freeableChunkPosition);
                
                chunk.QueueFree();
            }
        
            _freeableChunkPositions.Clear();
        }
        // Reposition Chunk instances from _freeableChunkPositions to _drawableChunkPositions.
        private void RecycleQueuedChunks()
        {
            if (_loadableChunkPositions.Count == 0) return;
            if (_freeableChunkPositions.Count == 0) return;

            //ThreadPool.SetMaxThreads(2, 2);

            foreach (Vector3I loadableChunkPosition in _loadableChunkPositions)
            {
                Vector3I freeableChunkPosition = _freeableChunkPositions.First();
                Chunk chunk = _loadedChunks[freeableChunkPosition];
                
                _freeableChunkPositions.Remove(freeableChunkPosition);
                
                _loadedChunks.Remove(freeableChunkPosition);
                _loadedChunks.Add(loadableChunkPosition, chunk);

                Action action = new(() => chunk.CallDeferred(nameof(Chunk.Generate), loadableChunkPosition));
                Task task = new(action);

                task.Start();
                task.Wait();

                Thread.Sleep(_updateFrequency);
            }

            _loadableChunkPositions.Clear();
        }

        #endregion Functions -> Chunks
    }
}
