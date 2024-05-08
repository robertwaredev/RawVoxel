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
    public partial class ChunkManager : Node3D
    {
        #region Exports

        [Export] public bool Regenerate
        {
            get { return _regenerate; }
            set { _regenerate = false; FreeLoadedGridIndices(); WorldGenerated = false; }
        }
        private bool _regenerate = false;

        [Export] private World _world { get; set; } = new();

        [Export] public Node3D FocusNode
        {
            get { return _focusNode; }
            set { _focusNode = value; WorldGenerated = false; }
        }
        private Node3D _focusNode;

        #endregion Exports

        #region Variables
        
        // Focus Node
        private readonly object _focusNodePositionLock = new();
        private Vector3 _focusNodeGlobalPosition;
        private Vector3I _focusNodeGridPosition = Vector3I.MinValue;
        
        // World
        public bool WorldGenerated;
        public Vector3I DrawDiameter;
        public Vector3I WorldDiameter;
        
        // Index queues.
        private readonly object _queueLock = new();
        private readonly List<int> _drawableIndices = new();
        private readonly List<int> _loadableIndices = new();
        private readonly List<int> _freeableIndices = new();
        private readonly Dictionary<int, Chunk> _loadedIndices = new();

        #endregion Variables
        
        public ChunkManager() {}
        
        // Processes
        public override void _Ready()
        {
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
            // Run while world instance is valid.
            while (IsInstanceValid(this))
            {
                if (WorldGenerated == false)
                {
                    lock (_queueLock)
                    {
                        TryUpdateFocusNodeGridPosition();
                        GenerateWorld();
                    }
                }
                else if (TryUpdateFocusNodeGridPosition())
                {
                    lock (_queueLock)
                    {
                        WrapWorld();
                    }
                }
                
                Thread.Sleep(100);
            }
        }

        // Focus Node
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
        private bool TryUpdateFocusNodeGridPosition()
        {
            if (_focusNode == null) return false;
            
            Vector3I queriedFocusNodeChunkPosition;
            
            // Lock acccess to _focusNodeGlobalPosition for secondary thread.
            lock (_focusNodePositionLock)
            {
                // Calculate queriedFocusNodeChunkPosition for the current frame.
                queriedFocusNodeChunkPosition = (Vector3I)(_focusNodeGlobalPosition / _world.ChunkDiameter).Floor();
            }
            
            // Check to see if _focusNodeGlobalPosition points to a different chunk. If true, update _focusNodeGridPosition and return.
            if (_focusNodeGridPosition != queriedFocusNodeChunkPosition)
            {
                _focusNodeGridPosition = queriedFocusNodeChunkPosition;

                return true;
            }

            return false;
        }

        // World dimensions
        private void UpdateWorldDiameter()
        {
            WorldDiameter.X = (_world.Radius.X << 1) + 1;
            WorldDiameter.Y = (_world.Radius.Y << 1) + 1;
            WorldDiameter.Z = (_world.Radius.Z << 1) + 1;
        }
        private void UpdateDrawDiameter()
        {
            DrawDiameter.X = (_world.Draw.X << 1) + 1;
            DrawDiameter.Y = (_world.Draw.Y << 1) + 1;
            DrawDiameter.Z = (_world.Draw.Z << 1) + 1;
        }
        
        // World Generation
        private void GenerateWorld()
        {   
            UpdateWorldDiameter();
            UpdateDrawDiameter();

            QueueDrawableIndices();
            QueueLoadableIndices();
            QueueFreeableIndices();

            LoadQueuedIndices();
            FreeQueuedIndices();

            WorldGenerated = true;
        }
        private void WrapWorld()
        {   
            QueueDrawableIndices();
            QueueLoadableIndices();
            QueueFreeableIndices();

            WrapQueuedIndices();
        }
        
        // Index queueing.
        private void QueueDrawableIndices()
        {
            // Clear any existing drawable indices.
            _drawableIndices.Clear();
            
            // Clamp draw diameter to not exceed world diameter.
            DrawDiameter.X = Mathf.Clamp(DrawDiameter.X, 0, WorldDiameter.X);
            DrawDiameter.Y = Mathf.Clamp(DrawDiameter.Y, 0, WorldDiameter.Y);
            DrawDiameter.Z = Mathf.Clamp(DrawDiameter.Z, 0, WorldDiameter.Z);
            
            // Queue drawable indices.
            for (int x = 0; x < DrawDiameter.X; x ++)
            {
                for (int y = 0; y < DrawDiameter.Y; y ++)
                {
                    for (int z = 0; z < DrawDiameter.Z; z ++)
                    {
                        // Center position relative to Origin, then add focus node position.
                        Vector3I signedPosition = new Vector3I(x, y, z) - _world.Draw + _focusNodeGridPosition;
                        
                        // Offset back to unsigned position using world radius.
                        Vector3I unsignedPosition = signedPosition + _world.Radius;
                        
                        // Convert unsigned position back into an index.
                        int gridIndex = XYZConvert.Vector3IToIndex(unsignedPosition, WorldDiameter);
                        
                        // Off we go.
                        _drawableIndices.Add(gridIndex);
                    }
                }
            }

            GD.PrintS("--> Drawable chunks:", _drawableIndices.Count);
        }
        private void QueueLoadableIndices()
        {
            // Return if there's no drawable chunks to load.
            if (_drawableIndices.Count == 0) return;
            
            // Clear any existing loadable indices.
            _loadableIndices.Clear();

            // Queue loadable indices.
            foreach (int drawableIndex in _drawableIndices)
            {
                if (_loadedIndices.ContainsKey(drawableIndex) == false)
                {
                    _loadableIndices.Add(drawableIndex);
                }
            }

            GD.PrintS("--> Loadable chunks:", _loadableIndices.Count);
        }
        private void QueueFreeableIndices()
        {
            // Return if there's no loaded chunks to free.
            if (_loadedIndices.Count == 0) return;
            
            // Clear any existing freeable indices.
            _freeableIndices.Clear();

            // Queue freeable indices.
            foreach (int loadedIndex in _loadedIndices.Keys)
            {
                if (_drawableIndices.Contains(loadedIndex) == false)
                {
                    _freeableIndices.Add(loadedIndex);
                }
            }

            GD.PrintS("--> Freeable chunks:", _freeableIndices.Count);
        }

        // Load, free, & wrap queued indices.
        private void LoadQueuedIndices()
        {
            // Prevent loading chunks with no loadable indices.
            if (_loadableIndices.Count == 0) return;

            // Load queued indices.
            foreach (int loadableIndex in _loadableIndices)
            {
                // Create a new chunk instance.
                Chunk chunk = new(_world);
                _loadedIndices.Add(loadableIndex, chunk);
                
                // Convert chunk index to position.
                Vector3I loadablePosition = XYZConvert.IndexToVector3I(loadableIndex, WorldDiameter) - _world.Radius;
                
                // Create a new task to generate the chunk during idle time.
                Task generate = new(new Action(() => chunk.CallDeferred(nameof(Chunk.GenerateChunkData), loadablePosition)));

                generate.Start();
                generate.Wait();
                
                // Load chunk into scene during idle time.
                CallDeferred(Node.MethodName.AddChild, chunk);

                // Sleep a short period. (~15ms)
                Thread.Sleep(_world.GenerateFrequency);
            }
        
            _loadableIndices.Clear();
        }
        private void FreeQueuedIndices()
        {
            // Prevent freeing chunks with no freeable chunks.
            if (_freeableIndices.Count == 0) return;

            // Free queued indices.
            foreach (int freeableIndex in _freeableIndices)
            {
                // Extract loaded chunk from _loadedIndices.
                Chunk chunk = _loadedIndices[freeableIndex];

                // Remove extracted chunk from _loadedIndices.
                _loadedIndices.Remove(freeableIndex);

                // Free the chunk.
                chunk.QueueFree();
            }
        
            // Clean up.
            _freeableIndices.Clear();
        }
        private void WrapQueuedIndices()
        {
            // Prevent wrapping with no freeable or loadable indices.
            if (_freeableIndices.Count == 0) return;
            if (_loadableIndices.Count == 0) return;

            // Wrap queued indices.
            foreach (int freeableIndex in _freeableIndices)
            {
                // Extract first loadable index.
                int loadableIndex = _loadableIndices.First();
                _loadableIndices.Remove(loadableIndex);
                
                // Extract chunk from its position in _loadedIndices.
                Chunk chunk = _loadedIndices[freeableIndex];
                _loadedIndices.Remove(freeableIndex);
                _loadedIndices.Add(loadableIndex, chunk);

                // Convert index to position.
                Vector3I loadablePosition = XYZConvert.IndexToVector3I(loadableIndex, WorldDiameter) - _world.Radius;
                
                // Create a new task to generate the chunk during idle time.
                Task generate = new(new Action(() => chunk.CallDeferred(nameof(Chunk.GenerateChunkData), loadablePosition)));

                generate.Start();
                generate.Wait();

                // Sleep a short period. (~15ms)
                Thread.Sleep(_world.GenerateFrequency);
            }
            
            _freeableIndices.Clear();
        }
    
        // Queue free all loaded indices.
        private void FreeLoadedGridIndices()
        {
            // Return if there are no loaded indices.
            if (_loadedIndices.Count == 0) return;

            // Queue free all chunk in the scene tree.
            foreach (int loadedIndex in _loadedIndices.Keys)
            {
                Chunk chunk = _loadedIndices[loadedIndex];
                
                _loadedIndices.Remove(loadedIndex);
                
                chunk.QueueFree();
            }
        }
    }
}
