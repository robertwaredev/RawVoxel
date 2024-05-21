using Godot;
using System.Threading;

namespace RawVoxel
{
    [GlobalClass, Tool]
    public partial class ChunkGrid : ChunkQueue
    {
        public ChunkGrid() {}
        
        public override void _Ready()
        {
            Thread WorldThread = new(new ThreadStart(ThreadProcess)) { Name = "Chunk Container Thread" };
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
                    LoadAndFree(new Chunk(World));

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
