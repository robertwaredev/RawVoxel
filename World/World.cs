using Godot;
using System;
using RawVoxel.Meshing;
using System.Threading;
using System.Threading.Tasks;
using RawVoxel.Math.Conversions;
using System.Collections.Generic;
using System.Diagnostics;

namespace RawVoxel.World;

[Tool]
public partial class World() : MeshInstance3D
{
    public enum MeshGenerationType { Greedy, Standard }

    #region Exports

    [Export] public bool Generated = false;
    [Export] public Node3D FocusNode { get; set; }
    [Export] public WorldSettings WorldSettings { get; set; }
    [Export] public Material TerrainMaterial { get; set; } = new StandardMaterial3D();
    
    [ExportGroup("Dimensions")]
    [Export] public bool CenterChunk = true;
    [Export] public Vector3I DrawRadius = new(1, 1, 1);
    public Vector3I DrawDiameter
    {
        get
        {
            Vector3I diameter = XYZBitShift.Vector3ILeft(DrawRadius, 1);
            if (CenterChunk) diameter += Vector3I.One;
            diameter.Clamp(Vector3I.Zero, WorldDiameter);
            return diameter;
        }
    }
    [Export] public Vector3I WorldRadius = new(128, 128, 128);
    public Vector3I WorldDiameter
    {
        get
        {
            Vector3I diameter = XYZBitShift.Vector3ILeft(WorldRadius, 1);
            if (CenterChunk) diameter += Vector3I.One;
            return diameter;
        }
    }
    [Export] public byte ChunkDiameter = 32;

    [ExportGroup("Rendering")]
    [Export] public bool ShowChunkEdges = false;
    [Export] public bool CullGeometry = false;
    [Export] public MeshGenerationType MeshGeneration { get; set; } = MeshGenerationType.Greedy;

    [ExportGroup("Threading")]
    [Export] public int GenerateFrequency = 30;

    #endregion Exports

    #region Variables

    private Vector3 _focusNodeWorldPosition;
    private Vector3I _focusNodeChunkPosition = Vector3I.MinValue;
    private readonly object _focusNodePositionLock = new();
    private Vector3 _focusNodeTrueBasisZ;
    private Vector3I _focusNodeSignBasisZ = Vector3I.MinValue;
    private readonly object _focusNodeBasisZLock = new();

    private readonly Queue<int> _drawQueue = [];
    private readonly Queue<int> _loadQueue = [];
    private readonly Queue<int> _wrapQueue = [];
    private readonly Dictionary<int, Chunk> _loaded = [];

    #endregion Variables

    public override void _Ready()
    {
        // This has to be reset on scene entry as it always saves true on scene exit.
        Generated = false;

        // Preload drawable chunks when playing the game, disable when in the editor for faster scene loading.
        if (Engine.IsEditorHint() == false)
        {
            TryUpdateFocusNodeWorldPosition();
            TryUpdateFocusNodeChunkPosition();
            
            TryUpdateFocusNodeTrueBasisZ();
            TryUpdateFocusNodeSignBasisZ();

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
        TryUpdateFocusNodeTrueBasisZ();
    }
    public void WorldProcess()
    {
        while (IsInstanceValid(this))
        {
            if (Generated)
            {
                if (TryUpdateFocusNodeChunkPosition() || TryUpdateFocusNodeSignBasisZ())
                {
                    GD.Print("Updating world.");
                    
                    QueueChunks();
                    WrapQueued();
                }
            }
            else
            {
                GD.Print("Generating world.");

                TryUpdateFocusNodeChunkPosition();
                TryUpdateFocusNodeSignBasisZ();

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
            queriedFocusNodeChunkPosition = (Vector3I)(_focusNodeWorldPosition / ChunkDiameter).Floor();
        }

        if (_focusNodeChunkPosition != queriedFocusNodeChunkPosition)
        {
            _focusNodeChunkPosition = queriedFocusNodeChunkPosition;

            return true;
        }

        return false;
    }
    public void TryUpdateFocusNodeTrueBasisZ()    // Update stored focus node true basis z.
    {
        if (FocusNode == null) return;
        
        lock (_focusNodeBasisZLock)
        {
            _focusNodeTrueBasisZ = FocusNode.Basis.Z;
        }
    }
    public bool TryUpdateFocusNodeSignBasisZ()    // Update stored focus node axis basis z.
    {
        if (FocusNode == null) return false;

        Vector3I queriedfocusNodeChunkBasisZ;

        // FIXME - check math for mathing.
        lock (_focusNodeBasisZLock)
        {
            queriedfocusNodeChunkBasisZ = new()
            {
                X = Mathf.Sign(_focusNodeTrueBasisZ.X),
                Y = Mathf.Sign(_focusNodeTrueBasisZ.Y),
                Z = Mathf.Sign(_focusNodeTrueBasisZ.Z)
            };
        }
        
        if (_focusNodeSignBasisZ != queriedfocusNodeChunkBasisZ)
        {
            _focusNodeSignBasisZ = queriedfocusNodeChunkBasisZ;
            
            return true;
        }

        return false;
    }
    
    private void QueueChunks() // Queue chunks into _drawQueue, _loadQueue, and _wrapQueue.
    {
        #region Draw Queue // Chunk positions that are drawable.

        _drawQueue.Clear();

        for (int x = 0; x < DrawDiameter.X; x++)
        {
            for (int y = 0; y < DrawDiameter.Y; y++)
            {
                for (int z = 0; z < DrawDiameter.Z; z++)
                {
                    Vector3I position = new Vector3I(x, y, z) - DrawRadius + _focusNodeChunkPosition + WorldRadius;

                    int chunkIndex = XYZConvert.Vector3IToIndex(position, WorldDiameter);

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

            Task generate = new(new Action(() => CallDeferred(nameof(GenerateChunkData), loadIndex, chunk, WorldSettings)));
            
            generate.Start();
            generate.Wait();

            CallDeferred(Node.MethodName.AddChild, chunk);

            chunk.AddToGroup("NavSource");

            Thread.Sleep(GenerateFrequency);
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
            
            Task generate = new(new Action(() => CallDeferred(nameof(GenerateChunkData), loadIndex, chunk, WorldSettings)));
            
            generate.Start();
            generate.Wait();

            Thread.Sleep(GenerateFrequency);
        }

        _wrapQueue.Clear();
    }
    private void MeshLoaded()  // Mesh chunks in the scene tree.
    {

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

    public void GenerateChunkData(int chunkIndex, Chunk chunk, WorldSettings worldSettings)
    {
        // Calculate chunk position.
        Vector3I chunkGridPosition = XYZConvert.IndexToVector3I(chunkIndex, WorldDiameter) - WorldRadius;
        Vector3I chunkTruePosition = XYZBitShift.Vector3ILeft(chunkGridPosition, XYZBitShift.CalculateShifts(ChunkDiameter));
        
        // Set chunk position.
        chunk.Position = chunkTruePosition;
        
        // FIXME - This is fast for now, but probably dreadful when there'a a lot of biomes.
        Biome biome = Biome.Generate(chunkGridPosition, WorldDiameter, worldSettings);
        
        Stopwatch stopwatch = Stopwatch.StartNew();
        
        // FIXME - Could be a LOT faster. Start in reverse and figure out how to send less data.
        byte[] voxels = Chunk.GenerateVoxels(chunkTruePosition, ChunkDiameter, biome, worldSettings);

        stopwatch.Stop(); GD.Print("~~~ Completed in " + stopwatch.ElapsedTicks / 10000.0f + " ms.");
        
        GenerateChunkMesh(ref voxels, chunkIndex, chunk, biome, worldSettings);
        
    }
    
    public void GenerateChunkMesh(ref byte[] voxels, int chunkIndex, Chunk chunk, Biome biome, WorldSettings worldSettings)
    {
        // Calculate chunk position.
        Vector3I chunkGridPosition = XYZConvert.IndexToVector3I(chunkIndex, WorldDiameter) - WorldRadius;
        Vector3I chunkTruePosition = XYZBitShift.Vector3ILeft(chunkGridPosition, XYZBitShift.CalculateShifts(ChunkDiameter));

        bool cullGeometry = CullGeometry;
        
        // Don't cull geometry from chunks in a buffer around the focus node to prevent unforseen edge cases of clipping into the world.
        if (chunkGridPosition <= _focusNodeChunkPosition - Vector3I.One || chunkGridPosition >= _focusNodeChunkPosition + Vector3I.One) cullGeometry = true;

        // TODO - Add voxel homogeneity check so we can skip these surface generators and use much simpler ones on homogenous chunks.
        
        // Switch surface generation algorithm based on export setting.
        Surface[] surfaces = MeshGeneration switch
        {
            MeshGenerationType.Greedy => BinaryMesher.GenerateSurfaces(ref voxels, ChunkDiameter, _focusNodeSignBasisZ, cullGeometry),
            _                         => CulledMesher.GenerateSurfaces(chunkTruePosition, ChunkDiameter, ShowChunkEdges, ref voxels, ref biome, ref worldSettings),
        };
        
        // Clear previous collision shape if any.
        StaticBody3D collision = chunk.GetChildOrNull<StaticBody3D>(0);
        collision?.QueueFree();
        
        // Check if any surfaces contain vertices. This is temporary workaround for not having a voxel homogeneity check.
        foreach (Surface surface in surfaces)
        {
            if (surface.Vertices.Count != 0)
            {
                chunk.Mesh = MeshHelper.GenerateMesh(ref surfaces, TerrainMaterial);

                chunk.CreateTrimeshCollision();
                break;
            }
        }
        
        chunk.AddToGroup("NavSource");
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
