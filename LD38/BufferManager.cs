using SharpVk;
using System.Runtime.InteropServices;

namespace LD38
{
    public class BufferManager
    {
        private readonly PhysicalDevice physicalDevice;
        private readonly Device device;
        private readonly Queue transferQueue;
        private readonly CommandPool transientCommandPool;

        private Buffer stagingBuffer;
        private DeviceMemory stagingBufferMemory;
        private uint stagingBufferSize;

        public BufferManager(PhysicalDevice physicalDevice, Device device, Queue transferQueue, uint transferQueueFamily)
        {
            this.physicalDevice = physicalDevice;
            this.device = device;
            this.transferQueue = transferQueue;

            this.transientCommandPool = device.CreateCommandPool(new CommandPoolCreateInfo
            {
                Flags = CommandPoolCreateFlags.Transient,
                QueueFamilyIndex = transferQueueFamily
            });
        }

        private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags flags)
        {
            var memoryProperties = this.physicalDevice.GetMemoryProperties();

            for (int i = 0; i < memoryProperties.MemoryTypes.Length; i++)
            {
                if ((typeFilter & (1u << i)) > 0
                        && memoryProperties.MemoryTypes[i].PropertyFlags.HasFlag(flags))
                {
                    return (uint)i;
                }
            }

            throw new System.Exception("No compatible memory type.");
        }

        public VulkanBuffer CreateBuffer(ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties)
        {
            this.CreateBuffer(size, usage, properties, out var buffer, out var memory);

            return new VulkanBuffer(this, buffer, memory);
        }

        private void CreateBuffer(ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties, out Buffer buffer, out DeviceMemory bufferMemory)
        {
            buffer = device.CreateBuffer(new BufferCreateInfo
            {
                Size = size,
                Usage = usage,
                SharingMode = SharingMode.Exclusive
            });

            var memRequirements = buffer.GetMemoryRequirements();

            bufferMemory = device.AllocateMemory(new MemoryAllocateInfo
            {
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties)
            });

            buffer.BindMemory(bufferMemory, 0);
        }

        internal void CopyBuffer(Buffer sourceBuffer, Buffer destinationBuffer, ulong size)
        {
            var transferBuffers = this.BeginSingleTimeCommand();

            transferBuffers[0].CopyBuffer(sourceBuffer, destinationBuffer, new[] { new BufferCopy { Size = size } });

            this.EndSingleTimeCommand(transferBuffers);
        }

        internal void UpdateBuffer<T>(Buffer buffer, T data, int offset = 0)
            where T : struct
        {
            uint dataSize = MemUtil.SizeOf<T>();
            uint dataOffset = (uint)(offset * dataSize);

            this.CheckStagingBufferSize(dataSize, dataOffset);

            System.IntPtr memoryBuffer = System.IntPtr.Zero;
            this.stagingBufferMemory.MapMemory(dataOffset, dataSize, MemoryMapFlags.None, ref memoryBuffer);

            Marshal.StructureToPtr(data, memoryBuffer, false);

            this.stagingBufferMemory.UnmapMemory();

            this.CopyBuffer(this.stagingBuffer, buffer, dataOffset + dataSize);
        }

        internal void UpdateBuffer<T>(Buffer buffer, T[] data, int offset = 0)
            where T : struct
        {
            uint dataSize = (uint)(MemUtil.SizeOf<T>() * data.Length);
            uint dataOffset = (uint)(offset * dataSize);

            this.CheckStagingBufferSize(dataSize, dataOffset);

            System.IntPtr memoryBuffer = System.IntPtr.Zero;
            this.stagingBufferMemory.MapMemory(dataOffset, dataSize, MemoryMapFlags.None, ref memoryBuffer);

            MemUtil.WriteToPtr(memoryBuffer, data, 0, data.Length);

            this.stagingBufferMemory.UnmapMemory();

            this.CopyBuffer(this.stagingBuffer, buffer, dataOffset + dataSize);
        }
        

        internal void CheckStagingBufferSize(uint dataSize, uint dataOffset)
        {
            uint memRequirement = dataOffset + dataSize;

            if (memRequirement > this.stagingBufferSize)
            {
                if (stagingBuffer != null)
                {
                    this.stagingBuffer.Destroy();
                    this.device.FreeMemory(this.stagingBufferMemory);
                }

                this.CreateBuffer(memRequirement, BufferUsageFlags.TransferSource, MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent, out this.stagingBuffer, out this.stagingBufferMemory);
                this.stagingBufferSize = memRequirement;
            }
        }

        internal void CopyImage(Image sourceImage, Image destinationImage, uint width, uint height)
        {
            var transferBuffers = this.BeginSingleTimeCommand();

            ImageSubresourceLayers subresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.Color,
                BaseArrayLayer = 0,
                LayerCount = 1,
                MipLevel = 0
            };

            ImageCopy region = new ImageCopy
            {
                DestinationSubresource = subresource,
                SourceSubresource = subresource,
                SourceOffset = new Offset3D(),
                DestinationOffset = new Offset3D(),
                Extent = new Extent3D
                {
                    Width = width,
                    Height = height,
                    Depth = 1
                }
            };

            transferBuffers[0].CopyImage(sourceImage, ImageLayout.TransferSourceOptimal, destinationImage, ImageLayout.TransferDestinationOptimal, new[] { region });

            this.EndSingleTimeCommand(transferBuffers);
        }

        private void TransitionImageLayout(Image image, Format format, ImageLayout oldLayout, ImageLayout newLayout)
        {
            var commandBuffer = this.BeginSingleTimeCommand();

            var barrier = new ImageMemoryBarrier
            {
                OldLayout = oldLayout,
                NewLayout = newLayout,
                SourceQueueFamilyIndex = Constants.QueueFamilyIgnored,
                DestinationQueueFamilyIndex = Constants.QueueFamilyIgnored,
                Image = image,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.Color,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            if (oldLayout == ImageLayout.Preinitialized && newLayout == ImageLayout.TransferSourceOptimal)
            {
                barrier.SourceAccessMask = AccessFlags.HostWrite;
                barrier.DestinationAccessMask = AccessFlags.TransferRead;
            }
            else if (oldLayout == ImageLayout.Preinitialized && newLayout == ImageLayout.TransferDestinationOptimal)
            {
                barrier.SourceAccessMask = AccessFlags.HostWrite;
                barrier.DestinationAccessMask = AccessFlags.TransferWrite;
            }
            else if (oldLayout == ImageLayout.TransferDestinationOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
            {
                barrier.SourceAccessMask = AccessFlags.TransferWrite;
                barrier.DestinationAccessMask = AccessFlags.ShaderRead;
            }
            else
            {
                throw new System.Exception("Unsupported layout transition");
            }

            commandBuffer[0].PipelineBarrier(PipelineStageFlags.TopOfPipe,
                                                PipelineStageFlags.TopOfPipe,
                                                DependencyFlags.None,
                                                null,
                                                null,
                                                new[] { barrier });

            this.EndSingleTimeCommand(commandBuffer);
        }

        private CommandBuffer[] BeginSingleTimeCommand()
        {
            var result = device.AllocateCommandBuffers(new CommandBufferAllocateInfo
            {
                Level = CommandBufferLevel.Primary,
                CommandPool = this.transientCommandPool,
                CommandBufferCount = 1
            });

            result[0].Begin(new CommandBufferBeginInfo
            {
                Flags = CommandBufferUsageFlags.OneTimeSubmit
            });

            return result;
        }

        private void EndSingleTimeCommand(CommandBuffer[] transferBuffers)
        {
            transferBuffers[0].End();

            this.transferQueue.Submit(new[] { new SubmitInfo { CommandBuffers = transferBuffers } }, null);
            this.transferQueue.WaitIdle();

            this.transientCommandPool.FreeCommandBuffers(transferBuffers);
        }

        public void CreateImage(uint width, uint height, Format format, ImageTiling imageTiling, ImageUsageFlags usage, MemoryPropertyFlags properties, out Image image, out DeviceMemory imageMemory)
        {
            image = this.device.CreateImage(new ImageCreateInfo
            {
                ImageType = ImageType.Image2d,
                Extent = new Extent3D
                {
                    Width = width,
                    Height = height,
                    Depth = 1
                },
                ArrayLayers = 1,
                MipLevels = 1,
                Format = format,
                Tiling = imageTiling,
                InitialLayout = ImageLayout.Preinitialized,
                Usage = usage,
                SharingMode = SharingMode.Exclusive,
                Samples = SampleCountFlags.SampleCount1,
                Flags = ImageCreateFlags.None
            });

            var memoryRequirements = image.GetMemoryRequirements();

            imageMemory = this.device.AllocateMemory(new MemoryAllocateInfo
            {
                AllocationSize = memoryRequirements.Size,
                MemoryTypeIndex = this.FindMemoryType(memoryRequirements.MemoryTypeBits, properties)
            });

            image.BindMemory(imageMemory, 0);
        }

    }
}
