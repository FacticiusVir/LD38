using SharpVk;

namespace LD38
{
    public abstract class RenderStage
    {
        public virtual void Initialise(Device device)
        {
        }

        public abstract void Bind(CommandBuffer buffer, Extent2D targetExtent);
    }
}
