using Godot;
using System.Threading;

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class OctreeRoot : ChunkQueue
    {
        #region Exports
        
        [Export] public byte Branches = 4;

        #endregion Exports
        
        public OctreeRoot() {}
        
        public override void _Ready()
        {
            Thread WorldThread = new(new ThreadStart(ThreadProcess)) { Name = "World Thread" };
            WorldThread.Start();
        }
        public override void _PhysicsProcess(double delta)
        {
            TryUpdateFocusNodeGlobalPosition();
        }
        public void ThreadProcess()
        {
            while (IsInstanceValid(this))
            {
                if (Generated == false)
                {
                    TryUpdateFocusNodeGridPosition();
                    LoadAndFree(new OctreeNode(World, Branches));

                    Generated = true;
                }
                else if (TryUpdateFocusNodeGridPosition())
                {
                    Wrap();
                }
                
                Thread.Sleep(100);
            }
        }
    }
}
