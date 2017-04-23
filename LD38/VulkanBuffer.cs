using SharpVk;

namespace LD38
{
    public class VulkanBuffer
    {
        private readonly BufferManager manager;
        private readonly Buffer buffer;
        private readonly DeviceMemory memory;

        public VulkanBuffer(BufferManager manager, Buffer buffer, DeviceMemory memory)
        {
            this.manager = manager;
            this.buffer = buffer;
            this.memory = memory;
        }

        public Buffer Buffer => this.buffer;

        public DeviceMemory Memory => this.memory;
        
        public void Update<T>(T data, int offset = 0)
            where T : struct
        {
            this.manager.UpdateBuffer(this.buffer, data, offset);
        }

        public void Update<T>(T[] data, int offset = 0)
            where T : struct
        {
            this.manager.UpdateBuffer(this.buffer, data, offset);
        }
    }
}
