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
    public partial class ChunkGrid : Node3D
    {
        #region Exports

        [Export] public bool PreloadChunks;
        [Export] public bool Regenerate
        {
            get { return _regenerate; }
            set { _regenerate = false; Generated = false; }
        }
        private bool _regenerate = false;

        [Export] public Node3D FocusNode { get; set; }
        [Export] public World World
        {
            get { return _world; }
            set { _world = value; }
        }
        private World _world;

        #endregion Exports
        
        #region Variables
        
        public bool Generated;

        private Vector3 _focusNodeGlobalPosition;
        private Vector3I _focusNodeChunkPosition = Vector3I.MinValue;
        private readonly object _focusNodePositionLock = new();
        
        private readonly List<int> _drawable = new();
        private readonly List<int> _loadable = new();
        private readonly List<int> _freeable = new();
        private readonly Dictionary<int, Chunk> _loaded = new();

        #endregion Variables
        
        public ChunkGrid() {}
        
        // On node ready.
        public override void _Ready()
        {
            if (PreloadChunks)
            {
                TryUpdateFocusNodeGlobalPosition();
                TryUpdateFocusNodeChunkPosition();
                Init();
                Generated = true;
            }
            
            Thread Thread = new(new ThreadStart(ChunkGridProcess)) { Name = "Chunk Container Thread" };
            Thread.Start();
        }
        // Physics process.
        public override void _PhysicsProcess(double delta)
        {
            TryUpdateFocusNodeGlobalPosition();
        }
        // Thread process.
        public void ChunkGridProcess()
        {
            while (IsInstanceValid(this))
            {
                if (Generated == false)
                {
                    TryUpdateFocusNodeChunkPosition();
                    Init();

                    Generated = true;
                }
                else if (TryUpdateFocusNodeChunkPosition())
                {
                    Wrap();
                }
                
                Thread.Sleep(100);
            }
        }

        // Update stored focus node chunk and global positions.
        public void TryUpdateFocusNodeGlobalPosition()
        {
            if (FocusNode == null) return;
            
            lock (_focusNodePositionLock)
            {
                _focusNodeGlobalPosition = (Vector3I)FocusNode.Position.Floor();
            }
        }
        public bool TryUpdateFocusNodeChunkPosition()
        {
            if (FocusNode == null) return false;
            
            Vector3I queriedFocusNodeChunkPosition;
            
            lock (_focusNodePositionLock)
            {
                queriedFocusNodeChunkPosition = (Vector3I)(_focusNodeGlobalPosition / World.ChunkDiameter).Floor();
            }
            
            if (_focusNodeChunkPosition != queriedFocusNodeChunkPosition)
            {
                _focusNodeChunkPosition = queriedFocusNodeChunkPosition;

                return true;
            }

            return false;
        }
        
        // Queue, load, and free chunks within drawable bounds.
        public void Init()
        {
            if (_loaded.Count > 0)
            {
                FreeLoaded();
            }
            
            QueueDrawable();
            QueueLoadable();
            QueueFreeable();

            LoadQueued();
            FreeQueued();
        }
        public void Wrap()
        {
            QueueDrawable();
            QueueLoadable();
            QueueFreeable();

            WrapQueued();
        }
        
        // Queue chunks.
        private void QueueDrawable()
        {
            _drawable.Clear();
            
            int centerChunk = 0;
            Vector3I drawRadius;

            if (World.CenterChunk == true)
            {
                centerChunk = 1;
                drawRadius = World.DrawRadius.Clamp(Vector3I.Zero, World.Radius);
            }
            else
            {
                drawRadius = World.DrawRadius.Clamp(Vector3I.One, World.Radius);
            }

            for (int x = -drawRadius.X; x < drawRadius.X + centerChunk; x ++)
            {
                for (int y = -drawRadius.Y; y < drawRadius.Y + centerChunk; y ++)
                {
                    for (int z = -drawRadius.Z; z < drawRadius.Z + centerChunk; z ++)
                    {
                        Vector3I position = new Vector3I(x, y, z) + _focusNodeChunkPosition + World.Radius;
                        
                        int chunkIndex = XYZConvert.Vector3IToIndex(position, World.Diameter);
                        
                        _drawable.Add(chunkIndex);
                    }
                }
            }

            GD.PrintS("--> Drawable chunks:", _drawable.Count);
        }
        private void QueueLoadable()
        {
            if (_drawable.Count == 0) return;

            _loadable.Clear();

            foreach (int drawableIndex in _drawable)
            {
                if (_loaded.ContainsKey(drawableIndex) == false)
                {
                    _loadable.Add(drawableIndex);
                }
            }

            GD.PrintS("--> Loadable chunks:", _loadable.Count);
        }
        private void QueueFreeable()
        {
            if (_loaded.Count == 0) return;

            _freeable.Clear();

            foreach (int loadedIndex in _loaded.Keys)
            {
                if (_drawable.Contains(loadedIndex) == false)
                {
                    _freeable.Add(loadedIndex);
                }
            }

            GD.PrintS("--> Freeable chunks:", _freeable.Count);
        }

        // Load, free, and reposition chunks.
        private void LoadQueued()
        {
            if (_loadable.Count == 0) return;

            foreach (int loadableIndex in _loadable)
            {
                Chunk chunk = new(ref _world);
                _loaded.Add(loadableIndex, chunk);

                Vector3I loadablePosition = XYZConvert.IndexToVector3I(loadableIndex, World.Diameter) - World.Radius;
                
                // FIXME - This should eventually be a proper thread queue.
                Task generate = new(new Action(() => chunk.CallDeferred(nameof(Chunk.Generate), loadablePosition)));
                generate.Start();
                generate.Wait();
                
                CallDeferred(Node.MethodName.AddChild, chunk);
                Thread.Sleep(chunk.World.GenerateFrequency);
            }
        
            _loadable.Clear();
        }
        private void FreeQueued()
        {
            if (_freeable.Count == 0) return;

            foreach (int freeableIndex in _freeable)
            {
                Chunk chunk = _loaded[freeableIndex];

                _loaded.Remove(freeableIndex);
                
                chunk.QueueFree();
            }
        
            _freeable.Clear();
        }
        private void WrapQueued()
        {
            if (_freeable.Count == 0) return;
            if (_loadable.Count == 0) return;

            foreach (int freeableIndex in _freeable)
            {
                Chunk chunk = _loaded[freeableIndex];
                _loaded.Remove(freeableIndex);
                
                int loadableIndex = _loadable.First();
                _loadable.Remove(loadableIndex);
                _loaded.Add(loadableIndex, chunk);
                
                Vector3I loadablePosition = XYZConvert.IndexToVector3I(loadableIndex, World.Diameter) - World.Radius;
                
                // FIXME - This should eventually be a proper thread queue.
                Task generate = new(new Action(() => chunk.CallDeferred(nameof(Chunk.Generate), loadablePosition)));
                generate.Start();
                generate.Wait();

                Thread.Sleep(World.GenerateFrequency);
            }
            
            _freeable.Clear();
        }
    
        // Clear all chunks.
        private void FreeLoaded()
        {
            if (_loaded.Count == 0) return;

            foreach (int loadedIndex in _loaded.Keys)
            {
                Chunk chunk = _loaded[loadedIndex];
                
                _loaded.Remove(loadedIndex);
                
                chunk.QueueFree();
            }
        }
        
        // Godot specific warnings.
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

            return Array.Empty<String>();
        }
    }
}
