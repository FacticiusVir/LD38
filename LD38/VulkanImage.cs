using SharpVk;

namespace LD38
{
    public class VulkanImage
    {
        private readonly BufferManager manager;
        private readonly Image image;
        private readonly DeviceMemory memory;
        private readonly Format format;

        public VulkanImage(BufferManager manager, Image image, DeviceMemory memory, Format format)
        {
            this.manager = manager;
            this.image = image;
            this.memory = memory;
            this.format = format;
        }

        public Image Image => this.image;

        public DeviceMemory Memory => this.memory;

        public Format Format => this.format;

        public void TransitionImageLayout(ImageLayout oldLayout, ImageLayout newLayout)
        {
            this.manager.TransitionImageLayout(this.image, this.format, oldLayout, newLayout);
        }
    }
}
