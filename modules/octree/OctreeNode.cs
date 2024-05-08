using Godot;
using System;
using RawUtils;
using System.Threading;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RawVoxel
{
    public partial class OctreeNode : MeshInstance3D
    {
        #region Variables
        
        public readonly World World;
        
        public readonly byte Branch;

        public Biome Biome;
        
        public OctreeNode[] Leaves;

        public BitArray VoxelBits = new(1);
        
        public byte[] VoxelIDs = new byte[1];

        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Color> _colors = new();
        private readonly List<int> _indices = new();

        #endregion Variables

        public OctreeNode(World world, byte branch)
        {
            World = world;
            Branch = branch;
        }

        public void Generate(Vector3I globalPosition, Biome biome)
        {
            Position = globalPosition;
            Biome = biome;
            
            if (Branch == 0)
            {
                VoxelBits.Set(0, Voxel.GenerateVisibility(Biome, (Vector3I)Position));
                VoxelIDs[0] = (byte)Voxel.GenerateID(World, Biome, (Vector3I)Position);
            }
/*
            else
            {
                if (CanSubdivideFast())
                {
                    GenerateLeaves();
                    return;
                }
            
                if (CanSubdivideSlow())
                {   
                    GenerateLeaves();
                    return;
                }
            }
*/
            GenerateMeshData();
            GenerateMesh();
            GenerateCollision();
        }

        // Voxel generation.
        private bool CanSubdivideFast()
        {
            // Create a new bit array to hold voxel visibility bits.
            VoxelBits = new BitArray(8);
            
            // Create a new byte array to hold voxel IDs.
            VoxelIDs = new byte[8];
            
            // Create placeholder for previous voxel ID.
            int prevID = 0;
            
            // Generate 8 temporary voxels at node center to check if subdivision should occur.
            for (int voxelIndex = 0; voxelIndex < 8; voxelIndex ++)
            {
                // Get voxel global position.
                Vector3I voxelPosition = XYZBitShift.IndexToVector3I(voxelIndex, 1) - Vector3I.One;
                Vector3I voxelGlobalPosition = (Vector3I)Position + voxelPosition;
                
                // Generate voxel visibility & ID.
                bool visible = Voxel.GenerateVisibility(Biome, voxelGlobalPosition);
                byte thisID = (byte)Voxel.GenerateID(World, Biome, voxelGlobalPosition);

                // Update visibility and ID.
                if (visible && thisID != 0)
                {
                    VoxelBits.Set(voxelIndex, true);
                    VoxelIDs[voxelIndex] = thisID;
                }

                // Check against previous ID to determine if subdivision should occur.
                if (voxelIndex > 0 && thisID != prevID) return true;

                // Update previous ID.
                prevID = thisID;
            }

            // No subdivision should occur, return false.
            return false;
        }
        private bool CanSubdivideSlow()
        {
            // Calculate number of voxels in a chunk.
            int voxelCount = 1 << Branch << Branch << Branch;
            
            // Create a new bit array to hold voxel visibility bits.
            VoxelBits = new BitArray(voxelCount);
            
            // Create a new byte array to hold voxel IDs.
            VoxelIDs = new byte[voxelCount];

            // Set a placeholder for the previous voxel ID.
            byte prevVoxelID = 0;
            
            // Begin generating all voxels to check if subdivision should occur.
            for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex ++)
            {
                // Convert voxel grid index to voxel grid position.
                Vector3I voxelPosition = XYZBitShift.IndexToVector3I(voxelIndex, Branch);

                // Get voxel global position by adding chunk global position to voxel grid position.
                Vector3I voxelGlobalPosition = (Vector3I)Position + voxelPosition;
                
                // Generate voxel visibility & ID.
                bool visible = Voxel.GenerateVisibility(Biome, voxelGlobalPosition);
                byte thisVoxelID = (byte)Voxel.GenerateID(World, Biome, voxelGlobalPosition);

                // Update visibility and ID.
                if (visible && thisVoxelID != 0)
                {
                    VoxelIDs[voxelIndex] = thisVoxelID;
                    VoxelBits.Set(voxelIndex, true);
                }

                // Compare voxel ID against previous ID to check if subdivision should occur.
                if (voxelIndex > 0 && thisVoxelID != prevVoxelID) return true;
                
                // Update placeholder.
                prevVoxelID = thisVoxelID;
            }
            
            // No subdivision should occur, return false.
            return false;
        }
        
        // Leaf generation.
        private void GenerateLeaves()
        {
            if (VoxelBits.Length > 0) VoxelBits = null;
            if (VoxelIDs.Length > 0) VoxelIDs = null;
            
            // Generate the leaves array.
            Leaves = new OctreeNode[8];
            
            // Generate 8 leaves.
            for (int leafIndex = 0; leafIndex < 8; leafIndex ++)
            {
                // Get leaf grid position in a 2x2 grid.
                Vector3I leafPosition = XYZBitShift.IndexToVector3I(leafIndex, 1);
                
                // Scale leaf position to half node size.
                Vector3I nestedLeafPosition = (Vector3I)Position + XYZBitShift.Vector3ILeft(leafPosition, Branch - 1);

                // Create a new leaf.
                OctreeNode leaf = new(World, (byte)(Branch - 1));
                
                // Store new leaf.
                Leaves[leafIndex] = leaf;

                // Add leaf to scene.
                CallDeferred(Node.MethodName.AddChild, leaf);

                // Generate leaf task.
                Task generate = new(new Action(() => leaf.CallDeferred(nameof(Generate), nestedLeafPosition, Biome)));

                // Start task and await its completion.
                generate.Start();
                generate.Wait();

                Thread.Sleep(World.GenerateFrequency);
            }
        }

        // Mesh generation.
        private void GenerateMeshData()
        {
            //Color color = _root.VoxelLibrary.Voxels[VoxelIDs[0]].Color;
            Color color = Colors.OrangeRed;
            
            GenerateFaceMeshData(Voxel.Face.Top, Vector3I.Up, color);
            GenerateFaceMeshData(Voxel.Face.Btm, Vector3I.Down, color);
            GenerateFaceMeshData(Voxel.Face.West, Vector3I.Left, color);
            GenerateFaceMeshData(Voxel.Face.East, Vector3I.Right, color);
            GenerateFaceMeshData(Voxel.Face.North, Vector3I.Forward, color);
            GenerateFaceMeshData(Voxel.Face.South, Vector3I.Back, color);
        }
        private void GenerateFaceMeshData(Voxel.Face face, Vector3I normal, Color color)
        {
            Voxel.Vertex[] faceVertices = Voxel.Faces[face];
            
            Vector3I vertexA = Voxel.Vertices[faceVertices[0]];
            Vector3I vertexB = Voxel.Vertices[faceVertices[1]];
            Vector3I vertexC = Voxel.Vertices[faceVertices[2]];
            Vector3I vertexD = Voxel.Vertices[faceVertices[3]];

            vertexA = (Vector3I)Position + XYZBitShift.Vector3ILeft(vertexA, Branch);
            vertexB = (Vector3I)Position + XYZBitShift.Vector3ILeft(vertexB, Branch);
            vertexC = (Vector3I)Position + XYZBitShift.Vector3ILeft(vertexC, Branch);
            vertexD = (Vector3I)Position + XYZBitShift.Vector3ILeft(vertexD, Branch);
            
            int offset = _vertices.Count;

            _vertices.AddRange(new List<Vector3> { vertexA, vertexB, vertexC, vertexD });
            _normals.AddRange(new List<Vector3> { normal, normal, normal, normal });
            _colors.AddRange(new List<Color> { color, color, color, color });
            _indices.AddRange(new List<int> { 0 + offset, 1 + offset, 2 + offset, 0 + offset, 2 + offset, 3 + offset });
        }

        // Chunk mesh surface generation.
        private void GenerateMesh()
        {
            if (IsInstanceValid(Mesh)) Mesh = null;
            
            if (_vertices.Count == 0) return;
            if (_normals.Count == 0) return;
            if (_colors.Count == 0) return;
            if (_indices.Count == 0) return;
            
            Godot.Collections.Array surfaceArray = new();
            surfaceArray.Resize((int)Mesh.ArrayType.Max);

            surfaceArray[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Normal] = _normals.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Color]  = _colors.ToArray();
            surfaceArray[(int)Mesh.ArrayType.Index]  = _indices.ToArray();
            
            /* _vertices.Clear();
            _normals.Clear();
            _colors.Clear();
            _indices.Clear(); */

            ArrayMesh arrayMesh = new();

            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray);

            surfaceArray.Clear();

            Mesh = arrayMesh;
        }

        // Collision generation.
        private void GenerateCollision()
        {
            // Prevent collision generation with no mesh.
            if (Mesh == null) return;

            // Clear collision if it exists.
            StaticBody3D collision = GetChildOrNull<StaticBody3D>(0);
            collision?.QueueFree();

            // Create collision.
            CreateTrimeshCollision();
            
            // Update navigation.
            AddToGroup("NavSource");
        }
    }
}
