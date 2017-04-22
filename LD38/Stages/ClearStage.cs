using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpVk;

namespace LD38.Stages
{
    public class ClearStage
        : RenderStage
    {
        public override void Bind(CommandBuffer buffer, Extent2D targetExtent)
        {
            buffer.ClearAttachments(
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
