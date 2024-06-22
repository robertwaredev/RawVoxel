using Godot;
using RawVoxel.Meshing;
using System.Threading;
using RawVoxel.Math.Conversions;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace RawVoxel.World;

// TODO - Optimize mesh generation with the RenderingServe.
// NOTE - Bypassing the scene tree might cause complications for world interactions.

[Tool]
public partial class World() : MeshInstance3D
{
    #region Constants

    public const int ChunkRadius = 16;
    public const int ChunkDiameter = 32;
    public const int ChunkBitshifts = 5;
    public const int ChunkVoxelCount = ChunkDiameter << ChunkBitshifts << ChunkBitshifts;

    #endregion Constants

    #region Exports

    [Export] public bool Generated = false;
    [Export] public Node3D FocusNode { get; set; }
    [Export] public Camera3D Camera { get; set; }
    [Export] public WorldSettings WorldSettings { get; set; }
    [Export] public Material TerrainMaterial { get; set; } = new StandardMaterial3D();
    
    [ExportGroup("Dimensions")]
    private Vector3I _drawRadius = new(1, 1, 1);
    [Export] public Vector3I DrawRadius
    {
        get => _drawRadius.Clamp(Vector3I.One,  WorldRadius);
        set => _drawRadius = value;
    }
    public Vector3I DrawDiameter
    {
        get => XYZBitShift.Vector3ILeft(DrawRadius, 1).Clamp(DrawRadius, WorldDiameter);
    }
    private Vector3I _worldRadius = new(128, 128, 128);
    [Export] public Vector3I WorldRadius
    {
        get => _worldRadius.Clamp(Vector3I.One,  Vector3I.MaxValue / 2);
        set => _worldRadius = value;
    }
    public Vector3I WorldDiameter
    {
        get => XYZBitShift.Vector3ILeft(WorldRadius, 1).Clamp(WorldRadius, Vector3I.MaxValue);
    }
    
    [ExportGroup("Meshing")]
    [Export] public bool CullFrustum = false;
    [Export] public bool CullGeometry = false;

    //[ExportGroup("Rendering")]
    //[Export] public bool ShowChunkEdges = false;

    [ExportGroup("Threading")]
    [Export] public int GenerateFrequency = 30;

    #endregion Exports

    #region Variables

    private Vector3 _focusNodeTruePosition; // Physics process only.
    private Vector3I _focusNodeGridPosition = Vector3I.MinValue; // Queue process only.
    private readonly object _focusNodePositionLock = new();
    
    private Vector3 _focusNodeTrueBasisZ; // Physics process only.
    private Vector3I _focusNodeSignBasisZ = Vector3I.Zero; // Mesh process only.
    private readonly object _focusNodeBasisZLock = new();
    
    private readonly ConcurrentDictionary<int, Chunk> _chunks = new(); // Chunks that are loaded into the scene tree, regardless of state.

    private readonly Queue<int> _drawable = []; // Chunk positions that are within draw distance. (Master queue)
    private readonly Queue<int> _loadable = []; // Chunk positions that are within draw distance, but not loaded.
    private readonly Queue<int> _freeable = []; // Chunk positions that are loaded, but outside of draw distance.
    private readonly Queue<int> _meshable = []; // Chunk positions that are meshable.

    #endregion Variables

    public override void _Ready()
    {
        // This has to be reset on scene entry as it always saves true on scene exit.
        Generated = false;

        // Ensure world generates around the focus node on scene entry.
        TryUpdateFocusNodeTruePosition();
        TryUpdateFocusNodeGridPosition();
        TryUpdateFocusNodeTrueBasisZ();
        TryUpdateFocusNodeSignBasisZ();
        
        // Preload chunks when playing the game, disable when in the editor for faster scene loading.
        if (Engine.IsEditorHint() == false)
        {
            QueueDrawable();
            QueueLoadable();
            
            LoadQueued();
            
            QueueMeshable();
            MeshQueued();

            Generated = true;
        }
        
        // Secondary threads to handle chunk queueing and meshing seperately.
        Thread chunkDataThread = new(new ThreadStart(DataProcess)) { Name = "Chunk Data Thread" };
        Thread chunkMeshThread = new(new ThreadStart(MeshProcess)) { Name = "Chunk Mesh Thread" };
        
        // Start your engines.
        chunkDataThread.Start();
        chunkMeshThread.Start();
    }
    public override void _PhysicsProcess(double delta)
    {
        TryUpdateFocusNodeTruePosition();
        TryUpdateFocusNodeTrueBasisZ();
    }
    
    private void DataProcess()
    {
        GD.Print("Started Chunk Data Thread.");

        while (IsInstanceValid(this))
        {
            if (!Generated)
            {
                TryUpdateFocusNodeGridPosition();

                FreeLoaded(); 
                
                QueueDrawable();
                QueueLoadable();
                
                LoadQueued();

                Generated = true;
            }
            else if (TryUpdateFocusNodeGridPosition())
            {
                QueueDrawable();
                QueueLoadable();
                QueueFreeable();
                
                WrapQueued();
            }

            Thread.Sleep(100);
        }
    }
    private void MeshProcess()
    {
        GD.Print("Started Chunk Mesh Thread.");

        while (IsInstanceValid(this))
        {  
            TryUpdateFocusNodeSignBasisZ();
                
            QueueMeshable();
            MeshQueued();

            Thread.Sleep(100);
        }
    }

    private void TryUpdateFocusNodeTruePosition() // Update stored focus node world position.
    {
        lock (_focusNodePositionLock)
        {
            if (FocusNode == null)
            {
                _focusNodeTruePosition = Vector3I.Zero;
            }
            else
            {
                _focusNodeTruePosition = (Vector3I)FocusNode.Position.Floor();
            }
        }
    }
    private bool TryUpdateFocusNodeGridPosition() // Update stored focus node chunk position.
    {
        Vector3I queriedFocusNodeChunkPosition;

        lock (_focusNodePositionLock)
        {
            
            queriedFocusNodeChunkPosition = (Vector3I)(_focusNodeTruePosition / ChunkDiameter).Floor();
        }

        if (_focusNodeGridPosition != queriedFocusNodeChunkPosition)
        {
            _focusNodeGridPosition = queriedFocusNodeChunkPosition;

            GD.PrintT("Focus Node Grid Position: ", _focusNodeGridPosition);

            return true;
        }

        return false;
    }
    private void TryUpdateFocusNodeTrueBasisZ()   // Update stored focus node true basis z.
    {
        if (FocusNode == null) return;
        
        lock (_focusNodeBasisZLock)
        {
            _focusNodeTrueBasisZ = FocusNode.Basis.Z;
        }
    }
    private bool TryUpdateFocusNodeSignBasisZ()   // Update stored focus node sign basis z.
    {
        if (FocusNode == null) return false;

        Vector3I queriedfocusNodeChunkBasisZ;

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

            GD.PrintT("Focus Node Sign BasisZ: ", _focusNodeSignBasisZ);
            
            return true;
        }

        return false;
    }
        
    private void QueueDrawable() // Queue chunks that are within draw distance.
    {
        _drawable.Clear();

        for (int x = -DrawRadius.X; x < DrawRadius.X; x++)
        {
            for (int y = -DrawRadius.Y; y < DrawRadius.Y; y++)
            {
                for (int z = -DrawRadius.Z; z < DrawRadius.Z; z++)
                {
                    // Calculate chunk position around focus node.
                    Vector3I chunkGridPosition = new Vector3I(x, y, z) + _focusNodeGridPosition;

                    // Skip if chunk position is outside of world boundaries. (Chunk should not exist)
                    if (chunkGridPosition < -WorldRadius || chunkGridPosition > WorldRadius) continue;

                    // Pack chunk position into an integer, offset to signed coordinates using world radius.
                    int drawableGridIndex = XYZConvert.Vector3IToIndex(chunkGridPosition + WorldRadius, WorldDiameter);
                    
                    // Draw me like one of your French girls.
                    _drawable.Enqueue(drawableGridIndex);
                }
            }
        }

        GD.Print("Drawable Chunks: ", _drawable.Count);
    }
    private void QueueLoadable() // Queue chunks that are within draw distance, but not loaded.
    {
        foreach (int drawableGridIndex in _drawable)
        {
            // Skip if chunk is loaded.
            if (_chunks.ContainsKey(drawableGridIndex)) continue;
                
            // I'm not loaded, you're loaded.
            _loadable.Enqueue(drawableGridIndex);
        }

        GD.Print("Loadable Chunks: ", _loadable.Count);
    }
    private void QueueFreeable() // Queue chunks that are loaded, but outside of draw distance.
    {
        foreach (int chunkGridIndex in _chunks.Keys)
        {
            // Skip if chunk is drawable.
            if (_drawable.Contains(chunkGridIndex)) continue;
            
            // Never felt so free.
            _freeable.Enqueue(chunkGridIndex);
        }

        GD.Print("Freeable Chunks: ", _freeable.Count);
    }
    private void QueueMeshable() // Queue chunks that are loaded, composed, and are in frustum.
    {
        foreach (int chunkGridIndex in _chunks.Keys)
        {
            // Retrieve chunk.
            _chunks.TryGetValue(chunkGridIndex, out Chunk chunk);

            // Skip if chunk is outside of frustum.
            if ((chunk.States & (byte)Chunk.State.Cullable) == (byte)Chunk.State.Cullable) continue;

            // Skip if chunk is not composed.
            if ((chunk.States & (byte)Chunk.State.Composed) != (byte)Chunk.State.Composed) continue;

            // Skip if chunk is already meshed.
            if ((chunk.States & (byte)Chunk.State.Complete) == (byte)Chunk.State.Complete) continue;
                
            // Don't mesh thish up.
            _meshable.Enqueue(chunkGridIndex);
        }
    }

    private void LoadQueued() // Load chunks into the scene tree.
    {
        foreach (int loadableIndex in _loadable)
        {
            Chunk chunk = new();

            _chunks.TryAdd(loadableIndex, chunk);

            CallDeferred(Node.MethodName.AddChild, chunk);

            CallDeferred(nameof(GenerateChunkData), loadableIndex, chunk, WorldSettings);
            
            Thread.Sleep(GenerateFrequency);
        }

        _loadable.Clear();
    }
    private void WrapQueued() // Wrap chunks back into draw range.
    {
        foreach (int freeableIndex in _freeable)
        {
            _chunks.TryRemove(freeableIndex, out Chunk chunk);

            int loadableIndex = _loadable.Dequeue();

            _chunks.TryAdd(loadableIndex, chunk);            

            CallDeferred(nameof(GenerateChunkData), loadableIndex, chunk, WorldSettings);
            
            Thread.Sleep(GenerateFrequency);
        }

        _freeable.Clear();
    }    
    private void MeshQueued() // Mesh chunks.
    {
        foreach (int meshableIndex in _meshable)
        {
            // Retrieve chunk.
            _chunks.TryGetValue(meshableIndex, out Chunk chunk);

            // Generate chunk mesh.
            CallDeferred(nameof(GenerateChunkMesh), chunk);

            Thread.Sleep(10);
        }

        _meshable.Clear();
    }
    
    private void FreeLoaded() // Free chunks from the scene tree.
    {
        foreach (int loadedIndex in _chunks.Keys)
        {
            _chunks.TryRemove(loadedIndex, out Chunk chunk);

            chunk.QueueFree();
        }
    }

    public void GenerateChunkData(int chunkGridIndex, Chunk chunk, WorldSettings worldSettings)
    {
        chunk.States = 0;

        // Calculate chunk position.
        Vector3I chunkGridPosition = Chunk.GetGridPosition(chunkGridIndex, WorldRadius, WorldDiameter);
        Vector3I chunkTruePosition = Chunk.GetTruePosition(chunkGridPosition, ChunkBitshifts);
        
        // Set chunk position.
        chunk.Position = chunkTruePosition;

        // FIXME - This is fast for now, but probably dreadful when there'a a lot of biomes.
        Biome biome = Biome.Generate(chunkGridPosition, WorldDiameter, worldSettings);
        
        // FIXME - Needs to be a LOT faster. Start in reverse and figure out how to send less data.
        chunk.GenerateVoxels(chunkTruePosition, ChunkBitshifts, ChunkVoxelCount, biome, worldSettings);
    }
    public void GenerateChunkMesh(Chunk chunk)
    {
        // Generate mesh surfaces. Each of the six surfaces contains vertex, and index data for each axis sign. [X-, X+, Y-, Y+, Z-, Z+]
        Surface[] surfaces = BinaryMesher.GenerateSurfaces(ref chunk.VoxelTypes, ChunkDiameter, ChunkBitshifts, _focusNodeSignBasisZ, CullGeometry);
        
        // Generate mesh.
        chunk.Mesh = MeshHelper.GenerateMesh(surfaces, TerrainMaterial);
        
        // Clear collision if any.
        chunk.GetChildOrNull<StaticBody3D>(0)?.QueueFree();

        // Create new collision.
        chunk.CreateTrimeshCollision();

        // Show chunk.
        chunk.Visible = true;
        
        // Mark chunk state as complete.
        chunk.States |= (byte)Chunk.State.Complete;
    }

    public override string[] _GetConfigurationWarnings() // Godot specific configuration warnings.
    {
        if (FocusNode == null)
        {
            return
            [
                "A Focus Node must be selected in the inspector to generate the world around."
            ];
        }

        if (Camera == null)
        {
            return
            [
                "A Camera3D Node must be selected in the inspector to generate the world around."
            ];
        }

        return [];
    }
}
