using Godot;
using System;
using RawUtils;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

// TODO - Thread Pool
// TODO - Fix octreeNode loading to always load chunks closest to focus node at surface level first.

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class OctreeRoot : Node3D
    {
        #region Exports

        [Export] public bool Regenerate
        {
            get { return _regenerate; }
            set { _regenerate = false; FreeLoaded(); WorldGenerated = false; }
        }
        private bool _regenerate = false;

        [Export] public Node3D FocusNode
        {
            get { return _focusNode; }
            set { _focusNode = value; WorldGenerated = false; }
        }
        private Node3D _focusNode;
        
        [Export] public World World { get; set; } = new();
        [Export] public byte Branches = 4;

        #endregion Exports

        #region Variables
        
        // Focus Node
        private readonly object _focusNodePositionLock = new();
        private Vector3I _focusNodeGlobalPosition;
        private Vector3I _focusNodeGridPosition = Vector3I.MinValue;
        
        // World
        private bool WorldGenerated;
        public Vector3I DrawDiameter;
        public Vector3I WorldDiameter;
        
        // Nodes
        private readonly object _queueLock = new();
        private readonly List<int> _drawable = new();
        private readonly List<int> _loadable = new();
        private readonly List<int> _freeable = new();
        private readonly Dictionary<int, OctreeNode> _loaded = new();

        #endregion Variables
        
        public OctreeRoot() {}
        
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
            
            // Lock access for primary thread.
            lock (_focusNodePositionLock)
            {
                _focusNodeGlobalPosition = (Vector3I)_focusNode.Position.Floor();
            }
        }
        private bool TryUpdateFocusNodeGridPosition()
        {
            if (_focusNode == null) return false;
            
            Vector3I queriedFocusNodeChunkPosition;
            
            // Lock acccess for secondary thread.
            lock (_focusNodePositionLock)
            {
                // Calculate queriedFocusNodeChunkPosition.
                queriedFocusNodeChunkPosition = new Vector3I()
                {
                    X = _focusNodeGlobalPosition.X >> Branches,
                    Y = _focusNodeGlobalPosition.Y >> Branches,
                    Z = _focusNodeGlobalPosition.Z >> Branches,
                };
            }
            
            // Check to see if _focusNodeGlobalPosition points to a different octreeNode.
            if (_focusNodeGridPosition != queriedFocusNodeChunkPosition)
            {
                // Update _focusNodeGridPosition and return.
                _focusNodeGridPosition = queriedFocusNodeChunkPosition;

                return true;
            }

            return false;
        }

        // World dimensions
        private void UpdateWorldDiameter()
        {
            WorldDiameter.X = (World.Radius.X << 1) + 1;
            WorldDiameter.Y = (World.Radius.Y << 1) + 1;
            WorldDiameter.Z = (World.Radius.Z << 1) + 1;
        }
        private void UpdateDrawDiameter()
        {
            DrawDiameter.X = (World.Draw.X << 1) + 1;
            DrawDiameter.Y = (World.Draw.Y << 1) + 1;
            DrawDiameter.Z = (World.Draw.Z << 1) + 1;
        }
        
        // World Generation
        private void GenerateWorld()
        {   
            UpdateWorldDiameter();
            UpdateDrawDiameter();

            QueueDrawable();
            QueueLoadable();
            QueueFreeable();

            LoadQueued();
            FreeQueued();

            WorldGenerated = true;
        }
        private void WrapWorld()
        {   
            QueueDrawable();
            QueueLoadable();
            QueueFreeable();

            WrapQueued();
        }
        
        // Node Queueing
        private void QueueDrawable()
        {
            _drawable.Clear();
            
            for (int x = - World.Draw.X; x <= World.Draw.X; x ++)
            {
                for (int y = - World.Draw.Y; y <= World.Draw.Y; y ++)
                {
                    for (int z = - World.Draw.Z; z <= World.Draw.Z; z ++)
                    {
                        Vector3I signedPosition = new Vector3I(x, y, z) + _focusNodeGridPosition;
                        Vector3I unsignedPosition = signedPosition + World.Radius;
                        
                        int gridIndex = XYZConvert.Vector3IToIndex(unsignedPosition, WorldDiameter);
                        
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

            foreach (int drawableGridIndex in _drawable)
            {
                if (_loaded.ContainsKey(drawableGridIndex) == false)
                {
                    _loadable.Add(drawableGridIndex);
                }
            }

            GD.PrintS("--> Loadable chunks:", _loadable.Count);
        }
        private void QueueFreeable()
        {
            if (_loaded.Count == 0) return;
            
            _freeable.Clear();

            foreach (int loadedGridIndex in _loaded.Keys)
            {
                if (_drawable.Contains(loadedGridIndex) == false)
                {
                    _freeable.Add(loadedGridIndex);
                }
            }

            GD.PrintS("--> Freeable chunks:", _freeable.Count);
        }

        // Node Generation
        private void GenerateNode(OctreeNode octreeNode, int gridIndex)
        {
            Vector3I gridPosition = XYZConvert.IndexToVector3I(gridIndex, WorldDiameter) - World.Radius;
            Vector3I globalPosition = XYZBitShift.Vector3ILeft(gridPosition, Branches);
                
            Biome biome = Biome.Generate(World, gridPosition);

            Task generate = new(new Action(() => octreeNode.CallDeferred(nameof(OctreeNode.GenerateVoxels), globalPosition, biome)));

            generate.Start();
            generate.Wait();

            Thread.Sleep(World.GenerateFrequency);
        }
        
        // Queued Nodes
        private void LoadQueued()
        {
            if (_loadable.Count == 0) return;

            foreach (int loadableIndex in _loadable)
            {
                OctreeNode octreeNode = new(World, Branches);
                _loaded.Add(loadableIndex, octreeNode);
                
                GenerateNode(octreeNode, loadableIndex);
                CallDeferred(Node.MethodName.AddChild, octreeNode);
            }
        
            _loadable.Clear();
        }
        private void FreeQueued()
        {
            if (_freeable.Count == 0) return;

            foreach (int freeableIndex in _freeable)
            {    
                OctreeNode octreeNode = _loaded[freeableIndex];
                _loaded.Remove(freeableIndex);
                octreeNode.QueueFree();
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
                
                OctreeNode octreeNode = _loaded[freeableIndex];
                _loaded.Remove(freeableIndex);
                _loaded.Add(loadableIndex, octreeNode);
                
                GenerateNode(octreeNode, loadableIndex);
            }
            
            _freeable.Clear();
        }

        // Loaded Nodes
        private void FreeLoaded()
        {
            if (_loaded.Count == 0) return;

            foreach (int loadedGridIndex in _loaded.Keys)
            {
                OctreeNode octreeNode = _loaded[loadedGridIndex];
                _loaded.Remove(loadedGridIndex);
                octreeNode.QueueFree();
            }
        }
    }
}
