using Godot;
using System;
using RawUtils;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

// TODO - Thread Pool
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
            set { _regenerate = false; FreeChunks(); _chunksGenerated = false; }
        }
        private bool _regenerate = false;

        #endregion Exports -> Tools

        #region Exports -> Focus Node
        [Export] public Node3D FocusNode
        {
            get { return _focusNode; }
            set { _focusNode = value; TryUpdateFocusNodeGlobalPosition(); _chunksGenerated = false; }
        }
        private Node3D _focusNode;
        
        #endregion Exports -> Focus Node

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
            set { _drawRadius = value; UpdateDrawDiameter(); _chunksGenerated = false; }
        }
        private Vector3I _drawRadius = new(1, 1, 1);
        
        [Export] public Vector3I WorldRadius
        {
            get { return _worldRadius; }
            set { _worldRadius = value; UpdateWorldDiameter(); }
        }
        private Vector3I _worldRadius = new (128, 128, 128);
        
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
        
        [Export] public int UpdateFrequency
        {
            get { return _updateFrequency; }
            set { _updateFrequency = value; }
        }
        private int _updateFrequency = 10;

        #endregion Exports -> Threading

        
        #region Variables -> Focus Node
        
        private Vector3 _focusNodeGlobalPosition;
        private Vector3I _focusNodeChunkGridPosition = Vector3I.MinValue;
        private readonly object _focusNodePositionLock = new();
        
        #endregion Variables -> Focus Node

        #region Variables -> World
        
        public Vector3I DrawDiameter;
        public Vector3I WorldDiameter;
        
        #endregion Variables -> World

        #region Variables -> Chunks
        
        private bool _chunksGenerated;
        private readonly object _chunkGenerationLock = new();
        
        private readonly List<int> _drawableChunkGridIndices = new();
        private readonly List<int> _loadableChunkGridIndices = new();
        private readonly List<int> _freeableChunkGridIndices = new();
        private readonly Dictionary<int, Chunk> _loadedChunkGridIndices = new();

        #endregion Variables -> Chunks

        
        #region Functions -> Processes

        public override void _Ready()
        {
            GenerateChunks();
            
            Thread WorldThread = new(new ThreadStart(WorldProcess)) { Name = "World Thread" };
            WorldThread.Start();
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
        public override void _PhysicsProcess(double delta)
        {
            TryUpdateFocusNodeGlobalPosition();
        }
        private void WorldProcess()
        {
            while (IsInstanceValid(this))
            {
                if (_chunksGenerated == false)
                {
                    GenerateChunks();
                }
                
                else if (TryUpdateFocusNodeChunkGridPosition())
                {
                    WrapChunks();
                }
                
                Thread.Sleep(100);
            }
        }
        
        #endregion Functions -> Processes

        #region Functions -> Focus Node

        private void TryUpdateFocusNodeGlobalPosition()
        {
            if (_focusNode == null) return;
            
            // Lock access to _focusNodeGlobalPosition for primary thread.
            lock (_focusNodePositionLock)
            {
                // Copy _focusNode position to _focusNodeGlobalPosition so it can be checked accessed in other threads.
                _focusNodeGlobalPosition = _focusNode.Position;
            }
        }
        private bool TryUpdateFocusNodeChunkGridPosition()
        {
            if (_focusNode == null) return false;
            
            Vector3I queriedFocusNodeChunkPosition;
            
            // Lock acccess to _focusNodeGlobalPosition for secondary thread.
            lock (_focusNodePositionLock)
            {
                // Calculate queriedFocusNodeChunkPosition for the current frame.
                queriedFocusNodeChunkPosition = (Vector3I)(_focusNodeGlobalPosition / _chunkDimension).Floor();
            }
            
            // Check to see if _focusNodeGlobalPosition points to a different chunk. If true, update _focusNodeChunkGridPosition and return.
            if (_focusNodeChunkGridPosition != queriedFocusNodeChunkPosition)
            {
                _focusNodeChunkGridPosition = queriedFocusNodeChunkPosition;

                return true;
            }

            return false;
        }
        
        #endregion Functions -> Focus Node

        #region Functions -> World

        private void GenerateChunks()
        {   
            lock (_chunkGenerationLock)
            {
                TryUpdateFocusNodeChunkGridPosition();

                GD.PrintS("--- Generating World at:", _focusNodeChunkGridPosition, "---");

                QueueDrawableChunkGridIndices();
                QueueLoadableChunkGridIndices();
                QueueFreeableChunkGridIndices();

                LoadQueuedChunkGridIndices();
                FreeQueuedChunkGridIndices();
                
                GD.PrintS("--- World Generated at:", _focusNodeChunkGridPosition, "---");

                _chunksGenerated = true;
            }
        }
        private void WrapChunks()
        {   
            lock (_chunkGenerationLock)
            {
                GD.PrintS("--- Updating World at:", _focusNodeChunkGridPosition, "---");
                
                QueueDrawableChunkGridIndices();
                QueueLoadableChunkGridIndices();
                QueueFreeableChunkGridIndices();

                WrapQueuedChunkGridIndices();

                GD.PrintS("--- World Updated at:", _focusNodeChunkGridPosition, "---");
            }
        }
        private void FreeChunks()
        {
            if (_loadedChunkGridIndices.Count == 0) return;

            foreach (int loadedChunkGridIndex in _loadedChunkGridIndices.Keys)
            {
                Chunk chunk = _loadedChunkGridIndices[loadedChunkGridIndex];
                
                _loadedChunkGridIndices.Remove(loadedChunkGridIndex);
                
                chunk.QueueFree();
            }
        }

        private void UpdateWorldDiameter()
        {
            WorldDiameter.X = (_worldRadius.X << 1) + 1;
            WorldDiameter.Y = (_worldRadius.Y << 1) + 1;
            WorldDiameter.Z = (_worldRadius.Z << 1) + 1;
        }
        private void UpdateDrawDiameter()
        {
            DrawDiameter.X = (_drawRadius.X << 1) + 1;
            DrawDiameter.Y = (_drawRadius.Y << 1) + 1;
            DrawDiameter.Z = (_drawRadius.Z << 1) + 1;
        }

        #endregion Functions -> World

        #region Functions -> Chunks
        
        private void QueueDrawableChunkGridIndices()
        {
            _drawableChunkGridIndices.Clear();

            for (int x = 0; x < DrawDiameter.X; x ++)
            {
                for (int y = 0; y < DrawDiameter.Y; y ++)
                {
                    for (int z = 0; z < DrawDiameter.Z; z ++)
                    {
                        // Center chunk grid position to Origin then add focus node grid position.
                        Vector3I signedChunkGridPosition = new Vector3I(x, y, z) - _drawRadius + _focusNodeChunkGridPosition;
                        
                        // Offset back to unsigned chunk grid position using world radius.
                        Vector3I unsignedChunkGridPosition = signedChunkGridPosition + _worldRadius;
                        
                        // Convert unsigned chunk grid position back into an index.
                        int chunkGridIndex = XYZConvert.Vector3IToIndex(unsignedChunkGridPosition, WorldDiameter);
                        
                        // Off we go.
                        _drawableChunkGridIndices.Add(chunkGridIndex);
                    }
                }
            }

            GD.PrintS("--> Drawable chunks:", _drawableChunkGridIndices.Count);
        }
        private void QueueLoadableChunkGridIndices()
        {
            if (_drawableChunkGridIndices.Count == 0) return;
            
            _loadableChunkGridIndices.Clear();

            foreach (int drawableChunkGridIndex in _drawableChunkGridIndices)
            {
                if (_loadedChunkGridIndices.ContainsKey(drawableChunkGridIndex) == false)
                {
                    _loadableChunkGridIndices.Add(drawableChunkGridIndex);
                }
            }

            GD.PrintS("--> Loadable chunks:", _loadableChunkGridIndices.Count);
        }
        private void QueueFreeableChunkGridIndices()
        {
            if (_loadedChunkGridIndices.Count == 0) return;
            
            _freeableChunkGridIndices.Clear();

            foreach (int loadedChunkGridIndex in _loadedChunkGridIndices.Keys)
            {
                if (_drawableChunkGridIndices.Contains(loadedChunkGridIndex) == false)
                {
                    _freeableChunkGridIndices.Add(loadedChunkGridIndex);
                }
            }

            GD.PrintS("--> Freeable chunks:", _freeableChunkGridIndices.Count);
        }

        private void LoadQueuedChunkGridIndices()
        {
            if (_loadableChunkGridIndices.Count == 0) return;

            foreach (int loadableChunkGridIndex in _loadableChunkGridIndices)
            {
                Chunk chunk = new(this, _terrainMaterial.Duplicate(true) as Material);
                
                _loadedChunkGridIndices.Add(loadableChunkGridIndex, chunk);
                
                CallDeferred(Node.MethodName.AddChild, chunk);

                Task generate = new(new Action(() => chunk.CallDeferred(nameof(Chunk.Generate), loadableChunkGridIndex)));

                generate.Start();
                generate.Wait();

                Thread.Sleep(_updateFrequency);
            }
        
            _loadableChunkGridIndices.Clear();
        }
        private void FreeQueuedChunkGridIndices()
        {
            if (_freeableChunkGridIndices.Count == 0) return;

            foreach (int freeableChunkGridIndex in _freeableChunkGridIndices)
            {
                Chunk chunk = _loadedChunkGridIndices[freeableChunkGridIndex];

                _loadedChunkGridIndices.Remove(freeableChunkGridIndex);

                chunk.QueueFree();
            }
        
            _freeableChunkGridIndices.Clear();
        }
        private void WrapQueuedChunkGridIndices()
        {
            if (_freeableChunkGridIndices.Count == 0) return;
            if (_loadableChunkGridIndices.Count == 0) return;

            foreach (int freeableChunkGridIndex in _freeableChunkGridIndices)
            {
                int loadableChunkGridIndex = _loadableChunkGridIndices.First();
                
                _loadableChunkGridIndices.Remove(loadableChunkGridIndex);
                
                Chunk chunk = _loadedChunkGridIndices[freeableChunkGridIndex];
                
                _loadedChunkGridIndices.Remove(freeableChunkGridIndex);
                _loadedChunkGridIndices.Add(loadableChunkGridIndex, chunk);

                Task recycle = new(new Action(() => chunk.CallDeferred(nameof(Chunk.Generate), loadableChunkGridIndex)));

                recycle.Start();
                recycle.Wait();

                Thread.Sleep(_updateFrequency);
            }
            
            _freeableChunkGridIndices.Clear();
        }

        #endregion Functions -> Chunks
    }
}
