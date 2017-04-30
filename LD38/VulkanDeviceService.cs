using GlmSharp;
using SharpVk;
using SharpVk.Glfw;
using System;
using System.Collections.Generic;
using System.Linq;

using Size = SharpVk.Size;

namespace LD38
{
    public class VulkanDeviceService
        : GameService, IUpdatable
    {
        // A managed reference is held to prevent the delegate from being
        // garbage-collected while still in use by the unmanaged API.
        private readonly SharpVk.Interop.DebugReportCallbackDelegate debugReportDelegate;
        private readonly IUpdateLoopService updateLoop;
        private readonly GlfwService glfwService;

        private Instance instance;
        private DebugReportCallback reportCallback;
        private Surface surface;
        private PhysicalDevice physicalDevice;
        private Device device;
        private Queue graphicsQueue;
        private Queue presentQueue;
        private Queue transferQueue;
        private Swapchain swapChain;
        private RenderPass renderPass;
        private Image[] swapChainImages;
        private ImageView[] swapChainImageViews;
        private Framebuffer[] swapChainFrameBuffers;
        private CommandPool commandPool;
        private CommandBuffer[] commandBuffers;
        private VulkanImage depthImage;
        private ImageView depthImageView;

        private Semaphore screenRenderSemaphore;
        private Semaphore imageAvailableSemaphore;
        private Semaphore renderFinishedSemaphore;

        private Format swapChainFormat;
        private Extent2D swapChainExtent;

        private bool isCommandBufferStale;

        private readonly List<RenderStage> renderStages = new List<RenderStage>();
        private BufferManager bufferManager;

        public VulkanDeviceService(IUpdateLoopService updateLoop, GlfwService glfwService)
        {
            this.debugReportDelegate = this.DebugReport;
            this.updateLoop = updateLoop;
            this.glfwService = glfwService;
        }

        public override void Initialise(Game game)
        {
            this.CreateInstance();
        }

        public override void Start()
        {
            this.CreateSurface();
            this.PickPhysicalDevice();
            this.CreateLogicalDevice();

            var queueFamilies = FindQueueFamilies(this.physicalDevice);
            this.bufferManager = new BufferManager(this.physicalDevice, this.device, this.transferQueue, queueFamilies.TransferFamily.Value);

            this.CreateSwapChain();
            this.CreateCommandPool();
            this.CreateDepthResources();
            this.CreateRenderPass();
            this.CreateFrameBuffers();
            this.CreateSemaphores();

            foreach (var stage in this.renderStages)
            {
                stage.Initialise(this.device, this.bufferManager);
            }

            this.isCommandBufferStale = true;

            this.updateLoop.Register(this, UpdateStage.Render);
        }

        public void Update()
        {
            if (this.glfwService.IsResized)
            {
                this.RecreateSwapChain();
            }

            if (this.isCommandBufferStale)
            {
                this.CreateCommandBuffers();

                this.isCommandBufferStale = false;
            }

            foreach (var stage in this.renderStages)
            {
                stage.Update();
            }

            uint nextImage = this.swapChain.AcquireNextImage(uint.MaxValue, this.imageAvailableSemaphore, null);

            this.graphicsQueue.Submit(
                new SubmitInfo
                {
                    CommandBuffers = new[] { this.commandBuffers[nextImage] },
                    SignalSemaphores = new[] { this.renderFinishedSemaphore },
                    WaitDestinationStageMask = new[] { PipelineStageFlags.ColorAttachmentOutput },
                    WaitSemaphores = new[] { this.imageAvailableSemaphore }
                }, null);

            this.presentQueue.Present(new PresentInfo
            {
                ImageIndices = new uint[] { nextImage },
                Results = new Result[1],
                WaitSemaphores = new[] { this.renderFinishedSemaphore },
                Swapchains = new[] { this.swapChain }
            });
        }

        public override void Stop()
        {
            this.updateLoop.Deregister(this);

            this.surface.Destroy();
            this.surface = null;

            this.reportCallback.Destroy();
            this.reportCallback = null;

            this.instance.Destroy();
            this.instance = null;
        }

        public T CreateStage<T>()
            where T : RenderStage, new()
        {
            var result = new T();

            if (this.device != null)
            {
                result.Initialise(device, this.bufferManager);
            }

            this.renderStages.Add(result);

            this.isCommandBufferStale = true;

            return result;
        }

        private void RecreateSwapChain()
        {
            this.device.WaitIdle();

            var oldSwapChain = this.swapChain;

            this.CreateSwapChain();
            this.CreateDepthResources();
            this.CreateRenderPass();
            this.CreateFrameBuffers();

            oldSwapChain.Dispose();

            this.isCommandBufferStale = true;
        }

        private void CreateInstance()
        {
            var enabledLayers = new List<string>();

            if (Instance.EnumerateLayerProperties().Any(x => x.LayerName == "VK_LAYER_LUNARG_standard_validation"))
            {
                enabledLayers.Add("VK_LAYER_LUNARG_standard_validation");
            }

            var glfwExtensions = Glfw3.glfwGetRequiredInstanceExtensions();

            this.instance = Instance.Create(new InstanceCreateInfo
            {
                ApplicationInfo = new ApplicationInfo
                {
                    ApplicationName = "Ludum Dare 38",
                    ApplicationVersion = new SharpVk.Version(1, 0, 0),
                    EngineName = "SharpVk",
                    EngineVersion = Constants.SharpVkVersion,
                    ApiVersion = Constants.ApiVersion10
                },
                EnabledExtensionNames = glfwExtensions.Concat(new[] { ExtDebugReport.ExtensionName }).ToArray(),
                EnabledLayerNames = enabledLayers.ToArray()
            }, null);

            this.reportCallback = this.instance.CreateDebugReportCallback(new DebugReportCallbackCreateInfo
            {
                Flags = DebugReportFlags.Error | DebugReportFlags.Warning,
                PfnCallback = this.debugReportDelegate
            });
        }

        private void CreateSurface()
        {
            this.surface = this.instance.CreateGlfwSurface(this.glfwService.WindowHandle);
        }

        private void PickPhysicalDevice()
        {
            var availableDevices = this.instance.EnumeratePhysicalDevices();

            this.physicalDevice = availableDevices.First(IsSuitableDevice);
        }

        private void CreateLogicalDevice()
        {
            QueueFamilyIndices queueFamilies = FindQueueFamilies(this.physicalDevice);

            this.device = physicalDevice.CreateDevice(new DeviceCreateInfo
            {
                QueueCreateInfos = queueFamilies.Indices
                                                .Select(index => new DeviceQueueCreateInfo
                                                {
                                                    QueueFamilyIndex = index,
                                                    QueuePriorities = new[] { 1f }
                                                }).ToArray(),
                EnabledExtensionNames = new[] { KhrSwapchain.ExtensionName },
                EnabledLayerNames = null
            });

            this.graphicsQueue = this.device.GetQueue(queueFamilies.GraphicsFamily.Value, 0);
            this.presentQueue = this.device.GetQueue(queueFamilies.PresentFamily.Value, 0);
            this.transferQueue = this.device.GetQueue(queueFamilies.TransferFamily.Value, 0);
        }

        private void CreateRenderPass()
        {
            this.renderPass = this.device.CreateRenderPass(new RenderPassCreateInfo
            {
                Attachments = new[]
                {
                    new AttachmentDescription
                    {
                        Format = this.swapChainFormat,
                        Samples = SampleCountFlags.SampleCount1,
                        LoadOp = AttachmentLoadOp.DontCare,
                        StoreOp = AttachmentStoreOp.Store,
                        StencilLoadOp = AttachmentLoadOp.DontCare,
                        StencilStoreOp = AttachmentStoreOp.DontCare,
                        InitialLayout = ImageLayout.Undefined,
                        FinalLayout = ImageLayout.PresentSource
                    },
                    new AttachmentDescription
                    {
                        Format = this.depthImage.Format,
                        Samples = SampleCountFlags.SampleCount1,
                        LoadOp = AttachmentLoadOp.Clear,
                        StoreOp = AttachmentStoreOp.DontCare,
                        StencilLoadOp = AttachmentLoadOp.DontCare,
                        StencilStoreOp = AttachmentStoreOp.DontCare,
                        InitialLayout = ImageLayout.Undefined,
                        FinalLayout = ImageLayout.DepthStencilAttachmentOptimal

                    }
                },
                Subpasses = new[]
                {
                    new SubpassDescription
                    {
                        DepthStencilAttachment = new AttachmentReference
                        {
                            Attachment = 1,
                            Layout = ImageLayout.DepthStencilAttachmentOptimal
                        },
                        PipelineBindPoint = PipelineBindPoint.Graphics,
                        ColorAttachments = new []
                        {
                            new AttachmentReference
                            {
                                Attachment = 0,
                                Layout = ImageLayout.ColorAttachmentOptimal
                            }
                        }
                    }
                },
                Dependencies = new[]
                {
                    new SubpassDependency
                    {
                        SourceSubpass = Constants.SubpassExternal,
                        DestinationSubpass = 0,
                        SourceStageMask = PipelineStageFlags.BottomOfPipe,
                        SourceAccessMask = AccessFlags.MemoryRead,
                        DestinationStageMask = PipelineStageFlags.ColorAttachmentOutput,
                        DestinationAccessMask = AccessFlags.ColorAttachmentRead | AccessFlags.ColorAttachmentWrite
                    },
                    new SubpassDependency
                    {
                        SourceSubpass = 0,
                        DestinationSubpass = Constants.SubpassExternal,
                        SourceStageMask = PipelineStageFlags.ColorAttachmentOutput,
                        SourceAccessMask = AccessFlags.ColorAttachmentRead | AccessFlags.ColorAttachmentWrite,
                        DestinationStageMask = PipelineStageFlags.BottomOfPipe,
                        DestinationAccessMask = AccessFlags.MemoryRead
                    }
                }
            });
        }

        private void CreateSwapChain()
        {
            SwapChainSupportDetails swapChainSupport = this.QuerySwapChainSupport(this.physicalDevice);

            uint imageCount = swapChainSupport.Capabilities.MinImageCount + 1;
            if (swapChainSupport.Capabilities.MaxImageCount > 0 && imageCount > swapChainSupport.Capabilities.MaxImageCount)
            {
                imageCount = swapChainSupport.Capabilities.MaxImageCount;
            }

            SurfaceFormat surfaceFormat = this.ChooseSwapSurfaceFormat(swapChainSupport.Formats);

            QueueFamilyIndices queueFamilies = this.FindQueueFamilies(this.physicalDevice);

            var indices = queueFamilies.Indices.ToArray();

            Extent2D extent = this.ChooseSwapExtent(swapChainSupport.Capabilities);

            this.swapChain = device.CreateSwapchain(new SwapchainCreateInfo
            {
                Surface = surface,
                Flags = SwapchainCreateFlags.None,
                PresentMode = this.ChooseSwapPresentMode(swapChainSupport.PresentModes),
                MinImageCount = imageCount,
                ImageExtent = extent,
                ImageUsage = ImageUsageFlags.ColorAttachment,
                PreTransform = swapChainSupport.Capabilities.CurrentTransform,
                ImageArrayLayers = 1,
                ImageSharingMode = indices.Length == 1
                                    ? SharingMode.Exclusive
                                    : SharingMode.Concurrent,
                QueueFamilyIndices = indices,
                ImageFormat = surfaceFormat.Format,
                ImageColorSpace = surfaceFormat.ColorSpace,
                Clipped = true,
                CompositeAlpha = CompositeAlphaFlags.Opaque,
                OldSwapchain = this.swapChain
            });

            this.swapChainFormat = surfaceFormat.Format;
            this.swapChainExtent = extent;

            this.swapChainImages = this.swapChain.GetImages();

            this.swapChainImageViews = swapChainImages.Select(image => this.CreateImageView(image, this.swapChainFormat, ImageAspectFlags.Color)).ToArray();
        }

        private void CreateFrameBuffers()
        {
            this.swapChainFrameBuffers = this.swapChainImageViews.Select(imageView => this.device.CreateFramebuffer(new FramebufferCreateInfo
            {
                RenderPass = this.renderPass,
                Attachments = new[] { imageView, this.depthImageView },
                Layers = 1,
                Height = this.swapChainExtent.Height,
                Width = this.swapChainExtent.Width
            })).ToArray();
        }

        private void CreateCommandPool()
        {
            QueueFamilyIndices queueFamilies = FindQueueFamilies(this.physicalDevice);

            this.commandPool = device.CreateCommandPool(new CommandPoolCreateInfo
            {
                QueueFamilyIndex = queueFamilies.GraphicsFamily.Value
            });
        }

        private void CreateCommandBuffers()
        {
            this.commandBuffers = this.device.AllocateCommandBuffers(new CommandBufferAllocateInfo
            {
                CommandBufferCount = (uint)this.swapChainFrameBuffers.Length,
                CommandPool = commandPool,
                Level = CommandBufferLevel.Primary
            });

            for (int bufferIndex = 0; bufferIndex < this.swapChainFrameBuffers.Length; bufferIndex++)
            {
                var commandBuffer = this.commandBuffers[bufferIndex];
                var frameBuffer = this.swapChainFrameBuffers[bufferIndex];

                commandBuffer.Begin(new CommandBufferBeginInfo
                {
                    Flags = CommandBufferUsageFlags.SimultaneousUse
                });

                commandBuffer.BeginRenderPass(new RenderPassBeginInfo
                {
                    RenderPass = renderPass,
                    Framebuffer = frameBuffer,
                    RenderArea = new Rect2D
                    {
                        Offset = new Offset2D(),
                        Extent = this.swapChainExtent
                    },
                    ClearValues = new ClearValue[]
                    {
                        new ClearColorValue(0f, 0f, 0f, 1f),
                        new ClearDepthStencilValue(1f, 0)
                    }
                }, SubpassContents.Inline);

                foreach (var renderStage in this.renderStages)
                {
                    renderStage.Bind(this.device, this.renderPass, commandBuffer, this.swapChainExtent);
                }

                commandBuffer.EndRenderPass();

                commandBuffer.End();
            }
        }

        private void CreateDepthResources()
        {
            Format depthFormat = this.FindDepthFormat();

            this.depthImage = this.bufferManager.CreateImage(this.swapChainExtent.Width,
                                                                this.swapChainExtent.Height,
                                                                depthFormat,
                                                                ImageTiling.Optimal,
                                                                ImageUsageFlags.DepthStencilAttachment,
                                                                MemoryPropertyFlags.DeviceLocal);

            this.depthImageView = this.CreateImageView(this.depthImage.Image, depthFormat, ImageAspectFlags.Depth);

            this.depthImage.TransitionImageLayout(ImageLayout.Undefined, ImageLayout.DepthStencilAttachmentOptimal);
        }

        private void CreateSemaphores()
        {
            this.screenRenderSemaphore = device.CreateSemaphore(new SemaphoreCreateInfo());
            this.imageAvailableSemaphore = device.CreateSemaphore(new SemaphoreCreateInfo());
            this.renderFinishedSemaphore = device.CreateSemaphore(new SemaphoreCreateInfo());
        }

        private Format FindDepthFormat()
        {
            return this.FindSupportedFormat(new[] { Format.D32SFloat, Format.D32SFloatS8UInt, Format.D24UNormS8UInt }, ImageTiling.Optimal, FormatFeatureFlags.DepthStencilAttachment);
        }

        private static bool HasStencilComponent(Format format)
        {
            return format == Format.D32SFloatS8UInt || format == Format.D24UNormS8UInt;
        }

        private Format FindSupportedFormat(IEnumerable<Format> candidates, ImageTiling tiling, FormatFeatureFlags features)
        {
            foreach (var format in candidates)
            {
                var props = this.physicalDevice.GetFormatProperties(format);

                if (tiling == ImageTiling.Linear && props.LinearTilingFeatures.HasFlag(features))
                {
                    return format;
                }
                else if (tiling == ImageTiling.Optimal && props.OptimalTilingFeatures.HasFlag(features))
                {
                    return format;
                }
            }

            throw new Exception("failed to find supported format!");
        }

        private Bool32 DebugReport(DebugReportFlags flags, DebugReportObjectType objectType, ulong @object, Size location, int messageCode, string layerPrefix, string message, IntPtr userData)
        {
            System.Diagnostics.Debug.WriteLine($"{flags}: {message}");

            return true;
        }

        private ImageView CreateImageView(Image image, Format format, ImageAspectFlags aspectFlags)
        {
            return device.CreateImageView(new ImageViewCreateInfo
            {
                Components = ComponentMapping.Identity,
                Format = format,
                Image = image,
                Flags = ImageViewCreateFlags.None,
                ViewType = ImageViewType.ImageView2d,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = aspectFlags,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
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

            throw new Exception("No compatible memory type.");
        }

        private QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
        {
            QueueFamilyIndices indices = new QueueFamilyIndices();

            var queueFamilies = device.GetQueueFamilyProperties();

            for (uint index = 0; index < queueFamilies.Length && !indices.IsComplete; index++)
            {
                if (queueFamilies[index].QueueFlags.HasFlag(QueueFlags.Graphics))
                {
                    indices.GraphicsFamily = index;
                }

                if (device.GetSurfaceSupport(index, this.surface))
                {
                    indices.PresentFamily = index;
                }

                if (queueFamilies[index].QueueFlags.HasFlag(QueueFlags.Transfer) && !queueFamilies[index].QueueFlags.HasFlag(QueueFlags.Graphics))
                {
                    indices.TransferFamily = index;
                }
            }

            if (!indices.TransferFamily.HasValue)
            {
                indices.TransferFamily = indices.GraphicsFamily;
            }

            return indices;
        }

        private SurfaceFormat ChooseSwapSurfaceFormat(SurfaceFormat[] availableFormats)
        {
            if (availableFormats.Length == 1 && availableFormats[0].Format == Format.Undefined)
            {
                return new SurfaceFormat
                {
                    Format = Format.B8G8R8A8UNorm,
                    ColorSpace = ColorSpace.SrgbNonlinear
                };
            }

            foreach (var format in availableFormats)
            {
                if (format.Format == Format.B8G8R8A8UNorm && format.ColorSpace == ColorSpace.SrgbNonlinear)
                {
                    return format;
                }
            }

            return availableFormats[0];
        }

        private PresentMode ChooseSwapPresentMode(PresentMode[] availablePresentModes)
        {
            return availablePresentModes.Contains(PresentMode.Mailbox)
                    ? PresentMode.Mailbox
                    : PresentMode.Fifo;
        }

        public Extent2D ChooseSwapExtent(SurfaceCapabilities capabilities)
        {
            if (capabilities.CurrentExtent.Width != uint.MaxValue)
            {
                return capabilities.CurrentExtent;
            }
            else
            {
                return new Extent2D
                {
                    Width = Math.Max(capabilities.MinImageExtent.Width, Math.Min(capabilities.MaxImageExtent.Width, (uint)this.glfwService.WindowWidth)),
                    Height = Math.Max(capabilities.MinImageExtent.Height, Math.Min(capabilities.MaxImageExtent.Height, (uint)this.glfwService.WindowHeight))
                };
            }
        }

        SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice device)
        {
            return new SwapChainSupportDetails
            {
                Capabilities = device.GetSurfaceCapabilities(this.surface),
                Formats = device.GetSurfaceFormats(this.surface),
                PresentModes = device.GetSurfacePresentModes(this.surface)
            };
        }

        private bool IsSuitableDevice(PhysicalDevice device)
        {
            return device.EnumerateDeviceExtensionProperties(null).Any(extension => extension.ExtensionName == KhrSwapchain.ExtensionName)
                    && FindQueueFamilies(device).IsComplete;
        }

        private struct QueueFamilyIndices
        {
            public uint? GraphicsFamily;
            public uint? PresentFamily;
            public uint? TransferFamily;

            public IEnumerable<uint> Indices
            {
                get
                {
                    if (this.GraphicsFamily.HasValue)
                    {
                        yield return this.GraphicsFamily.Value;
                    }

                    if (this.PresentFamily.HasValue && this.PresentFamily != this.GraphicsFamily)
                    {
                        yield return this.PresentFamily.Value;
                    }

                    if (this.TransferFamily.HasValue && this.TransferFamily != this.PresentFamily && this.TransferFamily != this.GraphicsFamily)
                    {
                        yield return this.TransferFamily.Value;
                    }
                }
            }

            public bool IsComplete
            {
                get
                {
                    return this.GraphicsFamily.HasValue
                        && this.PresentFamily.HasValue
                        && this.TransferFamily.HasValue;
                }
            }
        }

        private struct SwapChainSupportDetails
        {
            public SurfaceCapabilities Capabilities;
            public SurfaceFormat[] Formats;
            public PresentMode[] PresentModes;
        }

        public struct UniformBufferObject
        {
            public mat4 World;
            public mat4 View;
            public mat4 Projection;
        };
    }
}
