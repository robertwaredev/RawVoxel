using Godot;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;

namespace RAWVoxel
{
    [Tool]
    public partial class Chunk : MeshInstance3D
    {
        #region Constructor

        public Chunk(World world, Vector3I chunkPosition)
        {
            World = world;
            SetPosition(chunkPosition);
        }

        #endregion Constructor

        #region Variables

        private static World World;
        public Vector3I ChunkPosition;
        private ArrayMesh arrayMesh = new();
        private Godot.Collections.Array surfaceArray = new();
        private readonly List<Vector3> surfaceVertices = new();
        private readonly List<Vector3> surfaceNormals = new();
        private readonly List<Color> surfaceColors = new();
        private readonly List<Vector2> surfaceUVs = new();
        private readonly List<int> surfaceIndices = new();
        private StandardMaterial3D surfaceMaterial = new();
        public Dictionary<Vector3I, Voxel.Type> voxels = new();
        private readonly Stopwatch stopwatch = new();

        #endregion Variables

        #region Functions

        public override void _Ready()
        {
            SetOverrideMaterial(surfaceMaterial);
            SetupSurfaceArray();
            SetMesh();

            GenerateChunk();
        }
        public void SetPosition(Vector3I chunkPosition)
        {
            ChunkPosition = chunkPosition;
            Position = chunkPosition * World.ChunkDimension;
        }
        private void SetupSurfaceArray()
        {
            surfaceArray.Resize((int)Mesh.ArrayType.Max);
        }
        private void SetOverrideMaterial(BaseMaterial3D material3D)
        {
            material3D.VertexColorUseAsAlbedo = true;
            MaterialOverride = material3D;
        }
        private void SetMesh()
        {
            Mesh = arrayMesh;
        }

        public void GenerateChunk()
        {
            Stopwatch generateStopwatch = Stopwatch.StartNew();
            
            Console.WriteLine();
            Console.WriteLine("--- Generating chunk: " + ChunkPosition + " ---");
            
            GenerateVoxels();
            GenerateChunkMeshSurfaceData();
            GenerateMeshSurfaceArray();
            GenerateMeshSurface();
            GenerateCollision();

            generateStopwatch.Stop();
            Console.WriteLine("--- Generated chunk: " + ChunkPosition + " in " + generateStopwatch.ElapsedMilliseconds + " ms. ---");
            Console.WriteLine();
        }
        public void UpdateChunk(Vector3I chunkPosition)
        {
            Stopwatch updateStopwatch = Stopwatch.StartNew();
            
            Console.WriteLine();
            Console.WriteLine("--- Updating chunk: " + ChunkPosition + " ---");
            
            SetPosition(chunkPosition);
            
            ClearChunk();
            
            GenerateChunk();

            updateStopwatch.Stop();
            Console.WriteLine("--- Updated chunk: " + ChunkPosition + " in " + updateStopwatch.ElapsedMilliseconds + " ms. ---");
            Console.WriteLine();
        }
        public void ClearChunk()
        {
            ClearVoxels();
            ClearChunkMeshSurfaceData();
            ClearMeshSurfaceArray();
            ClearMeshSurface();
            ClearCollision();
        }

        private void GenerateVoxels()
        {
            stopwatch.Reset();
            stopwatch.Start();

            for (int x = 0; x < World.ChunkDimension.X; x++)
            {
                for (int y = 0; y < World.ChunkDimension.Y; y++)
                {
                    for (int z = 0; z < World.ChunkDimension.Z; z++)
                    {
                        Vector3I voxelPosition = new(x, y, z);
                        voxels.Add(voxelPosition, GenerateVoxel(voxelPosition));
                    }
                }
            }

            stopwatch.Stop();
            Console.WriteLine(nameof(GenerateVoxels) + " completed in " + stopwatch.ElapsedMilliseconds + " ms.");
        }
        private Voxel.Type GenerateVoxel(Vector3I voxelPosition)
        {
            // If the voxel being generated is not in this chunk and we want to show chunk edges, assume it's air and return early.
            if (World.ShowChunkEdges == true && CheckVoxelOutOfBounds(voxelPosition) == true) return Voxel.Type.Air;
            
            Vector3I globalVoxelPosition = voxelPosition + (Vector3I)Position;
            // Sample voxel density chance.
            float densityNoise3D = World.DensityNoise.GetNoise3Dv(globalVoxelPosition);
            float densityCurve3D = World.DensityCurve.Sample((densityNoise3D + 1) * 0.5f);
            if (densityCurve3D < 0.5) return Voxel.Type.Air;

            // Sample noise value for surface using the voxel's global position.
            float surfaceNoise = World.SurfaceNoise.GetNoise2D(voxelPosition.X + Position.X, voxelPosition.Z + Position.Z);
            float surfaceCurve = World.SurfaceCurve.Sample((surfaceNoise + 1) * 0.5f);


            // Switch voxel type based on the generated surface value.
            return globalVoxelPosition.Y switch
            {
                (0) => Voxel.Type.Bedrock,
                (> 0) when globalVoxelPosition.Y <  World.BedrockHeight + (int)(surfaceCurve * World.BedrockHeight) => Voxel.Type.Bedrock,
                (> 0) when globalVoxelPosition.Y <  World.Layer2Height  + (int)(surfaceCurve * World.Layer2Height)  => Voxel.Type.Stone,
                (> 0) when globalVoxelPosition.Y <  World.SurfaceHeight + (int)(surfaceCurve * World.SurfaceHeight) => Voxel.Type.Dirt,
                (> 0) when globalVoxelPosition.Y == World.SurfaceHeight + (int)(surfaceCurve * World.SurfaceHeight) => Voxel.Type.Grass,
                _ => Voxel.Type.Air,
            };
            
        }
        private void ClearVoxels()
        {
            voxels.Clear();
        }
        
        private Voxel.Type GetVoxel(Vector3I voxelPosition)
        {
            if (CheckVoxelOutOfBounds(voxelPosition))
            {
                return GenerateVoxel(voxelPosition);
            }

            return voxels[voxelPosition];
        }
        private static bool CheckVoxelOutOfBounds(Vector3I voxelPosition)
        {
            if (voxelPosition.X < 0 || voxelPosition.X >= World.ChunkDimension.X)
            {
                return true;
            }
            if (voxelPosition.Y < 0 || voxelPosition.Y >= World.ChunkDimension.Y)
            {
                return true;
            }
            if (voxelPosition.Z < 0 || voxelPosition.Z >= World.ChunkDimension.Z)
            {
                return true;
            }

            return false;
        }
        
        private void GenerateChunkMeshSurfaceData()
        {
            stopwatch.Reset();
            stopwatch.Start();

            foreach (Vector3I voxelPosition in voxels.Keys)
            {
                GenerateVoxelMeshSurfaceData(voxelPosition);
            }

            stopwatch.Stop();
            Console.WriteLine(nameof(GenerateChunkMeshSurfaceData) + " completed in " + stopwatch.ElapsedMilliseconds + " ms.");
        }
        private void GenerateVoxelMeshSurfaceData(Vector3I voxelPosition)
        {
            Voxel.Type type = GetVoxel(voxelPosition);
            System.Drawing.Color c = System.Drawing.Color.FromKnownColor(Voxel.Colors[type]);
            Color color = new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

            if (type == Voxel.Type.Air) { return; }

            if (GetVoxel(voxelPosition + Vector3I.Up) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.Top, voxelPosition, color);
            }
            if (GetVoxel(voxelPosition + Vector3I.Down) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.Btm, voxelPosition, color);
            }
            if (GetVoxel(voxelPosition + Vector3I.Left) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.West, voxelPosition, color);
            }
            if (GetVoxel(voxelPosition + Vector3I.Right) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.East, voxelPosition, color);
            }
            if (GetVoxel(voxelPosition + Vector3I.Forward) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.North, voxelPosition, color);
            }
            if (GetVoxel(voxelPosition + Vector3I.Back) == Voxel.Type.Air)
            {
                GenerateFaceMeshSurfaceData(Voxel.Face.South, voxelPosition, color);
            }
        }
        private void GenerateFaceMeshSurfaceData(Voxel.Face face, Vector3I voxelPosition, Color color)
        {
            // Assign vertices for the specified face.
            Vector3I vertexA = Voxel.Vertices[Voxel.Faces[face][0]] + voxelPosition;
            Vector3I vertexB = Voxel.Vertices[Voxel.Faces[face][1]] + voxelPosition;
            Vector3I vertexC = Voxel.Vertices[Voxel.Faces[face][2]] + voxelPosition;
            Vector3I vertexD = Voxel.Vertices[Voxel.Faces[face][3]] + voxelPosition;

            // Create normal direction placeholder.
            Vector3I normal = new();

            // Assign normal direction for the specified face.
            switch (face)
            {
                case Voxel.Face.Top: normal = Vector3I.Up; break;
                case Voxel.Face.Btm: normal = Vector3I.Down; break;
                case Voxel.Face.West: normal = Vector3I.Left; break;
                case Voxel.Face.East: normal = Vector3I.Right; break;
                case Voxel.Face.North: normal = Vector3I.Forward; break;
                case Voxel.Face.South: normal = Vector3I.Back; break;

                default: break;
            }

            // Assign UVs for the specified face.
            // Vector2I uvA = Voxel.UVs[Voxel.UV.TopLeft];
            // Vector2I uvB = Voxel.UVs[Voxel.UV.BtmLeft];
            // Vector2I uvC = Voxel.UVs[Voxel.UV.BtmRight];
            // Vector2I uvD = Voxel.UVs[Voxel.UV.TopRight];

            // Get the offset for indices pointers.
            int offset = surfaceVertices.Count;

            // Add surface data to their respective lists.
            surfaceVertices.AddRange(new List<Vector3> { vertexA, vertexB, vertexC, vertexD });
            surfaceNormals.AddRange(new List<Vector3> { normal, normal, normal, normal });
            surfaceColors.AddRange(new List<Color> { color, color, color, color });
            // surfaceUVs.AddRange(new List<Vector2> {uvA, uvB, uvC, uvD});
            surfaceIndices.AddRange(new List<int> { 0 + offset, 1 + offset, 2 + offset, 0 + offset, 2 + offset, 3 + offset });
        }
        private void ClearChunkMeshSurfaceData()
        {
            if (surfaceVertices.Count == 0) return;
            if (surfaceNormals.Count == 0)  return;
            if (surfaceColors.Count == 0)   return;
            //if (surfaceUVs.Count == 0)      return;
            if (surfaceIndices.Count == 0)  return;
            
            surfaceVertices.Clear();
            surfaceNormals.Clear();
            surfaceColors.Clear();
            //surfaceUVs.Clear();
            surfaceIndices.Clear();
        }

        private void GenerateMeshSurfaceArray()
        {
            if (surfaceVertices.Count == 0) { surfaceArray.Clear(); return; }
            if (surfaceNormals.Count == 0)  { surfaceArray.Clear(); return; }
            if (surfaceColors.Count == 0)   { surfaceArray.Clear(); return; }
            if (surfaceIndices.Count == 0)  { surfaceArray.Clear(); return; }

            stopwatch.Reset();
            stopwatch.Start();

            surfaceArray[(int)Mesh.ArrayType.Vertex] = surfaceVertices.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Normal] = surfaceNormals.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Color] = surfaceColors.ToArray();
            //surfaceArray[(int)Mesh.ArrayType.TexUV] = surfaceUVs.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Index] = surfaceIndices.ToArray();

            stopwatch.Stop();
            Console.WriteLine(nameof(GenerateMeshSurfaceArray) + " completed in " + stopwatch.ElapsedMilliseconds + " ms.");
        }
        private void ClearMeshSurfaceArray()
        {
            if (surfaceArray.Count == 0) return;

            surfaceArray.Clear();
            SetupSurfaceArray();
        }

        private void GenerateMeshSurface()
        {
            if (surfaceArray.Count == 0)
            {
                SetupSurfaceArray();
                return;
            }

            stopwatch.Reset();
            stopwatch.Start();

            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray);

            stopwatch.Stop();
            Console.WriteLine(nameof(GenerateMeshSurface) + " completed in " + stopwatch.ElapsedMilliseconds + " ms.");
        }
        private void ClearMeshSurface()
        {
            if (arrayMesh.GetSurfaceCount() == 0) return;

            arrayMesh.ClearSurfaces();
        }

        private void GenerateCollision()
        {
            if (arrayMesh.GetSurfaceCount() == 0) return;

            stopwatch.Reset();
            stopwatch.Start();

            CreateTrimeshCollision();
            AddToGroup("NavSource");

            stopwatch.Stop();
            Console.WriteLine(nameof(GenerateCollision) + " completed in " + stopwatch.ElapsedMilliseconds + " ms.");
        }
        private void ClearCollision()
        {
            StaticBody3D collision = GetChildOrNull<StaticBody3D>(0);
            
            collision?.QueueFree();
        }

        #endregion Functions
    }
}
