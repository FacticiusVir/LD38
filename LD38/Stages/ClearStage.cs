using SharpVk;

namespace LD38.Stages
{
    public class ClearStage
        : RenderStage
    {
        public override void Bind(Device device, RenderPass renderPass, CommandBuffer commandBuffer, Extent2D targetExtent)
        {
            commandBuffer.ClearAttachments(
                    new ClearAttachment
                    {
                        AspectMask = ImageAspectFlags.Color,
                        ClearValue = new ClearColorValue(0.5f, 0f, 0.5f, 1f),
                        ColorAttachment = 0
                    },
                    new ClearRect
                    {
                        BaseArrayLayer = 0,
                        LayerCount = 1,
                        Rect = new Rect2D
                        {
                            Extent = targetExtent
                        }
                    });
        }
    }
}
