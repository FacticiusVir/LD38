using SharpVk;

namespace LD38
{
    public class VulkanImage
    {
        private readonly BufferManager manager;
        private readonly Image image;
        private readonly DeviceMemory memory;

        public VulkanImage(BufferManager manager, Image image, DeviceMemory memory)
        {
            this.manager = manager;
            this.image = image;
            this.memory = memory;
        }

        public Image Image => this.image;

        public DeviceMemory Memory => this.memory;
    }
}
