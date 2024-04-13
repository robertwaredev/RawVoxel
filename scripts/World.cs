using Godot;
using System;
using RawUtils;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

namespace RAWVoxel
{
    [Tool]
    public partial class World : Node3D
    {
        #region Constructor

        public World() {}

        #endregion Constructor
        
        #region Exports
        
        #region Exports -> Tools

        [Export] public bool Regenerate
        {
            get { return regenerate; }
            set { regenerate = false; GenerateWorld(); }
        }
        private bool regenerate = false;

        #endregion Exports -> Tools

        #region Exports -> FocusNode

        [ExportCategory("Focus Node")]
        
        [Export] public Node3D FocusNode
        {
            get { return focusNode; }
            set { focusNode = value; GenerateWorld(); }
        }
        private Node3D focusNode;
        
        #endregion Exports -> FocusNode

        #region Exports -> Rendering
        [ExportGroup("Rendering")]
        
        [Export] public Vector3I DrawDistance
        {
            get { return drawDistance; }
            set { drawDistance = value; }
        }
        private Vector3I drawDistance = Vector3I.One;
        
        [Export] public bool ShowChunkEdges
        {
            get { return showChunkEdges; }
            set { showChunkEdges = value; }
        }
        private bool showChunkEdges = true;
        
        #endregion Exports -> Rendering

        #region Exports -> Threading
        [ExportGroup("Threading")]
        
        [Export] public bool Threading
        {
            get { return threading; }
            set { threading = value; }
        }
        private bool threading = true;
        
        [Export] public int UpdateFrequency
        {
            get { return updateFrequency; }
            set { updateFrequency = value; }
        }
        private int updateFrequency = 100;

        #endregion Exports -> Threading

        #region Exports -> World
        [ExportGroup("World Settings")]
        
        [Export] public Vector3I WorldDimension
        {
            get { return worldDimension; }
            set { worldDimension = value; }
        }
        private Vector3I worldDimension = new (128, 1, 128);
        
        [Export] public Vector3I ChunkDimension
        {
            get { return chunkDimension; }
            set { chunkDimension = value; }
        }
        private Vector3I chunkDimension = new (16, 256, 16);
        
        [Export] public float VoxelDimension
        {
            get { return voxelDimension; }
            set { voxelDimension = value; }
        }
        private float voxelDimension = 1;
        
        #endregion Exports -> World

        #region Exports -> Height
        [ExportGroup("Layer Height")]
        
        [Export] public int SurfaceHeight
        {
            get { return surfaceHeight; }
            set { surfaceHeight = value; }
        }
        private int surfaceHeight = 128;
        
        [Export] public int Layer2Height
        {
            get { return layer2Height; }
            set { layer2Height = value; }
        }
        private int layer2Height = 96;
        
        [Export] public int Layer1Height
        {
            get { return layer1Height; }
            set { layer1Height = value; }
        }
        private int layer1Height = 64;
        
        [Export] public int BedrockHeight
        {
            get { return bedrockHeight; }
            set { bedrockHeight = value; }
        }
        private int bedrockHeight = 0;

        #endregion Exports -> Height
        
        #region Exports -> Terrain Noise
        [ExportGroup("Terrain Noise")]
        
        // Noise map used for density generation. Controls y value for terrain generation.
        [Export] public FastNoiseLite DensityNoise
        {
            get { return densityNoise; }
            set { densityNoise = value; }
        }
        private FastNoiseLite densityNoise = GD.Load<FastNoiseLite>("res://addons/RawVoxel/resources/world/density_noise.tres");
        
        // Noise map used for height generation. Controls y value for terrain generation.
        [Export] public FastNoiseLite SurfaceNoise
        {
            get { return surfaceNoise; }
            set { surfaceNoise = value; }
        }
        private FastNoiseLite surfaceNoise = GD.Load<FastNoiseLite>("res://addons/RawVoxel/resources/world/surface_noise.tres");
        
        // Noise map used for humidity generation. Controls x value for terrain generation.
        [Export] public FastNoiseLite HumidityNoise
        {
            get { return humidityNoise; }
            set { humidityNoise = value; }
        }
        private FastNoiseLite humidityNoise = GD.Load<FastNoiseLite>("res://addons/RawVoxel/resources/world/humidity_noise.tres");
        
        // Noise map used for temperature generation. Controls z value for terrain generation.
        [Export] public FastNoiseLite TemperatureNoise
        {
            get { return temperatureNoise; }
            set { temperatureNoise = value; }
        }
        private FastNoiseLite temperatureNoise = GD.Load<FastNoiseLite>("res://addons/RawVoxel/resources/world/temperature_noise.tres");

        #endregion Exports -> Terrain Noise
        
        #region Exports -> Terrain Curves
        [ExportGroup("Terrain Curves")]
        // Controls density distribution.
        [Export] public Curve DensityCurve
        {
            get { return densityCurve; }
            set { densityCurve = value; }
        }
        private Curve densityCurve = GD.Load<Curve>("res://addons/RawVoxel/resources/world/density_curve.tres");
        // Controls surface distribution.
        [Export] public Curve SurfaceCurve
        {
            get { return surfaceCurve; }
            set { surfaceCurve = value; }
        }
        private Curve surfaceCurve = GD.Load<Curve>("res://addons/RawVoxel/resources/world/surface_curve.tres");
        // Controls humidity distribution.
        [Export] public Curve HumidityCurve
        {
            get { return humidityCurve; }
            set { humidityCurve = value; }
        }
        private Curve humidityCurve = GD.Load<Curve>("res://addons/RawVoxel/resources/world/humidity_curve.tres");
        // Controls temperature distribution.
        [Export] public Curve TemperatureCurve
        {
            get { return temperatureCurve; }
            set { temperatureCurve = value; }
        }
        private Curve temperatureCurve = GD.Load<Curve>("res://addons/RawVoxel/resources/world/temperature_curve.tres");

        #endregion Exports -> Terrain Curves

        #endregion Exports
        
        #region Variables

        #region Variables -> FocusNode
        
        private Vector3 focusNodePosition;
        private Vector3I focusNodeChunkPosition = Vector3I.MinValue;
        private readonly object focusNodeChunkPositionLock = new();
        
        #endregion Variables -> FocusNode

        #region Variables -> Queues

        private bool worldGenerated = false;
        private readonly List<Vector3I> drawableChunkPositions = new();
        private readonly List<Vector3I> loadableChunkPositions = new();
        private readonly List<Vector3I> freeableChunkPositions = new();
        private readonly Dictionary<Vector3I, Chunk> loadedChunks = new();

        #endregion Variables -> Queues

        #endregion Variables

        #region Functions

        #region Functions -> Processes

        public override void _Ready()
        {
            GenerateWorld();
            
            if (threading)
            {
                ThreadStart UpdateWorldProcessStart = new ThreadStart(UpdateWorldProcess);
                Thread UpdateWorldThread = new Thread(UpdateWorldProcessStart);
                UpdateWorldThread.Name = "UpdateWorldThread";
                UpdateWorldThread.Start();
            }
        }
        public override void _PhysicsProcess(double delta)
        {
            if (worldGenerated) UpdateFocusNodePosition();
            
            if (threading) return;
            
            if (TryUpdateFocusNodeChunkPosition())
            {
                UpdateWorld();
            }
        }
        private void UpdateWorldProcess()
        {
            while (IsInstanceValid(this) && worldGenerated)
            {
                if (TryUpdateFocusNodeChunkPosition())
                {
                    UpdateWorld();
                }
                
                Thread.Sleep(100);
            }
        }
        
        #endregion Functions -> Processes

        #region Functions -> FocusNode

        // Set focusNodeChunkPosition to focusNode.Position. Locked for thread access.
        private void UpdateFocusNodePosition()
        {
            // Lock for primary thread access.
            lock (focusNodeChunkPositionLock)
            {
                focusNodePosition = focusNode.Position;
            }
        }
        // Set focusNodeChunkPosition when focusNode enters a new chunk. Locked for thread access.
        private bool TryUpdateFocusNodeChunkPosition()
        {
            Vector3I queriedFocusNodeChunkPosition;
            
            // Lock for secondary thread access.
            lock (focusNodeChunkPositionLock)
            {
                // Calculate queriedFocusNodeChunkPosition for the current frame.
                queriedFocusNodeChunkPosition = (Vector3I)(focusNodePosition / chunkDimension).Floor();
            }
            
            // Check to see if focusNode has a new chunk position. If true, update focusNodeChunkPosition and return.
            if (focusNodeChunkPosition != queriedFocusNodeChunkPosition)
            {
                focusNodeChunkPosition = queriedFocusNodeChunkPosition;
                
                GD.Print(focusNode.Name + " chunk position updated: " + focusNodeChunkPosition.ToString());
                Console.WriteLine(focusNode.Name + " chunk position updated: " + focusNodeChunkPosition.ToString());
                
                return true;
            }

            return false;
        }
        
        #endregion Functions -> FocusNode

        #region Functions -> World

        // Queue, load, and free chunks to and from the scene tree.
        private void GenerateWorld()
        {   
            worldGenerated = false;
            
            UpdateFocusNodePosition();
            TryUpdateFocusNodeChunkPosition();

            QueueChunkPositions();
            LoadQueuedChunks();
            FreeQueuedChunks();

            worldGenerated = true;
        }
        // Reposition chunks at freeableChunkPositions to loadableChunkPositions.
        private void UpdateWorld()
        {
            QueueChunkPositions();
            RawTimer.Time(RecycleQueuedChunks, RawTimer.AppendLine.Both);
        }
        
        #endregion Functions -> World

        #region Functions -> Queues

        // Call all chunk position queueing methods.
        private void QueueChunkPositions()
        {
            QueueDrawableChunkPositions();
            QueueLoadableChunkPositions();
            QueueFreeableChunkPositions();
        }
        // Queue a new Vector3I into its respective List for each drawable chunk position. Called by QueueChunkPositions().
        private void QueueDrawableChunkPositions()
        {
            drawableChunkPositions.Clear();

            Vector3I drawableCenter = focusNodeChunkPosition;
            
            int drawableXMin = drawableCenter.X - drawDistance.X;
            int drawableXMax = drawableCenter.X + drawDistance.X;
            
            int drawableYMin = drawableCenter.Y - drawDistance.Y;
            int drawableYMax = drawableCenter.Y + drawDistance.Y;

            int drawableZMin = drawableCenter.Z - drawDistance.Z;
            int drawableZMax = drawableCenter.Z + drawDistance.Z;
            
            for (int x = drawableXMin; x <= drawableXMax; x++)
            {
                for (int y = drawableYMin; y <= drawableYMax; y++)
                {
                    for (int z = drawableZMin; z <= drawableZMax; z++)
                    {
                        drawableChunkPositions.Add(new(x, y, z));
                    }
                }
            }

            GD.Print("Drawable chunks: " + drawableChunkPositions.Count);
        }
        // Queue a new Vector3I into its respective List for each loadable chunk position. Called by QueueChunkPositions().
        private void QueueLoadableChunkPositions()
        {
            if (drawableChunkPositions.Count == 0) return;
            
            loadableChunkPositions.Clear();

            foreach (Vector3I chunkPosition in drawableChunkPositions)
            {
                if (loadedChunks.ContainsKey(chunkPosition) == false)
                {
                    loadableChunkPositions.Add(chunkPosition);
                }
            }

            GD.Print("Loadable chunks: " + loadableChunkPositions.Count);
        }
        // Queue a new Vector3I into its respective List for each freeable chunk position. Called by QueueChunkPositions().
        private void QueueFreeableChunkPositions()
        {
            if (loadedChunks.Count == 0) return;
            
            freeableChunkPositions.Clear();

            foreach (Vector3I chunkPosition in loadedChunks.Keys)
            {
                if (drawableChunkPositions.Contains(chunkPosition) == false)
                {
                    freeableChunkPositions.Add(chunkPosition);
                }
            }

            GD.Print("Freeable chunks: " + loadableChunkPositions.Count);
        }

        // Load a Chunk instance into the scene tree for each Vector3I in loadableChunkPositions. Called by GenerateWorld().
        private void LoadQueuedChunks()
        {
            if (loadableChunkPositions.Count == 0) return;
            
            foreach (Vector3I chunkPosition in loadableChunkPositions)
            {
                Chunk chunk = new(this, chunkPosition);
                
                loadedChunks.Add(chunkPosition, chunk);
                
                CallDeferred(Node.MethodName.AddChild, chunk);
                chunk.CallDeferred(nameof(Chunk.GenerateChunk));
            }
        
            loadableChunkPositions.Clear();
        }
        // Free a Chunk instance from the scene tree for each Vector3I in freeableChunkPositions. Called by GenerateWorld().
        private void FreeQueuedChunks()
        {
            if (freeableChunkPositions.Count == 0) return;

            foreach (Vector3I chunkPosition in freeableChunkPositions)
            {
                Chunk chunk = loadedChunks[chunkPosition];
                
                loadedChunks.Remove(chunkPosition);
                
                chunk.QueueFree();
            }
        
            freeableChunkPositions.Clear();
        }
        // Reposition Chunk instances from freeableChunkPositions to drawableChunkPositions. Called by UpdateWorld().
        private void RecycleQueuedChunks()
        {
            if (freeableChunkPositions.Count == 0) return;
            if (loadableChunkPositions.Count == 0) return;

            foreach (Vector3I loadableChunkPosition in loadableChunkPositions)
            {
                Vector3I freeableChunkPosition = freeableChunkPositions.First();
                Chunk chunk = loadedChunks[freeableChunkPosition];
                
                freeableChunkPositions.Remove(freeableChunkPosition);
                loadedChunks.Remove(freeableChunkPosition);
                loadedChunks.Add(loadableChunkPosition, chunk);

                chunk.CallDeferred(nameof(Chunk.UpdateChunk), loadableChunkPosition);

                Thread.Sleep(updateFrequency);
            }

            loadableChunkPositions.Clear();
        }

        #endregion Functions -> Queues
        
        #endregion Functions
    }
}
