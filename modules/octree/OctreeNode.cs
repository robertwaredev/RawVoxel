using Godot;
using System;
using RawUtils;
using System.Threading;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RawVoxel
{
    public partial class OctreeNode : VoxelContainer
    {
        public readonly byte Branch;
        public OctreeNode[] Leaves;

        public OctreeNode(World world, byte branch)
        {
            World = world;
            Branch = branch;
        }

        public override void GenerateVoxels(Vector3I globalPosition)
        {
            Position = globalPosition;
            Biome = Biome.Generate(World, globalPosition);
            
            if (Branch == 0)
            {
                VoxelMasks.Set(0, Voxel.GenerateMask(this, (Vector3I)Position));
                VoxelTypes[0] = (byte)Voxel.GenerateType(this, (Vector3I)Position);
            }
            else
            {
                if (CanSubdivideFast() || CanSubdivideSlow())
                {
                    GenerateLeaves();
                    return;
                }
            }

            CulledMesher.Generate(this);
        }
        private bool CanSubdivideFast()
        {
            VoxelMasks = new BitArray(8);
            VoxelTypes = new byte[8];
            
            int prevType = 0;
            
            // Generate 8 temporary voxels at node center to check if subdivision should occur.
            for (int voxelIndex = 0; voxelIndex < 8; voxelIndex ++)
            {
                Vector3I voxelgridPosition = XYZBitShift.IndexToVector3I(voxelIndex, 1) - Vector3I.One;
                Vector3I voxelGlobalPosition = (Vector3I)Position + voxelgridPosition;
                
                bool mask = Voxel.GenerateMask(this, voxelGlobalPosition);
                byte type = (byte)Voxel.GenerateType(this, voxelGlobalPosition);

                if (mask && type != 0)
                {
                    VoxelMasks.Set(voxelIndex, true);
                    VoxelTypes[voxelIndex] = type;
                }

                if (voxelIndex > 0 && type != prevType) return true;

                prevType = type;
            }

            return false;
        }
        private bool CanSubdivideSlow()
        {
            int voxelCount = 1 << Branch << Branch << Branch;

            VoxelMasks = new BitArray(voxelCount);
            VoxelTypes = new byte[voxelCount];

            byte prevVoxelID = 0;
            
            // Begin generating all voxels to check if subdivision should occur.
            for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex ++)
            {
                Vector3I voxelPosition = XYZBitShift.IndexToVector3I(voxelIndex, Branch);

                Vector3I voxelGlobalPosition = (Vector3I)Position + voxelPosition;
                
                bool visible = Voxel.GenerateMask(this, voxelGlobalPosition);
                byte thisVoxelID = (byte)Voxel.GenerateType(this, voxelGlobalPosition);

                if (visible && thisVoxelID != 0)
                {
                    VoxelTypes[voxelIndex] = thisVoxelID;
                    VoxelMasks.Set(voxelIndex, true);
                }

                if (voxelIndex > 0 && thisVoxelID != prevVoxelID) return true;
                
                prevVoxelID = thisVoxelID;
            }
            
            return false;
        }
        
        private void GenerateLeaves()
        {
            if (VoxelMasks.Length > 0) VoxelMasks = null;
            if (VoxelTypes.Length > 0) VoxelTypes = null;
            
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
                Task generate = new(new Action(() => leaf.CallDeferred(nameof(GenerateVoxels), nestedLeafPosition, Biome)));

                // Start task and await its completion.
                generate.Start();
                generate.Wait();

                Thread.Sleep(World.GenerateFrequency);
            }
        }
    }
}
