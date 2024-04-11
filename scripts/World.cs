using Godot;
using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

namespace RAWVoxel
{
    [Tool]
    public partial class World : Node3D
    {
        #region Exports
        
        #region Exports -> FocusNode

        [ExportCategory("World")]
        [Export] public Node3D FocusNode
        {
            get { return focusNode; }
            set { focusNode = value; /* GenerateWorld(); */ }
        }
        private Node3D focusNode;
        
        #endregion Exports -> FocusNode

        #region Exports -> Rendering

        [ExportGroup("Rendering")]
        [Export] public int DrawDistance
        {
            get { return drawDistance; }
            set { drawDistance = value;  /* GenerateWorld(); */ }
        }
        private static int drawDistance = 1;
        [Export] public bool ShowChunkEdges
        {
            get { return showChunkEdges; }
            set { showChunkEdges = value; }
        }
        private static bool showChunkEdges = true;
        [Export] public bool UseThreading
        {
            get { return useThreading; }
            set { useThreading = value; /* GenerateWorld(); */ }
        }
        private static bool useThreading = true;
        
        #endregion Exports -> Rendering

        #region Exports -> World
        
        [ExportGroup("World Settings")]
        [Export] public Vector3I WorldDimension
        {
            get { return worldDimension; }
            set { worldDimension = value; }
        }
        private static Vector3I worldDimension = new (128, 128, 128);
        
        #endregion Exports -> World

        #region Exports -> Chunk

        [ExportGroup("Chunk Settings")]
        [Export] public Vector3I ChunkDimension
        {
            get { return chunkDimension; }
            set { chunkDimension = value; }
        }
        private static Vector3I chunkDimension = new (16, 256, 16);
        [Export] public float VoxelSize
        {
            get { return voxelSize; }
            set { voxelSize = value; }
        }
        private static float voxelSize = 1;
        
        #endregion Exports -> Chunk

        #region Exports -> Height

        // Heights used to offset surface terrain generation.
        [ExportGroup("Layer Height")]
        [Export] public int SurfaceHeight
        {
            get { return surfaceHeight; }
            set { surfaceHeight = value; }
        }
        private static int surfaceHeight = 128;
        [Export] public int Layer2Height
        {
            get { return layer2Height; }
            set { layer2Height = value; }
        }
        private static int layer2Height = 96;
        [Export] public int Layer1Height
        {
            get { return layer1Height; }
            set { layer1Height = value; }
        }
        private static int layer1Height = 64;
        [Export] public int BedrockHeight
        {
            get { return bedrockHeight; }
            set { bedrockHeight = value; }
        }
        private static int bedrockHeight = 0;

        #endregion Exports -> Height
        
        #region Exports -> Noise

        [ExportGroup("Noise Maps")]
        // Noise map used for height generation. Controls y value for terrain generation.
        [Export] public FastNoiseLite SurfaceNoise
        {
            get { return surfaceNoise; }
            set { surfaceNoise = value; }
        }
        private static FastNoiseLite surfaceNoise = GD.Load<FastNoiseLite>("res://addons/raw_voxel/resources/world/surface_noise.tres");
        // Noise map used for density generation. Controls y value for terrain generation.
        [Export] public FastNoiseLite DensityNoise
        {
            get { return densityNoise; }
            set { densityNoise = value; }
        }
        private static FastNoiseLite densityNoise = GD.Load<FastNoiseLite>("res://addons/raw_voxel/resources/world/density_noise.tres");
        // Noise map used for humidity generation. Controls x value for terrain generation.
        [Export] public FastNoiseLite HumidityNoise
        {
            get { return humidityNoise; }
            set { humidityNoise = value; }
        }
        private static FastNoiseLite humidityNoise = GD.Load<FastNoiseLite>("res://addons/raw_voxel/resources/world/humidity_noise.tres");
        // Noise map used for temperature generation. Controls z value for terrain generation.
        [Export] public FastNoiseLite TemperatureNoise
        {
            get { return temperatureNoise; }
            set { temperatureNoise = value; }
        }
        private static FastNoiseLite temperatureNoise = GD.Load<FastNoiseLite>("res://addons/raw_voxel/resources/world/temperature_noise.tres");

        #endregion Exports -> Noise
        
        #region Exports -> Curves

        [ExportGroup("Noise Curves")]
        // Controls surface distribution.
        [Export] public Curve SurfaceCurve
        {
            get { return surfaceCurve; }
            set { surfaceCurve = value; }
        }
        private static Curve surfaceCurve = GD.Load<Curve>("res://addons/raw_voxel/resources/world/surface_curve.tres");
        // Controls density distribution.
        [Export] public Curve DensityCurve
        {
            get { return densityCurve; }
            set { densityCurve = value; }
        }
        private static Curve densityCurve = GD.Load<Curve>("res://addons/raw_voxel/resources/world/density_curve.tres");
        // Controls humidity distribution.
        [Export] public Curve HumidityCurve
        {
            get { return humidityCurve; }
            set { humidityCurve = value; }
        }
        private static Curve humidityCurve = GD.Load<Curve>("res://addons/raw_voxel/resources/world/humidity_curve.tres");
        // Controls temperature distribution.
        [Export] public Curve TemperatureCurve
        {
            get { return temperatureCurve; }
            set { temperatureCurve = value; }
        }
        private static Curve temperatureCurve = GD.Load<Curve>("res://addons/raw_voxel/resources/world/temperature_curve.tres");

        #endregion Exports -> Curves

        #endregion Exports
        
        #region Variables

        #region Variables -> FocusNode
        
        private Vector3 focusNodePosition = Vector3.Zero;
        private Vector2I focusNodeChunkPosition = Vector2I.MinValue;
        private readonly object focusNodeChunkPositionLock = new();
        
        #endregion Variables -> FocusNode

        #region Variables -> Queues

        private bool worldGenerated = false;
        private readonly List<Vector2I> drawableChunkPositions = new();
        private readonly List<Vector2I> loadableChunkPositions = new();
        private readonly List<Vector2I> freeableChunkPositions = new();
        private readonly Dictionary<Vector2I, Chunk> loadedChunks = new();

        #endregion Variables -> Queues
        
        #region Variables -> Utilities

        private readonly Stopwatch stopwatch = new();

        #endregion Variables -> Utilities

        #endregion Variables
        
        #region Functions

        #region Functions -> Processes

        public override void _Ready()
        {
            GenerateWorld();
            
            if (useThreading == false) return;
            
            // Start the secondary thread, running UpdateWorldProcess.
            ThreadStart UpdateWorldProcessStart = new ThreadStart(UpdateWorldProcess);
            Thread UpdateWorldThread = new Thread(UpdateWorldProcessStart);
            UpdateWorldThread.Name = "UpdateWorldThread";
            UpdateWorldThread.Start();
        }
        public override void _PhysicsProcess(double delta)
        {
            if (worldGenerated == false) return;
            
            UpdateFocusNodePosition();
            
            if (useThreading == true) return;
            
            if (UpdateFocusNodeChunkPosition() == true)
            {
                UpdateWorld();
            }
        }
        private void UpdateWorldProcess()
        {
            while (IsInstanceValid(this) == true)
            {
                if (UpdateFocusNodeChunkPosition() == true)
                {
                    UpdateWorld();
                }
                
                Thread.Sleep(100);
            }
        }
        
        #endregion Functions -> Processes

        #region Functions -> FocusNode

        // Set currentFocusNodeChunkPosition to queriedFocusNodeChunkPosition if not equal.
        private void UpdateFocusNodePosition()
        {
            // Lock for primary thread access.
            lock (focusNodeChunkPositionLock)
            {
                focusNodePosition = focusNode.Position;
            }
        }
        // Set lastKnownFocusNodeChunkPosition to currentFocusNodeChunkPosition if not equal.
        private bool UpdateFocusNodeChunkPosition()
        {
            bool updated = false;
            
            Vector2I queriedFocusNodeChunkPosition;
            
            // Lock for secondary thread access.
            lock (focusNodeChunkPositionLock)
            {
                
                // Calculate queriedFocusNodeChunkPosition for the current frame. Convert Vector3 to Vector2I.
                queriedFocusNodeChunkPosition.X = (int)MathF.Floor(focusNodePosition.X / chunkDimension.X);
                queriedFocusNodeChunkPosition.Y = (int)MathF.Floor(focusNodePosition.Z / chunkDimension.Z);   
            }
                
            // Check to see if focusNode has a new chunk position. If true, update focusNodeChunkPosition and return.
            if (focusNodeChunkPosition != queriedFocusNodeChunkPosition)
            {
                focusNodeChunkPosition = queriedFocusNodeChunkPosition;
                
                Console.WriteLine("Focus node chunk position updated: " + focusNodeChunkPosition.ToString());
                updated = true;
            }

            return updated;
        }
        
        #endregion Functions -> FocusNode

        #region Functions -> World

        // Queue, load, and free chunks to and from the scene tree.
        private void GenerateWorld()
        {   
            UpdateFocusNodePosition();
            UpdateFocusNodeChunkPosition();
            
            Console.WriteLine();
            Console.WriteLine("--- Generating World ---");

            QueueChunkPositions();
            LoadQueuedChunks();
            FreeQueuedChunks();

            worldGenerated = true;

            Console.WriteLine("--- World Generated ---");
        }
        // Reposition chunks at freeableChunkPositions to loadableChunkPositions.
        private void UpdateWorld()
        {
            Console.WriteLine();
            Console.WriteLine("--- Updating World ---");

            QueueChunkPositions();
            RecycleQueuedChunks();

            Console.WriteLine("--- World Updated ---");
        }
        
        #endregion Functions -> World

        #region Functions -> Queues

        // Call all chunk position queueing methods.
        private void QueueChunkPositions()
        {
            stopwatch.Reset();
            stopwatch.Start();

            QueueDrawableChunkPositions();
            QueueLoadableChunkPositions();
            QueueFreeableChunkPositions();

            stopwatch.Stop();
            Console.WriteLine(nameof(QueueChunkPositions) + " completed in " + stopwatch.ElapsedMilliseconds + " ms.");
        }
        // Queue a new Vector2I into its respective List for each drawable chunk position. Called by QueueChunkPositions().
        private void QueueDrawableChunkPositions()
        {
            drawableChunkPositions.Clear();

            Vector2I drawableCenter = focusNodeChunkPosition;
            
            int drawableXMin = drawableCenter.X - drawDistance;
            int drawableXMax = drawableCenter.X + drawDistance;
            
            int drawableYMin = drawableCenter.Y - drawDistance;
            int drawableYMax = drawableCenter.Y + drawDistance;
            
            for (int x = drawableXMin; x <= drawableXMax; x++)
            {
                for (int y = drawableYMin; y <= drawableYMax; y++)
                {
                    drawableChunkPositions.Add(new Vector2I(x, y));
                }
            }

            Console.WriteLine("Drawable chunks: " + drawableChunkPositions.Count);
        }
        // Queue a new Vector2I into its respective List for each loadable chunk position. Called by QueueChunkPositions().
        private void QueueLoadableChunkPositions()
        {
            if (drawableChunkPositions.Count == 0) return;

            foreach (Vector2I chunkPosition in drawableChunkPositions)
            {
                if (loadedChunks.ContainsKey(chunkPosition) == false)
                {
                    loadableChunkPositions.Add(chunkPosition);
                }
            }

            Console.WriteLine("Loadable chunks: " + loadableChunkPositions.Count);
        }
        // Queue a new Vector2I into its respective List for each freeable chunk position. Called by QueueChunkPositions().
        private void QueueFreeableChunkPositions()
        {
            if (loadedChunks.Count == 0) return;
            
            foreach (Vector2I chunkPosition in loadedChunks.Keys)
            {
                if (drawableChunkPositions.Contains(chunkPosition) == false)
                {
                    freeableChunkPositions.Add(chunkPosition);
                }
            }

            Console.WriteLine("Freeable chunks: " + freeableChunkPositions.Count);
        }

        // Load a Chunk instance into the scene tree for each Vector2I in loadableChunkPositions. Called by GenerateWorld().
        private void LoadQueuedChunks()
        {
            if (loadableChunkPositions.Count == 0) return;
            
            stopwatch.Reset();
            stopwatch.Start();

            foreach (Vector2I chunkPosition in loadableChunkPositions)
            {
                Chunk chunk = new(this, chunkPosition);
                
                loadedChunks.Add(chunkPosition, chunk);
                
                CallDeferred(Node.MethodName.AddChild, chunk);
                //chunk.GenerateChunk();
            }
        
            loadableChunkPositions.Clear();

            stopwatch.Stop();
            Console.WriteLine(nameof(LoadQueuedChunks) + " completed in " + stopwatch.ElapsedMilliseconds + " ms.");
        }
        // Free a Chunk instance from the scene tree for each Vector2I in freeableChunkPositions. Called by GenerateWorld().
        private void FreeQueuedChunks()
        {
            if (freeableChunkPositions.Count == 0) return;

            stopwatch.Reset();
            stopwatch.Start();

            foreach (Vector2I chunkPosition in freeableChunkPositions)
            {
                Chunk chunk = loadedChunks[chunkPosition];
                
                loadedChunks.Remove(chunkPosition);
                
                chunk.QueueFree();
            }
        
            freeableChunkPositions.Clear();

            stopwatch.Stop();
            Console.WriteLine(nameof(FreeQueuedChunks) + " completed in " + stopwatch.ElapsedMilliseconds + " ms.");
        }
        // Reposition Chunk instances from freeableChunkPositions to drawableChunkPositions. Called by UpdateWorld().
        private void RecycleQueuedChunks()
        {
            if (freeableChunkPositions.Count == 0) return;
            if (loadableChunkPositions.Count == 0) return;

            stopwatch.Reset();
            stopwatch.Start();

            foreach (Vector2I loadableChunkPosition in loadableChunkPositions)
            {
                Vector2I freeableChunkPosition = freeableChunkPositions.Last();
                Chunk chunk = loadedChunks[freeableChunkPosition];
                
                freeableChunkPositions.Remove(freeableChunkPosition);
                loadedChunks.Remove(freeableChunkPosition);
                loadedChunks.Add(loadableChunkPosition, chunk);

                chunk.CallDeferred(nameof(Chunk.UpdateChunk), loadableChunkPosition);

                Thread.Sleep(100);
            }

            loadableChunkPositions.Clear();

            stopwatch.Stop();
            Console.WriteLine(nameof(RecycleQueuedChunks) + " completed in " + stopwatch.ElapsedMilliseconds + " ms.");
        }

        #endregion Functions -> Queues
        
        #endregion Functions
    }
}
