using Godot;
using System;
using RawVoxel.Meshing;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using RawVoxel.Math.Conversions;
using System.Collections.Generic;

namespace RawVoxel.World;

[Tool]
public partial class World() : MeshInstance3D
{
    const byte ChunkDiameter = 32;

    #region Exports

    [Export] public bool Generated = false;
    [Export] public Node3D FocusNode { get; set; }
    [Export] public WorldSettings WorldSettings { get; set; }
    [Export] public Material TerrainMaterial { get; set; } = new StandardMaterial3D();
    
    [ExportGroup("Dimensions")]
    [Export] public bool CenterChunk = true;
    private Vector3I _drawRadius = new(1, 1, 1);
    [Export] public Vector3I DrawRadius
    {
        get => CenterChunk switch
        {
            false => _drawRadius.Clamp(Vector3I.One,  WorldRadius),
            true  => _drawRadius.Clamp(Vector3I.Zero, WorldRadius),
        };
        
        set => _drawRadius = value;
    }
    public Vector3I DrawDiameter
    {
        get
        {
            Vector3I diameter = XYZBitShift.Vector3ILeft(DrawRadius, 1);
            
            return CenterChunk switch
            {
                false => diameter.Clamp(diameter, WorldDiameter),
                true  => diameter.Clamp(diameter, WorldDiameter) + Vector3I.One,
            };
        }
    }
    private Vector3I _worldRadius = new(128, 128, 128);
    [Export] public Vector3I WorldRadius
    {
        get => CenterChunk switch
        {
            false => _worldRadius.Clamp(Vector3I.One,  Vector3I.MaxValue / 2),
            true  => _worldRadius.Clamp(Vector3I.Zero, Vector3I.MaxValue / 2),
        };
        
        set => _worldRadius = value;
    }
    public Vector3I WorldDiameter
    {
        get
        {
            Vector3I diameter = XYZBitShift.Vector3ILeft(WorldRadius, 1);
            
            return CenterChunk switch
            {
                false => diameter.Clamp(diameter, Vector3I.MaxValue),
                true  => diameter.Clamp(diameter, Vector3I.MaxValue) + Vector3I.One,
            };
        }
    }
    
    [ExportGroup("Meshing")]
    [Export] public bool CullGeometry = false;

    [ExportGroup("Rendering")]
    [Export] public bool ShowChunkEdges = false;

    [ExportGroup("Threading")]
    [Export] public int GenerateFrequency = 30;

    #endregion Exports

    #region Variables

    private Vector3 _focusNodeTruePosition;
    private Vector3I _focusNodeGridPosition = Vector3I.MinValue;
    private readonly object _focusNodePositionLock = new();
    
    private Vector3 _focusNodeTrueBasisZ;
    private Vector3I _focusNodeSignBasisZ = Vector3I.Zero;
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

        // Try to update these before physics process takes over. Ensures world load at the correct initial position.
        TryUpdateFocusNodeTruePosition();
        TryUpdateFocusNodeTrueBasisZ();
        
        // Preload drawable chunks when playing the game, disable when in the editor for faster scene loading.
        if (Engine.IsEditorHint() == false)
        {
            TryUpdateFocusNodeGridPosition();
            TryUpdateFocusNodeSignBasisZ();

            QueueChunks();
            LoadQueued();
            MeshLoaded();

            Generated = true;
        }
        
        // Start secondary thread to handle chunk queueing, loading, freeing, and wrapping.
        Thread worldThread = new(new ThreadStart(WorldProcess)) { Name = "World Thread" };
        worldThread.Start();
    }
    public override void _PhysicsProcess(double delta)
    {
        TryUpdateFocusNodeTruePosition();
        TryUpdateFocusNodeTrueBasisZ();
    }
    public void WorldProcess()
    {
        while (IsInstanceValid(this))
        {
            if (Generated)
            {
                if (TryUpdateFocusNodeSignBasisZ())
                {
                    GD.Print("Meshing world.");

                    MeshLoaded();
                }

                if (TryUpdateFocusNodeGridPosition())
                {
                    GD.Print("Wrapping world.");
                    
                    QueueChunks();
                    WrapQueued();
                    MeshLoaded();
                }
            }
            else
            {
                GD.Print("Generating world.");

                TryUpdateFocusNodeGridPosition();
                TryUpdateFocusNodeSignBasisZ();

                QueueChunks();
                LoadQueued();
                MeshLoaded();

                Generated = true;
            }

            Thread.Sleep(100);
        }
    }

    public void TryUpdateFocusNodeTruePosition() // Update stored focus node world position.
    {
        if (FocusNode == null) return;

        lock (_focusNodePositionLock)
        {
            _focusNodeTruePosition = (Vector3I)FocusNode.Position.Floor();
        }
    }
    public bool TryUpdateFocusNodeGridPosition() // Update stored focus node chunk position.
    {
        if (FocusNode == null) return false;

        Vector3I queriedFocusNodeChunkPosition;

        lock (_focusNodePositionLock)
        {
            queriedFocusNodeChunkPosition = (Vector3I)(_focusNodeTruePosition / ChunkDiameter).Floor();
        }

        if (_focusNodeGridPosition != queriedFocusNodeChunkPosition)
        {
            _focusNodeGridPosition = queriedFocusNodeChunkPosition;

            return true;
        }

        return false;
    }
    public void TryUpdateFocusNodeTrueBasisZ()   // Update stored focus node true basis z.
    {
        if (FocusNode == null) return;
        
        lock (_focusNodeBasisZLock)
        {
            _focusNodeTrueBasisZ = FocusNode.Basis.Z;
        }
    }
    public bool TryUpdateFocusNodeSignBasisZ()   // Update stored focus node sign basis z.
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
                    Vector3I position = new Vector3I(x, y, z) - DrawRadius + _focusNodeGridPosition + WorldRadius;

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

            chunk.Visible = false;

            CallDeferred(Node.MethodName.AddChild, chunk);

            Task generate = new(new Action(() => CallDeferred(nameof(GenerateChunkData), loadIndex, chunk, WorldSettings)));
            
            generate.Start();
            generate.Wait();

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
            int loadIndex = _loadQueue.Dequeue();
            
            _loaded.Remove(wrapIndex);
            _loaded.Add(loadIndex, chunk);
            
            chunk.Visible = false;

            Task generate = new(new Action(() => CallDeferred(nameof(GenerateChunkData), loadIndex, chunk, WorldSettings)));
            
            generate.Start();
            generate.Wait();

            Thread.Sleep(GenerateFrequency);
        }

        _wrapQueue.Clear();
    }
    private void MeshLoaded()  // Mesh chunks in the scene tree.
    {
        if (_loaded.Count == 0) return;
        
        foreach (int chunkIndex in _loaded.Keys)
        {
            Chunk chunk = _loaded[chunkIndex];

            // Skip chunk if it's marked as empty.
            if (chunk.Contents == Chunk.ChunkContents.Empty) continue;

            // Generate chunk mesh.
            Task generateMesh = new(new Action(() => CallDeferred(nameof(GenerateChunkMesh), chunkIndex, chunk, WorldSettings)));
            
            generateMesh.Start();
            generateMesh.Wait();

            chunk.Visible = true;

            Thread.Sleep(10);
        }
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
        
        // Stopwatch stopwatch = Stopwatch.StartNew();
        
        // FIXME - Needs to be a LOT faster. Start in reverse and figure out how to send less data.
        chunk.GenerateVoxels(chunkTruePosition, ChunkDiameter, biome, worldSettings);

        // stopwatch.Stop(); GD.Print("~~~ Completed in " + stopwatch.ElapsedTicks / 10000.0f + " ms.");
    }
    public void GenerateChunkMesh(int chunkIndex, Chunk chunk, WorldSettings worldSettings)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        
        // Calculate chunk position.
        Vector3I chunkGridPosition = XYZConvert.IndexToVector3I(chunkIndex, WorldDiameter) - WorldRadius;
        
        // This can only be overwritten if the global variable is false.
        bool cullGeometry = CullGeometry;

        // Don't cull geometry from chunks in a buffer around the focus node to prevent (some) edge cases of clipping into the world.
        if (CullGeometry)
        {
            if(chunkGridPosition <= _focusNodeGridPosition - Vector3I.One || chunkGridPosition >= _focusNodeGridPosition + Vector3I.One)
                cullGeometry = true;
        }
        
        // Generate mesh surfaces. Each of the three surfaces contains vertex, normal, and index data for planes on each axis respectively.
        Surface[] surfaces = BinaryMesher.GenerateSurfaces(ref chunk.VoxelTypes, ChunkDiameter, _focusNodeSignBasisZ, cullGeometry);
        
        // Generate mesh.
        chunk.Mesh = MeshHelper.GenerateMesh(surfaces, TerrainMaterial);
        
        // Clear previous collision shape if any.
        chunk.GetChildOrNull<StaticBody3D>(0)?.QueueFree();

        // Create collision.
        chunk.CreateTrimeshCollision();

        stopwatch.Stop(); GD.Print("~~~ Completed in " + stopwatch.ElapsedTicks / 10000.0f + " ms.");
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
