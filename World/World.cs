using Godot;
using RawVoxel.Math;
using RawVoxel.Meshing;
using System.Threading;
using RawVoxel.Resources;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace RawVoxel.World;

// TODO - Offset focus node position detection by chunk radius so the load threshold is at chunk center.
// TODO - Optimize mesh generation with the RenderingServer?
// NOTE - Bypassing the scene tree might cause complications for world interactions.
// TODO - Improve draw distance loop to start at player position.

[Tool]
public partial class World() : MeshInstance3D
{
    public enum MeshingAlgorithmType { Simple, Greedy }

    #region Constants

    public const int ChunkRadius = 16;
    public const int ChunkBitshifts = 5;

    #endregion Constants

    #region Exports

    [Export] public bool Generated = false;
    [Export] public Node ChunkBin { get; set; }
    [Export] public Node3D FocusNode { get; set; }
    [Export] public Camera3D Camera { get; set; }
    [Export] public WorldSettings WorldSettings { get; set; }
    [Export] public Material TerrainMaterial { get; set; } = new StandardMaterial3D();
    
    [ExportGroup("Dimensions")]
    private Vector3I _drawRadius = Vector3I.One;
    [Export] public Vector3I DrawRadius
    {
        get => _drawRadius.Clamp(Vector3I.One,  WorldRadius);
        set => _drawRadius = value;
    }
    public Vector3I DrawDiameter
    {
        get => XYZ.LShift(DrawRadius, 1).Clamp(DrawRadius, WorldDiameter);
    }
    public int DrawableChunks
    {
        get => DrawDiameter.X * DrawDiameter.Y * DrawDiameter.Z;
    }

    private Vector3I _collisionRadius = Vector3I.One;
    [Export] public Vector3I CollisionRadius
    {
        get => _collisionRadius.Clamp(Vector3I.One, DrawRadius);
        set => _collisionRadius = value;
    }
    public Vector3I CollisionDiameter
    {
        get => XYZ.LShift(CollisionRadius, 1).Clamp(CollisionRadius, DrawDiameter);
    }
    
    private Vector3I _worldRadius = new(128, 128, 128);
    [Export] public Vector3I WorldRadius
    {
        get => _worldRadius.Clamp(Vector3I.One,  Vector3I.MaxValue / 2);
        set => _worldRadius = value;
    }
    public Vector3I WorldDiameter
    {
        get => XYZ.LShift(WorldRadius, 1).Clamp(WorldRadius, Vector3I.MaxValue);
    }

    [ExportGroup("Meshing")]
    [Export] public MeshingAlgorithmType MeshingAlgorithm = MeshingAlgorithmType.Greedy;

    [ExportGroup("Culling")]
    [Export] public bool CullFrustum = false;
    [Export] public bool CullAxes = false;

    [ExportGroup("Threading")]
    [Export] public int GenerateFrequency = 30;

    #endregion Exports

    #region Variables

    private Vector3 _focusNodeTruePosition; // Physics process only.
    private Vector3I _focusNodeLastSGridPosition = Vector3I.Zero; // Signed position.
    private Vector3I _focusNodeThisSGridPosition = Vector3I.Zero; // Signed position.
    private Vector3I _focusNodeLastUGridPosition = Vector3I.Zero; // Unsigned position.
    private Vector3I _focusNodeThisUGridPosition = Vector3I.Zero; // Unsigned position.
    private readonly object _focusNodePositionLock = new();

    private Vector3 _cameraTrueBasisZ; // Physics process only.
    private Vector3I _cameraSignBasisZ = Vector3I.Zero; // Mesh process only.
    private readonly object _cameraBasisZLock = new();

    private readonly ConcurrentDictionary<int, Chunk> _chunks = new(); // Chunks that are loaded into the scene tree, regardless of state.

    private readonly Queue<KeyValuePair<int, int>> _abstract = []; // Chunk indices that have no voxel data.
    private readonly Queue<KeyValuePair<int, int>> _tangible = []; // Chunk indices that have voxel data.
    private readonly Queue<KeyValuePair<int, int>> _observed = []; // Chunk indices that are within draw distance.
    private readonly Queue<KeyValuePair<int, int>> _collider = []; // Chunk indices that are within collision distance.

    #endregion Variables

    public override void _Ready()
    {
        ThreadPool.SetMinThreads(DrawableChunks * 3, DrawableChunks * 3);
        //ThreadPool.SetMaxThreads(DrawableChunks * 3, DrawableChunks * 3);

        // Set chunk bin to self if nothing else is selected.
        ChunkBin ??= this;

        // This has to be reset on scene entry as it always saves true on scene exit.
        Generated = false;

        // Ensure world generates around the focus node on scene entry.
        TryUpdateFocusNodeTruePosition();
        TryUpdateCameraTrueBasisZ();
        
        // Preload chunks when playing the game, disable when in the editor for faster scene loading.
        /* if (Engine.IsEditorHint() == false)
        {
            TryUpdateFocusNodeGridPosition();
            TryUpdateCameraSignBasisZ();
            
            FreeChunkDictionary();
            FillChunkDictionary();
            
            QualifyChunks();

            //QueueAbstract();
            //QueueCollider();
            //QueueAbstract();
            
            //HandleAbstract();
            
            //QueueTangible();
            //HandleTangible();

            Generated = true;
        } */
        
        // Secondary threads to handle chunk queueing and meshing seperately.
        Thread chunkDataThread = new(new ThreadStart(DataProcess)) { Name = "Chunk Data Thread" };
        Thread chunkMeshThread = new(new ThreadStart(MeshProcess)) { Name = "Chunk Mesh Thread" };
        
        // Start your engines.
        chunkDataThread.Start();
        //chunkMeshThread.Start();
    }
    public override void _PhysicsProcess(double delta)
    {
        TryUpdateFocusNodeTruePosition();
        TryUpdateCameraTrueBasisZ();
    }

    private void DataProcess()
    {
        GD.Print("Started Chunk Data Thread.");

        while (IsInstanceValid(this))
        {
            if (!Generated)
            {
                TryUpdateFocusNodeGridPosition();

                FreeChunkDictionary();
                FillChunkDictionary();

                HandleAbstract();

                Generated = true;
            }
            
            if (TryUpdateFocusNodeGridPosition())
            {
                //LocateAbstract();
            }

            Thread.Sleep(100);
        }
    }
    private void MeshProcess()
    {
        GD.Print("Started Chunk Mesh Thread.");

        while (IsInstanceValid(this))
        {  
            TryUpdateCameraSignBasisZ();
                
            HandleTangible();
            HandleObserved();
        
            Thread.Sleep(100);
        }
    }

    private void TryUpdateFocusNodeTruePosition()
    {
        lock (_focusNodePositionLock)
        {
            if (FocusNode == null)
            {
                _focusNodeTruePosition = Vector3.Zero;
            }
            else
            {
                _focusNodeTruePosition = FocusNode.Position;
            }
        }
    }
    private bool TryUpdateFocusNodeGridPosition()
    {
        Vector3I queriedFocusNodeGridPosition;

        lock (_focusNodePositionLock)
        {
            queriedFocusNodeGridPosition = XYZ.RShift((Vector3I)_focusNodeTruePosition.Floor(), ChunkBitshifts);
        }

        if (Generated == false)
        {
            _focusNodeLastSGridPosition = queriedFocusNodeGridPosition;
            _focusNodeLastUGridPosition = XYZ.Wrap(_focusNodeLastSGridPosition + WorldRadius, WorldDiameter);
            
            _focusNodeThisSGridPosition = queriedFocusNodeGridPosition;
            _focusNodeThisUGridPosition = XYZ.Wrap(_focusNodeThisSGridPosition + WorldRadius, WorldDiameter);

            GD.PrintS("Focus node last signed chunk:", _focusNodeLastSGridPosition);
            GD.PrintS("Focus node last unsigned chunk:", _focusNodeLastSGridPosition);
            GD.PrintS("Focus node this signed chunk:", _focusNodeThisUGridPosition);
            GD.PrintS("Focus node this unsigned chunk:", _focusNodeThisUGridPosition);

            return true;
        }
        
        if (_focusNodeThisSGridPosition != queriedFocusNodeGridPosition)
        {
            _focusNodeLastSGridPosition = _focusNodeThisSGridPosition;
            _focusNodeLastUGridPosition = XYZ.Wrap(_focusNodeLastSGridPosition + WorldRadius, WorldDiameter);

            _focusNodeThisSGridPosition = queriedFocusNodeGridPosition;
            _focusNodeThisUGridPosition = XYZ.Wrap(_focusNodeThisSGridPosition + WorldRadius, WorldDiameter);

            GD.PrintS("Focus node last signed chunk:", _focusNodeLastSGridPosition);
            GD.PrintS("Focus node last unsigned chunk:", _focusNodeLastSGridPosition);
            GD.PrintS("Focus node this signed chunk:", _focusNodeThisUGridPosition);
            GD.PrintS("Focus node this unsigned chunk:", _focusNodeThisUGridPosition);

            return true;
        }
        
        return false;
    }

    private void TryUpdateCameraTrueBasisZ()
    {
        lock (_cameraBasisZLock)
        {
            if (Camera == null)
            {
                _cameraTrueBasisZ = Vector3.Zero;
            }
            else
            {
                _cameraTrueBasisZ = Camera.Basis.Z;
            }
        }
    }
    private bool TryUpdateCameraSignBasisZ()
    {
        Vector3I queriedfocusNodeChunkBasisZ;

        lock (_cameraBasisZLock)
        {
            queriedfocusNodeChunkBasisZ = (Vector3I)_cameraTrueBasisZ.Sign();
        }
        
        if (_cameraSignBasisZ != queriedfocusNodeChunkBasisZ)
        {
            _cameraSignBasisZ = queriedfocusNodeChunkBasisZ;
            
            return true;
        }

        return false;
    }

    private void FreeChunkDictionary()
    {
        foreach (KeyValuePair<int, Chunk> item in _chunks)
        {
            _chunks.TryRemove(item.Key, out Chunk chunk);

            chunk.QueueFree();
        }
    }
    private void FillChunkDictionary()
    {
        if (_chunks.Count == DrawableChunks) return;

        for (int x = 0; x < DrawDiameter.X; x ++)
        {
            for (int y = 0; y < DrawDiameter.Y; y ++)
            {
                for (int z = 0; z < DrawDiameter.Z; z ++)
                {
                    Vector3I chunkGridPosition = new Vector3I(x, y, z) + _focusNodeThisUGridPosition;
                    
                    int chunkGridIndex = XYZ.Encode(chunkGridPosition, WorldDiameter);

                    Chunk chunk = new();

                    _chunks.TryAdd(chunkGridIndex, chunk);
                    
                    ThreadPool.QueueUserWorkItem(new WaitCallback(task => { CallDeferred(Node.MethodName.AddChild, chunk); }));
                    
                    Thread.Sleep(5);

                    GD.PrintS("🟥 Generated chunk item.");
                }
            }
        }
    }

    private void LocateAbstract()
    {
        Vector3I drawOffset = _focusNodeThisSGridPosition - _focusNodeLastSGridPosition;

        Vector3I rangeMin = new()
        {
            X = (drawOffset.X < 0) ? drawOffset.X : 0,
            Y = (drawOffset.Y < 0) ? drawOffset.Y : 0,
            Z = (drawOffset.Z < 0) ? drawOffset.Z : 0,
        };

        Vector3I rangeMax = new()
        {
            X = (drawOffset.X > 0) ? drawOffset.X : (drawOffset.X == 0) ? DrawDiameter.X : 0,
            Y = (drawOffset.Y > 0) ? drawOffset.Y : (drawOffset.Y == 0) ? DrawDiameter.Y : 0,
            Z = (drawOffset.Z > 0) ? drawOffset.Z : (drawOffset.Z == 0) ? DrawDiameter.Z : 0,
        };

        for (int axis = 0; axis < 3; axis ++)
        {
            Vector2I swizzledDrawDiameter = axis switch
            {
                0 => new(DrawDiameter.Y, DrawDiameter.Z),
                1 => new(DrawDiameter.Z, DrawDiameter.X),
                _ => new(DrawDiameter.X, DrawDiameter.Y),
            };

            for (int width = rangeMin[axis]; width < rangeMax[axis]; width ++)
            {
                for (int height = 0; height < swizzledDrawDiameter.X; height ++)
                {
                    for (int depth = 0; depth < swizzledDrawDiameter.Y; depth ++)
                    {
                        Vector3I oldChunkGridPosition = axis switch
                        {
                            0 => XYZ.Wrap(new Vector3I(width, height, depth), DrawDiameter), // X axis.
                            1 => XYZ.Wrap(new Vector3I(height, depth, width), DrawDiameter), // Y axis.
                            _ => XYZ.Wrap(new Vector3I(depth, width, height), DrawDiameter), // Z axis.
                        };

                        Vector3I newChunkGridPosition = axis switch
                        {
                            0 => XYZ.Wrap(new Vector3I(width, height, depth) - drawOffset, DrawDiameter), // X axis.
                            1 => XYZ.Wrap(new Vector3I(height, depth, width) - drawOffset, DrawDiameter), // Y axis.
                            _ => XYZ.Wrap(new Vector3I(depth, width, height) - drawOffset, DrawDiameter), // Z axis.
                        };
                        
                        int oldChunkGridIndex = XYZ.Encode(oldChunkGridPosition, WorldDiameter);
                        int newChunkGridIndex = XYZ.Encode(newChunkGridPosition, WorldDiameter);

                        if (newChunkGridIndex != oldChunkGridIndex)
                        {
                            if (_chunks.TryRemove(oldChunkGridIndex, out Chunk chunk))
                                _chunks.TryAdd(newChunkGridIndex, chunk);
                            
                            chunk.State = Chunk.StateType.Abstract;
                        }
                    }
                }
            }
        }
    }
    private void HandleAbstract()
    {
        foreach (int i in _chunks.Keys)
        {
            if (_chunks.TryGetValue(i, out Chunk chunk) == false) continue;

            if (chunk.State != Chunk.StateType.Abstract) continue;

            chunk.State = Chunk.StateType.Tethered;

            ThreadPool.QueueUserWorkItem(new WaitCallback(task => { GenerateChunkData(chunk, i); }));
        
            Thread.Sleep(10);
        }
    }
    private void HandleTangible()
    {
        foreach (int i in _chunks.Keys)
        {
            if (_chunks.TryGetValue(i, out Chunk chunk) == false) continue;

            if (chunk.State != Chunk.StateType.Tangible) continue;

            chunk.State = Chunk.StateType.Tethered;

            ThreadPool.QueueUserWorkItem(new WaitCallback(task => { CallDeferred(nameof(EvaluateChunkView), chunk); }));

            Thread.Sleep(10);
        }
    }
    private void HandleObserved()
    {
        foreach (int i in _chunks.Keys)
        {
            if (_chunks.TryGetValue(i, out Chunk chunk) == false) continue;

            if (chunk.State != Chunk.StateType.Observed) continue;

            chunk.State = Chunk.StateType.Tethered;

            ThreadPool.QueueUserWorkItem(new WaitCallback(task => { CallDeferred(nameof(GenerateChunkMesh), chunk, i); }));

            Thread.Sleep(10);
        }
    }

    private void GenerateChunkData(Chunk chunk, int chunkGridIndex)
    {
        // Calculate chunk position.
        Vector3I chunkGridPosition = XYZ.Decode(chunkGridIndex, WorldDiameter) - WorldRadius - DrawRadius;
        Vector3I chunkTruePosition = XYZ.LShift(chunkGridPosition, ChunkBitshifts);
        
        // Generate biome.
        Biome biome = Biome.Generate(chunkGridPosition, WorldDiameter, WorldSettings);
        
        // Generate voxels.
        chunk.GenerateVoxels(chunkTruePosition, ChunkBitshifts, biome, WorldSettings);

        // Mark chunk state as tangible.
        if (chunk.VoxelMasks.HasAnySet()) chunk.State = Chunk.StateType.Tangible;

        GD.PrintS("🟧 Generated chunk data.");
    }
    private void EvaluateChunkView(Chunk chunk)
    {
        if (Camera == null || CullFrustum == false)
        {
            chunk.State = Chunk.StateType.Observed;
        }
        else
        {
            Vector3I chunkCenterPosition = (Vector3I)chunk.Position + new Vector3I(ChunkRadius, ChunkRadius, ChunkRadius);
            Vector3I chunkFrustumPosition = chunkCenterPosition - XYZ.LShift(_cameraSignBasisZ, ChunkBitshifts) * 2;

            if (Camera.IsPositionInFrustum(chunkFrustumPosition))
                chunk.State = Chunk.StateType.Observed;
        }
        
        GD.PrintS("🟨 Evaluated chunk view.");
    }
    private void GenerateChunkMesh(Chunk chunk, int chunkGridIndex)
    {
        // Clear chunk collision if any.
        chunk.GetChildOrNull<StaticBody3D>(0)?.QueueFree();

        // Calculate chunk position.
        Vector3I chunkGridPosition = XYZ.Decode(chunkGridIndex, WorldDiameter) - DrawRadius + _focusNodeThisSGridPosition;
        Vector3I chunkTruePosition = XYZ.LShift(chunkGridPosition, ChunkBitshifts);

        // Set chunk position.
        chunk.Position = chunkTruePosition;

        // Generate mesh surfaces. Each of the six surfaces contains vertex, and index data for each axis sign. [X-, X+, Y-, Y+, Z-, Z+]
        Binary.Surface[] surfaces = MeshingAlgorithm switch
        {  
            MeshingAlgorithmType.Simple => CulledMesher.GenerateSurfaces(ref chunk.VoxelMasks, ChunkBitshifts, _cameraSignBasisZ, CullAxes),
            MeshingAlgorithmType.Greedy => Binary.Mesher.GenerateSurfaces(ref chunk.VoxelMasks, ChunkBitshifts, _cameraSignBasisZ, CullAxes),
            _ => []
        };

        // Generate mesh.
        chunk.Mesh = MeshHelper.GenerateMesh(surfaces, chunk, TerrainMaterial);
        
        // Limit collision generation to a radius around the focus node.
        // if (_collider.Contains(chunkGridIndex)) chunk.CreateTrimeshCollision();
        
        // Mark chunk state as rendered.
        chunk.State = Chunk.StateType.Rendered;

        GD.PrintS("🟩 Generated chunk mesh.");
    }

    public override string[] _GetConfigurationWarnings()
    {
        if (FocusNode == null)
        {
            return
            [
                "A Focus Node must be selected in the inspector to generate the world around."
            ];
        }

        if ((CullFrustum || CullAxes) && Camera == null)
        {
            return
            [
                "Culling options are enabled, but no Camera3D has been selected in the inspector!"
            ];
        }

        if (ChunkBin == null)
        {
            return
            [
                "A Node must be selected to generate chunks under!"
            ];
        }

        return [];
    }
}
