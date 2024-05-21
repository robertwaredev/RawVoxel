using Godot;
using System;
using RawUtils;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RawVoxel
{
    public abstract partial class ChunkQueue : Node3D
    {
        #region Exports

        [Export] public bool Regenerate
        {
            get { return _regenerate; }
            set { _regenerate = false; FreeLoaded(); Generated = false; }
        }
        private bool _regenerate = false;

        [Export] public Node3D FocusNode { get; set; }
        [Export] public World World { get; set; } = new();

        #endregion Exports
        
        #region Variables
        
        public bool Generated;

        private readonly object _focusNodePositionLock = new();
        private Vector3 _focusNodeGlobalPosition;
        private Vector3I _focusNodeGridPosition = Vector3I.MinValue;
        
        private readonly object _queueLock = new();
        private readonly List<int> _drawable = new();
        private readonly List<int> _loadable = new();
        private readonly List<int> _freeable = new();
        private readonly Dictionary<int, VoxelContainer> _loaded = new();

        #endregion Variables
        
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

        public void TryUpdateFocusNodeGlobalPosition()
        {
            if (FocusNode == null) return;
            
            lock (_focusNodePositionLock)
            {
                _focusNodeGlobalPosition = (Vector3I)FocusNode.Position.Floor();
            }
        }
        public bool TryUpdateFocusNodeGridPosition()
        {
            if (FocusNode == null) return false;
            
            Vector3I queriedFocusNodeChunkPosition;
            
            lock (_focusNodePositionLock)
            {
                queriedFocusNodeChunkPosition = (Vector3I)(_focusNodeGlobalPosition / World.ChunkDiameter).Floor();
            }
            
            if (_focusNodeGridPosition != queriedFocusNodeChunkPosition)
            {
                _focusNodeGridPosition = queriedFocusNodeChunkPosition;

                return true;
            }

            return false;
        }
        
        public void LoadAndFree(VoxelContainer voxelContainer)
        {
            lock (_queueLock)
            {
                QueueDrawable();
                QueueLoadable();
                QueueFreeable();

                LoadQueued();
                FreeQueued();
            }
        }
        public void Wrap()
        {
            lock (_queueLock)
            {
                QueueDrawable();
                QueueLoadable();
                QueueFreeable();

                WrapQueued();
            }
        }
        
        private void QueueDrawable()
        {
            _drawable.Clear();
            
            int centerChunk = 0;
            Vector3I drawRadius;

            if (World.CenterChunk)
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
                        Vector3I position = new Vector3I(x, y, z) + _focusNodeGridPosition + World.Radius;
                        
                        int gridIndex = XYZConvert.Vector3IToIndex(position, World.Diameter);
                        
                        _drawable.Add(gridIndex);
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

        private void LoadQueued()
        {
            if (_loadable.Count == 0) return;

            foreach (int loadableIndex in _loadable)
            {
                VoxelContainer voxelContainer = new Chunk(World);

                _loaded.Add(loadableIndex, voxelContainer);

                Vector3I loadablePosition = XYZConvert.IndexToVector3I(loadableIndex, World.Diameter) - World.Radius;
                
                Task generate = new(new Action(() => voxelContainer.CallDeferred(nameof(VoxelContainer.GenerateVoxels), loadablePosition)));
                generate.Start();
                generate.Wait();
                
                CallDeferred(Node.MethodName.AddChild, voxelContainer);
                Thread.Sleep(voxelContainer.World.GenerateFrequency);
            }
        
            _loadable.Clear();
        }
        private void FreeQueued()
        {
            if (_freeable.Count == 0) return;

            foreach (int freeableIndex in _freeable)
            {
                VoxelContainer voxelContainer = _loaded[freeableIndex];

                _loaded.Remove(freeableIndex);
                
                voxelContainer.QueueFree();
            }
        
            _freeable.Clear();
        }
        private void WrapQueued()
        {
            if (_freeable.Count == 0) return;
            if (_loadable.Count == 0) return;

            foreach (int freeableIndex in _freeable)
            {
                int loadableIndex = _loadable.First();
                _loadable.Remove(loadableIndex);
                
                Vector3I loadablePosition = XYZConvert.IndexToVector3I(loadableIndex, World.Diameter) - World.Radius;

                VoxelContainer voxelContainer = _loaded[freeableIndex];
                _loaded.Remove(freeableIndex);
                _loaded.Add(loadableIndex, voxelContainer);
                
                Task generate = new(new Action(() => voxelContainer.CallDeferred(nameof(VoxelContainer.GenerateVoxels), loadablePosition)));
                generate.Start();
                generate.Wait();

                Thread.Sleep(World.GenerateFrequency);
            }
            
            _freeable.Clear();
        }
    
        public void FreeLoaded()
        {
            if (_loaded.Count == 0) return;

            foreach (int loadedIndex in _loaded.Keys)
            {
                VoxelContainer voxelContainer = _loaded[loadedIndex];
                
                _loaded.Remove(loadedIndex);
                
                voxelContainer.QueueFree();
            }
        }
    }
}