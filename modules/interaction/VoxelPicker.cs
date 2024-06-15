using Godot;

namespace RawVoxel;

public partial class VoxelPicker : RayCast3D
{
    [Export] MeshInstance3D VoxelHighlight;
    private readonly object voxelEditLock = new();
    
    public override void _Process(double delta)
    {
        Chunk chunk = GetCollidedChunk();
        
        if (chunk == null)
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
                // FIXME - Voxel.SetType(ref chunk, voxelGlobalPosition, 0);
                // FIXME - chunk.Update();
        
                /* switch (worldSettings.MeshGeneration)
                {
                    case WorldSettings.MeshGenerationType.Greedy:
                        BinaryMesher.Generate(ref chunk, ref worldSettings);
                        break;
                    case WorldSettings.MeshGenerationType.Standard:
                        CulledMesher.Generate(ref chunk, ref biome, ref worldSettings);
                        break;
                } */
            }
        }
        
        if (Input.IsActionPressed("place_voxel"))
        {
            lock (voxelEditLock)
            {
                // FIXME - Voxel.SetType(ref chunk, voxelGlobalPosition + (Vector3I)collisionNormal, 1);
                // FIXME - chunk.Update();

                /* switch (worldSettings.MeshGeneration)
                {
                    case WorldSettings.MeshGenerationType.Greedy:
                        BinaryMesher.Generate(ref chunk, ref worldSettings);
                        break;
                    case WorldSettings.MeshGenerationType.Standard:
                        CulledMesher.Generate(ref chunk, ref biome, ref worldSettings);
                        break;
                } */
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
