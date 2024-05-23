using Godot;

namespace RawVoxel
{
    public partial class VoxelPicker : RayCast3D
    {
        public VoxelPicker() {}
        
        [Export] MeshInstance3D VoxelHighlight;
        
        private readonly object voxelEditLock = new();
        
        public override void _Process(double delta)
        {
            Chunk collidedChunk = GetCollidedChunk();
            
            if (collidedChunk == null)
            {
                VoxelHighlight.Visible = false;
                VoxelHighlight.Position = Vector3.Zero;
                return;
            }

            Vector3 collisionPoint = GetCollisionPoint();
            Vector3 collisionNormal = GetCollisionNormal();
            Vector3I voxelGlobalPosition = (Vector3I)(collisionPoint + collisionNormal * -0.5f).Floor();
            
            VoxelHighlight.Position = voxelGlobalPosition + new Vector3(0.5f, 0.5f, 0.5f);
            VoxelHighlight.Visible = true;
            
            if (Input.IsActionPressed("break_voxel"))
            {
                lock (voxelEditLock)
                {
                    Voxel.SetType(collidedChunk, voxelGlobalPosition, 0);
                    CulledMesher.Generate(collidedChunk);
                }
            }
            
            if (Input.IsActionPressed("place_voxel"))
            {
                lock (voxelEditLock)
                {
                    Voxel.SetType(collidedChunk, voxelGlobalPosition + (Vector3I)collisionNormal, 1);
                    CulledMesher.Generate(collidedChunk);
                }
            }
        }

        public Chunk GetCollidedChunk()
        {
            object collidedObject = GetCollider();

            if (collidedObject is StaticBody3D collidedStaticBody3D)
            {
                Node parent = collidedStaticBody3D.GetParent();
                
                if (parent is Chunk) return parent as Chunk;
            }

            return null;
        }
    }
}