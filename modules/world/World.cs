using Godot;
using System;
using RawUtils;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RawVoxel
{
    [Tool]
    public partial class World() : Node3D
    {
        #region Exports

        [Export] public bool Preload = true;
        [Export] public bool Generated = false;

        [Export] public Node3D FocusNode { get; set; }
        [Export] public WorldSettings WorldSettings { get; set; }

        #endregion Exports
        
        #region Variables

        private Vector3 _focusNodeWorldPosition;
        private Vector3I _focusNodeChunkPosition = Vector3I.MinValue;
        private readonly object _focusNodePositionLock = new();
        
        private readonly Queue<int> _drawQueue = [];
        private readonly Queue<int> _loadQueue = [];
        private readonly Queue<int> _wrapQueue = [];
        private readonly Dictionary<int, Chunk> _loaded = [];

        #endregion Variables
        
        public override void _Ready()
        {
            Generated = false;

            // Preload chunks when playing the game, disable when in the editor for faster project loading.
            if (Preload)
            {
                TryUpdateFocusNodeWorldPosition();
                TryUpdateFocusNodeChunkPosition();
                
                QueueChunks();
                LoadQueued();

                Generated = true;
            }
            
            // Start secondary thread to handle chunk queueing, loading, freeing, and repositioning ("wrapping").
            Thread worldThread = new(new ThreadStart(WorldProcess)) { Name = "World Thread" };
            worldThread.Start();
        }
        public override void _PhysicsProcess(double delta)
        {
            TryUpdateFocusNodeWorldPosition();
        }
        public void WorldProcess()
        {
            while (IsInstanceValid(this))
            {
                if (Generated)
                {
                    if (TryUpdateFocusNodeChunkPosition())
                    {
                        QueueChunks();
                        WrapQueued();
                    }
                }
                else
                {
                    TryUpdateFocusNodeChunkPosition();

                    FreeLoaded();
                    QueueChunks();
                    LoadQueued();

                    Generated = true;
                }

                Thread.Sleep(100);
            }
        }

        public void TryUpdateFocusNodeWorldPosition() // Update stored focus node world position.
        {
            if (FocusNode == null) return;
            
            lock (_focusNodePositionLock)
            {
                _focusNodeWorldPosition = (Vector3I)FocusNode.Position.Floor();
            }
        }
        public bool TryUpdateFocusNodeChunkPosition() // Update stored focus node chunk position.
        {
            if (FocusNode == null) return false;
            
            Vector3I queriedFocusNodeChunkPosition;
            
            lock (_focusNodePositionLock)
            {
                queriedFocusNodeChunkPosition = (Vector3I)(_focusNodeWorldPosition / WorldSettings.ChunkDiameter).Floor();
            }
            
            if (_focusNodeChunkPosition != queriedFocusNodeChunkPosition)
            {
                _focusNodeChunkPosition = queriedFocusNodeChunkPosition;

                return true;
            }

            return false;
        }
        
        private void QueueChunks() // Queue chunks into _drawQueue, _loadQueue, and _wrapQueue.
        {
            #region Draw Queue // Chunk positions that are drawable.

            _drawQueue.Clear();
            
            for (int x = 0; x < WorldSettings.DrawDiameter.X; x ++)
            {
                for (int y = 0; y < WorldSettings.DrawDiameter.Y; y ++)
                {
                    for (int z = 0; z < WorldSettings.DrawDiameter.Z; z ++)
                    {
                        Vector3I position = new Vector3I(x, y, z) - WorldSettings.DrawRadius + _focusNodeChunkPosition + WorldSettings.Radius;
                        
                        int chunkIndex = XYZConvert.Vector3IToIndex(position, WorldSettings.Diameter);
                        
                        _drawQueue.Enqueue(chunkIndex);
                    }
                }
            }

            GD.PrintS("--> Draw Queue:", _drawQueue.Count);

            #endregion Draw Queue

            #region Load Queue // Chunk positions that are drawable, but not loaded.

            if (_drawQueue.Count == 0) return;

            _loadQueue.Clear();

            foreach (int drawIndex in _drawQueue)
            {
                if (_loaded.ContainsKey(drawIndex) == false)
                {
                    _loadQueue.Enqueue(drawIndex);
                }
            }

            GD.PrintS("--> Load Queue:", _loadQueue.Count);

            #endregion Load Queue

            #region Wrap Queue // Chunk positions that are loaded, but not drawable.

            if (_loaded.Count == 0) return;

            _wrapQueue.Clear();

            foreach (int loadedIndex in _loaded.Keys)
            {
                if (_drawQueue.Contains(loadedIndex) == false)
                {
                    _wrapQueue.Enqueue(loadedIndex);
                }
            }

            GD.PrintS("--> Wrap Queue:", _wrapQueue.Count);

            #endregion Wrap Queue
        }
        private void LoadQueued()  // Load chunks into the scene tree.
        {
            if (_loadQueue.Count == 0) return;

            foreach (int loadIndex in _loadQueue)
            {
                Chunk chunk = new();
                _loaded.Add(loadIndex, chunk);

                chunk.MaterialOverride = WorldSettings.TerrainMaterial;

                Vector3I loadablePosition = XYZConvert.IndexToVector3I(loadIndex, WorldSettings.Diameter) - WorldSettings.Radius;
                
                // FIXME - This should eventually be a proper thread queue.
                Task generate = new(new Action(() => chunk.CallDeferred(nameof(Chunk.Generate), loadablePosition, WorldSettings)));
                generate.Start();
                generate.Wait();
                
                CallDeferred(Node.MethodName.AddChild, chunk);
                Thread.Sleep(WorldSettings.GenerateFrequency);
            }
        
            _loadQueue.Clear();
        }
        private void WrapQueued()  // Wrap chunks in the scene tree.
        {
            if (_wrapQueue.Count == 0 || _loadQueue.Count == 0) return;

            foreach (int wrapIndex in _wrapQueue)
            {
                Chunk chunk = _loaded[wrapIndex];
                _loaded.Remove(wrapIndex);
                
                int loadIndex = _loadQueue.Dequeue();
                _loaded.Add(loadIndex, chunk);
                
                Vector3I loadablePosition = XYZConvert.IndexToVector3I(loadIndex, WorldSettings.Diameter) - WorldSettings.Radius;
                
                // FIXME - This should eventually be a proper thread queue.
                Task generate = new(new Action(() => chunk.CallDeferred(nameof(Chunk.Generate), loadablePosition, WorldSettings)));
                generate.Start();
                generate.Wait();

                Thread.Sleep(WorldSettings.GenerateFrequency);
            }
            
            _wrapQueue.Clear();
        }
        private void FreeLoaded()  // Free chunks from the scene tree.
        {
            if (_loaded.Count == 0) return;

            foreach (int loadedIndex in _loaded.Keys)
            {
                Chunk chunk = _loaded[loadedIndex];
                
                _loaded.Remove(loadedIndex);
                
                chunk.QueueFree();
            }
        }
        
        public override string[] _GetConfigurationWarnings() // Godot specific configuration warnings.
        {
            if (FocusNode == null)
            {
                return
                [
                    "A Focus Node must be selected in the inspector to generate the world around.",
                    "(You can select the world itself.)"
                ];
            }

            return [];
        }
    }
}
