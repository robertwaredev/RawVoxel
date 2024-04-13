using Godot;
using System;
using RawUtils;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

namespace RAWVoxel
{
    [Tool]
    public partial class World : Node3D
    {
        #region Constructor

        public World() {}

        #endregion Constructor
        
        #region Exports -> Tools

        [Export] public bool Regenerate
        {
            get { return _regenerate; }
            set { _regenerate = false; GenerateWorld(); }
        }
        private bool _regenerate = false;

        #endregion Exports -> Tools

        #region Exports -> FocusNode

        [ExportCategory("Focus Node")]
        
        [Export] public Node3D FocusNode
        {
            get { return _focusNode; }
            set { _focusNode = value; GenerateWorld(); }
        }
        private Node3D _focusNode;
        
        #endregion Exports -> FocusNode

        #region Exports -> Rendering
        [ExportGroup("Rendering")]
        
        [Export] public Vector3I DrawDistance
        {
            get { return _drawDistance; }
            set { _drawDistance = value; }
        }
        private Vector3I _drawDistance = Vector3I.Zero;
        
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

        #region Exports -> World
        [ExportGroup("World Settings")]
        
        [Export] public Vector3I WorldDimension
        {
            get { return _worldDimension; }
            set { _worldDimension = value; }
        }
        private Vector3I _worldDimension = new (128, 1, 128);
        
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
        
        #endregion Exports -> World

        #region Exports -> Terrain Material
        [ExportGroup("Terrain Material")]

        [Export] public Material TerrainMaterial
        {
            get { return _terrainMaterial; }
            set { _terrainMaterial = value; }
        }
        private Material _terrainMaterial = GD.Load<Material>("res://addons/RawVoxel/resources/materials/chunk_material.tres");

        #endregion Exports -> Terrain Material
        
        #region Exports -> Terrain Height
        [ExportGroup("Terrain Height")]
        
        [Export] public int SurfaceHeight
        {
            get { return surfaceHeight; }
            set { surfaceHeight = value; }
        }
        private int surfaceHeight = 128;
        
        [Export] public int Layer2Height
        {
            get { return layer2Height; }
            set { layer2Height = value; }
        }
        private int layer2Height = 96;
        
        [Export] public int Layer1Height
        {
            get { return layer1Height; }
            set { layer1Height = value; }
        }
        private int layer1Height = 64;
        
        [Export] public int BedrockHeight
        {
            get { return bedrockHeight; }
            set { bedrockHeight = value; }
        }
        private int bedrockHeight = 0;

        #endregion Exports -> Terrain Height

        #region Exports -> Terrain Noise
        [ExportGroup("Terrain Noise")]
        
        // Noise map used for density generation. Controls y value for terrain generation.
        [Export] public FastNoiseLite DensityNoise
        {
            get { return _densityNoise; }
            set { _densityNoise = value; }
        }
        private FastNoiseLite _densityNoise = GD.Load<FastNoiseLite>("res://addons/RawVoxel/resources/world/density_noise.tres");
        
        // Noise map used for height generation. Controls y value for terrain generation.
        [Export] public FastNoiseLite SurfaceNoise
        {
            get { return _surfaceNoise; }
            set { _surfaceNoise = value; }
        }
        private FastNoiseLite _surfaceNoise = GD.Load<FastNoiseLite>("res://addons/RawVoxel/resources/world/surface_noise.tres");
        
        // Noise map used for humidity generation. Controls x value for terrain generation.
        [Export] public FastNoiseLite HumidityNoise
        {
            get { return _humidityNoise; }
            set { _humidityNoise = value; }
        }
        private FastNoiseLite _humidityNoise = GD.Load<FastNoiseLite>("res://addons/RawVoxel/resources/world/humidity_noise.tres");
        
        // Noise map used for temperature generation. Controls z value for terrain generation.
        [Export] public FastNoiseLite TemperatureNoise
        {
            get { return _temperatureNoise; }
            set { _temperatureNoise = value; }
        }
        private FastNoiseLite _temperatureNoise = GD.Load<FastNoiseLite>("res://addons/RawVoxel/resources/world/temperature_noise.tres");

        #endregion Exports -> Terrain Noise
        
        #region Exports -> Terrain Curves
        [ExportGroup("Terrain Curves")]
        // Controls density distribution.
        [Export] public Curve DensityCurve
        {
            get { return _densityCurve; }
            set { _densityCurve = value; }
        }
        private Curve _densityCurve = GD.Load<Curve>("res://addons/RawVoxel/resources/world/density_curve.tres");
        // Controls surface distribution.
        [Export] public Curve SurfaceCurve
        {
            get { return _surfaceCurve; }
            set { _surfaceCurve = value; }
        }
        private Curve _surfaceCurve = GD.Load<Curve>("res://addons/RawVoxel/resources/world/surface_curve.tres");
        // Controls humidity distribution.
        [Export] public Curve HumidityCurve
        {
            get { return _humidityCurve; }
            set { _humidityCurve = value; }
        }
        private Curve _humidityCurve = GD.Load<Curve>("res://addons/RawVoxel/resources/world/humidity_curve.tres");
        // Controls temperature distribution.
        [Export] public Curve TemperatureCurve
        {
            get { return _temperatureCurve; }
            set { _temperatureCurve = value; }
        }
        private Curve _temperatureCurve = GD.Load<Curve>("res://addons/RawVoxel/resources/world/temperature_curve.tres");

        #endregion Exports -> Terrain Curves

        #region Variables -> FocusNode
        
        private Vector3 _focusNodePosition;
        private Vector3I _focusNodeChunkPosition = Vector3I.MinValue;
        private readonly object _focusNodeChunkPositionLock = new();
        
        #endregion Variables -> FocusNode

        #region Variables -> World
        
        private readonly object _worldGenerationLock = new();
        
        #endregion Variables -> World

        #region Variables -> Queues

        private bool _worldGenerated = false;
        private readonly List<Vector3I> _drawableChunkPositions = new();
        private readonly List<Vector3I> _loadableChunkPositions = new();
        private readonly List<Vector3I> _freeableChunkPositions = new();
        private readonly Dictionary<Vector3I, Chunk> _loadedChunks = new();

        #endregion Variables -> Queues

        #region Functions -> Processes

        public override void _Ready()
        {
            GenerateWorld();
            
            if (_threading)
            {
                ThreadStart UpdateWorldProcessStart = new(UpdateWorldProcess);
                Thread UpdateWorldThread = new(UpdateWorldProcessStart)
                {
                    Name = "UpdateWorldThread"
                };
                UpdateWorldThread.Start();
            }
        }
        public override void _PhysicsProcess(double delta)
        {
            if (_worldGenerated) UpdateFocusNodePosition();
            
            if (_threading) return;
            
            if (TryUpdateFocusNodeChunkPosition())
            {
                UpdateWorld();
            }
        }
        private void UpdateWorldProcess()
        {
            while (IsInstanceValid(this) && _worldGenerated)
            {
                if (TryUpdateFocusNodeChunkPosition())
                {
                    UpdateWorld();
                }
                
                Thread.Sleep(100);
            }
        }
        
        #endregion Functions -> Processes

        #region Functions -> FocusNode

        // Set _focusNodeChunkPosition to _focusNode.Position. Locked for thread access.
        private void UpdateFocusNodePosition()
        {
            // Lock for primary thread access.
            lock (_focusNodeChunkPositionLock)
            {
                _focusNodePosition = _focusNode.Position;
            }
        }
        // Set _focusNodeChunkPosition when _focusNode enters a new chunk. Locked for thread access.
        private bool TryUpdateFocusNodeChunkPosition()
        {
            Vector3I queriedFocusNodeChunkPosition;
            
            // Lock for secondary thread access.
            lock (_focusNodeChunkPositionLock)
            {
                // Calculate queriedFocusNodeChunkPosition for the current frame.
                queriedFocusNodeChunkPosition = (Vector3I)(_focusNodePosition / _chunkDimension).Floor();
            }
            
            // Check to see if _focusNode has a new chunk position. If true, update _focusNodeChunkPosition and return.
            if (_focusNodeChunkPosition != queriedFocusNodeChunkPosition)
            {
                _focusNodeChunkPosition = queriedFocusNodeChunkPosition;
                
                GD.Print(_focusNode.Name + " chunk position updated: " + _focusNodeChunkPosition.ToString());
                Console.WriteLine(_focusNode.Name + " chunk position updated: " + _focusNodeChunkPosition.ToString());
                
                return true;
            }

            return false;
        }
        
        #endregion Functions -> FocusNode

        #region Functions -> Generate & Update

        // Queue, load, and free chunks to and from the scene tree.
        private void GenerateWorld()
        {   
            // Ensure that this can't be called again while it's still running.
            // Ensure that this can't be called if UpdateWorld() is running.
            // This is to prevent crashes when making changes in the inspector that might cause overlap.
            lock (_worldGenerationLock)
            {
                _worldGenerated = false;
            
                UpdateFocusNodePosition();
                TryUpdateFocusNodeChunkPosition();

                QueueChunkPositions();
                LoadQueuedChunks();
                FreeQueuedChunks();
                
                _worldGenerated = true;
            }
        }
        // Reposition chunks at _freeableChunkPositions to _loadableChunkPositions.
        private void UpdateWorld()
        {
            // Ensure that this can't be called again while it's still running.
            // Ensure that this can't be called if GenerateWorld() is running.
            // This is to prevent crashes when making changes in the inspector that might cause overlap.
            lock (_worldGenerationLock)
            {
                QueueChunkPositions();
                RawTimer.Time(RecycleQueuedChunks, RawTimer.AppendLine.Both);
            }
        }
        
        #endregion Functions -> Generate & Update

        #region Functions -> Queues

        // Call all chunk position queueing methods. Called by GenerateWorld() and UpdateWorld().
        private void QueueChunkPositions()
        {
            QueueDrawableChunkPositions();
            QueueLoadableChunkPositions();
            QueueFreeableChunkPositions();
        }
        // Queue a new Vector3I into its respective List for each drawable chunk position. Called by QueueChunkPositions().
        private void QueueDrawableChunkPositions()
        {
            _drawableChunkPositions.Clear();

            Vector3I drawableCenter = _focusNodeChunkPosition;
            
            int drawableXMin = drawableCenter.X - _drawDistance.X;
            int drawableXMax = drawableCenter.X + _drawDistance.X;
            
            int drawableYMin = drawableCenter.Y - _drawDistance.Y;
            int drawableYMax = drawableCenter.Y + _drawDistance.Y;

            int drawableZMin = drawableCenter.Z - _drawDistance.Z;
            int drawableZMax = drawableCenter.Z + _drawDistance.Z;
            
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

            GD.Print("Drawable chunks: " + _drawableChunkPositions.Count);
        }
        // Queue a new Vector3I into its respective List for each loadable chunk position. Called by QueueChunkPositions().
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

            GD.Print("Loadable chunks: " + _loadableChunkPositions.Count);
        }
        // Queue a new Vector3I into its respective List for each freeable chunk position. Called by QueueChunkPositions().
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

            GD.Print("Freeable chunks: " + _loadableChunkPositions.Count);
        }

        // Load a Chunk instance into the scene tree for each Vector3I in _loadableChunkPositions. Called by GenerateWorld().
        private void LoadQueuedChunks()
        {
            if (_loadableChunkPositions.Count == 0) return;
            
            foreach (Vector3I chunkPosition in _loadableChunkPositions)
            {
                Chunk chunk = new(chunkPosition, this, _terrainMaterial);
                
                _loadedChunks.Add(chunkPosition, chunk);
                
                CallDeferred(Node.MethodName.AddChild, chunk);
                chunk.CallDeferred(nameof(Chunk.GenerateChunk));
            }
        
            _loadableChunkPositions.Clear();
        }
        // Free a Chunk instance from the scene tree for each Vector3I in _freeableChunkPositions. Called by GenerateWorld().
        private void FreeQueuedChunks()
        {
            if (_freeableChunkPositions.Count == 0) return;

            foreach (Vector3I chunkPosition in _freeableChunkPositions)
            {
                Chunk chunk = _loadedChunks[chunkPosition];
                
                _loadedChunks.Remove(chunkPosition);
                
                chunk.QueueFree();
            }
        
            _freeableChunkPositions.Clear();
        }
        // Reposition Chunk instances from _freeableChunkPositions to _drawableChunkPositions. Called by UpdateWorld().
        private void RecycleQueuedChunks()
        {
            if (_freeableChunkPositions.Count == 0) return;
            if (_loadableChunkPositions.Count == 0) return;

            foreach (Vector3I loadableChunkPosition in _loadableChunkPositions)
            {
                Vector3I freeableChunkPosition = _freeableChunkPositions.First();
                Chunk chunk = _loadedChunks[freeableChunkPosition];
                
                _freeableChunkPositions.Remove(freeableChunkPosition);
                _loadedChunks.Remove(freeableChunkPosition);
                _loadedChunks.Add(loadableChunkPosition, chunk);

                chunk.CallDeferred(nameof(Chunk.UpdateChunk), loadableChunkPosition);

                Thread.Sleep(_updateFrequency);
            }

            _loadableChunkPositions.Clear();
        }

        #endregion Functions -> Queues
    }
}
