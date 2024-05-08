using Godot;
using RawUtils;
using System.Collections;
using System.Collections.Generic;

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class Chunk : MeshInstance3D
    {
        #region Variables

        public World World;
        public Biome Biome;
        public BitArray VoxelBits;
        public byte[] VoxelIDs;
        
        private readonly List<Vector3> _surfaceVertices = new();
        private readonly List<Vector3> _surfaceNormals = new();
        private readonly List<Color> _surfaceColors = new();
        private readonly List<int> _surfaceIndices = new();

        #endregion Variables

        public Chunk(World world)
        {
            World = world;
            MaterialOverride = world.TerrainMaterial;
        }

        // Chunk generation.
        public void GenerateChunkData(Vector3I chunkGridPosition)
        {
            Position = chunkGridPosition * World.ChunkDiameter;
            Biome = Biome.Generate(World, chunkGridPosition);
            
            RawTimer.Time(GenerateVoxels, RawTimer.AppendLine.Pre);
            
            GenerateChunkMesh();
        }
        public void GenerateChunkMesh()
        {
            RawTimer.Time(GenerateChunkMeshData);
            RawTimer.Time(GenerateChunkMeshSurface);
            RawTimer.Time(GenerateChunkMeshCollision, RawTimer.AppendLine.Post);
        }

        // Voxel generation.
        private void GenerateVoxels()
        {
            int voxelCount = World.ChunkDiameter.X * World.ChunkDiameter.Y * World.ChunkDiameter.Z;
            
            VoxelBits = new BitArray(voxelCount);
            
            VoxelIDs = new byte[voxelCount];

            for (int voxelGridIndex = 0; voxelGridIndex < VoxelIDs.Length; voxelGridIndex ++)
            {
                Vector3I voxelGridPosition = XYZConvert.IndexToVector3I(voxelGridIndex, World.ChunkDiameter);
                Vector3I voxelGlobalPosition = (Vector3I)Position + voxelGridPosition;
                
                bool visible = Voxel.GenerateVisibility(Biome, voxelGlobalPosition);
                byte thisVoxelID = (byte)Voxel.GenerateID(World, Biome, voxelGlobalPosition);

                if (visible && thisVoxelID != 0)
                {
                    VoxelIDs[voxelGridIndex] = thisVoxelID;
                    VoxelBits.Set(voxelGridIndex, true);
                }
            }
        }

        // Chunk mesh data generation.
        private void GenerateChunkMeshData()
        {
            // Prevent mesh creation with no voxel IDs.
            if (VoxelIDs.Length == 0) return;
            
            // Generate chunk mesh surface data for visible voxels.
            for (int voxelGridIndex = 0; voxelGridIndex < VoxelBits.Length; voxelGridIndex ++)
            {
                if (VoxelBits[voxelGridIndex])
                    GenerateVoxelMeshData(voxelGridIndex);
            }
        }
        private void GenerateVoxelMeshData(int voxelGridIndex)
        {
            Vector3I voxelGridPosition = XYZConvert.IndexToVector3I(voxelGridIndex, World.ChunkDiameter);
            int voxelID = VoxelIDs[voxelGridIndex];
            Color color = World.Voxels[voxelID].Color;
                
            if (Voxel.IsVisible(this, voxelGridPosition + Vector3I.Up) == false)
                GenerateFaceMeshData(Voxel.Face.Top, Vector3I.Up, color, voxelGridPosition);
            if (Voxel.IsVisible(this, voxelGridPosition + Vector3I.Down) == false)
                GenerateFaceMeshData(Voxel.Face.Btm, Vector3I.Down, color, voxelGridPosition);
            if (Voxel.IsVisible(this, voxelGridPosition + Vector3I.Left) == false)
                GenerateFaceMeshData(Voxel.Face.West, Vector3I.Left, color, voxelGridPosition);
            if (Voxel.IsVisible(this, voxelGridPosition + Vector3I.Right) == false)
                GenerateFaceMeshData(Voxel.Face.East, Vector3I.Right, color, voxelGridPosition);
            if (Voxel.IsVisible(this, voxelGridPosition + Vector3I.Forward) == false)
                GenerateFaceMeshData(Voxel.Face.North, Vector3I.Forward, color, voxelGridPosition);
            if (Voxel.IsVisible(this, voxelGridPosition + Vector3I.Back) == false)
                GenerateFaceMeshData(Voxel.Face.South, Vector3I.Back, color, voxelGridPosition);
        }
        private void GenerateFaceMeshData(Voxel.Face face, Vector3I normal, Color color, Vector3I voxelGridPosition)
        {
            Voxel.Vertex[] faceVertices = Voxel.Faces[face];
            
            Vector3I vertexA = Voxel.Vertices[faceVertices[0]] + voxelGridPosition;
            Vector3I vertexB = Voxel.Vertices[faceVertices[1]] + voxelGridPosition;
            Vector3I vertexC = Voxel.Vertices[faceVertices[2]] + voxelGridPosition;
            Vector3I vertexD = Voxel.Vertices[faceVertices[3]] + voxelGridPosition;

            int offset = _surfaceVertices.Count;

            _surfaceVertices.AddRange(new List<Vector3> { vertexA, vertexB, vertexC, vertexD });
            _surfaceNormals.AddRange(new List<Vector3> { normal, normal, normal, normal });
            _surfaceColors.AddRange(new List<Color> { color, color, color, color });
            _surfaceIndices.AddRange(new List<int> { 0 + offset, 1 + offset, 2 + offset, 0 + offset, 2 + offset, 3 + offset });
        }
        
        // Chunk mesh surface generation.
        private void GenerateChunkMeshSurface()
        {
            // Clear existing mesh.
            if (IsInstanceValid(Mesh)) Mesh = null;
            
            // Prevent mesh creation with no surface data.
            if (_surfaceVertices.Count == 0) return;
            if (_surfaceNormals.Count == 0) return;
            if (_surfaceColors.Count == 0) return;
            if (_surfaceIndices.Count == 0) return;
            
            // Create new surface array.
            Godot.Collections.Array surfaceArray = new();
            surfaceArray.Resize((int)Mesh.ArrayType.Max);

            // Pack surface data into surface array.
            surfaceArray[(int)Mesh.ArrayType.Vertex] = _surfaceVertices.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Normal] = _surfaceNormals.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Color]  = _surfaceColors.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Index]  = _surfaceIndices.ToArray();
            
            // Clear surface data.
            _surfaceVertices.Clear();
            _surfaceNormals.Clear();
            _surfaceColors.Clear();
            _surfaceIndices.Clear();

            // Create new array mesh.
            ArrayMesh arrayMesh = new();

            // Add surface to array mesh using surface array to populate its data.
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray);

            // Clear duplicate surface data.
            surfaceArray.Clear();

            // Set mesh.
            Mesh = arrayMesh;
        }
        
        // Chunk mesh collision generation.
        private void GenerateChunkMeshCollision()
        {
            if (Mesh == null) return;

            StaticBody3D collision = GetChildOrNull<StaticBody3D>(0);
            collision?.QueueFree();

            CreateTrimeshCollision();
            AddToGroup("NavSource");
        }
    }
}
