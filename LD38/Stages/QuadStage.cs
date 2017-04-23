using System;
using SharpVk;
using System.Linq;
using GlmSharp;
using System.Diagnostics;

namespace LD38.Stages
{
    public class QuadStage
        : RenderStage
    {
        private ShaderModule vertexShader;
        private ShaderModule fragmentShader;
        private DescriptorPool descriptorPool;
        private DescriptorSetLayout descriptorSetLayout;
        private PipelineLayout pipelineLayout;
        private Pipeline pipeline;
        private VulkanBuffer vertexBuffer;
        private VulkanBuffer indexBuffer;
        private VulkanBuffer uniformBuffer;
        private DescriptorSet descriptorSet;

        private int indexCount;
        private float aspectRatio;

        public override void Initialise(Device device, BufferManager bufferManager)
        {
            var vertShaderData = LoadShaderData(".\\Shaders\\BasicShader.vert.spv", out int vertCodeSize);
            this.vertexShader = device.CreateShaderModule(new ShaderModuleCreateInfo
            {
                Code = vertShaderData,
                CodeSize = vertCodeSize
            });

            var fragShaderData = LoadShaderData(".\\Shaders\\BasicShader.frag.spv", out int fragCodeSize);
            this.fragmentShader = device.CreateShaderModule(new ShaderModuleCreateInfo
            {
                Code = fragShaderData,
                CodeSize = fragCodeSize
            });

            SphereData.Get(2, out var vertices, out var indices);

            var vertexData = vertices.Select(x => new Vertex(x.Item1, x.Item2)).ToArray();

            indexCount = indices.Count();

            this.vertexBuffer = bufferManager.CreateBuffer(MemUtil.SizeOf<Vertex>() * (uint)vertexData.Length, BufferUsageFlags.TransferDestination | BufferUsageFlags.VertexBuffer, MemoryPropertyFlags.DeviceLocal);

            this.vertexBuffer.Update(vertexData);

            this.indexBuffer = bufferManager.CreateBuffer(MemUtil.SizeOf<ushort>() * (uint)indices.Length, BufferUsageFlags.TransferDestination | BufferUsageFlags.IndexBuffer, MemoryPropertyFlags.DeviceLocal);

            this.indexBuffer.Update(indices);

            this.uniformBuffer = bufferManager.CreateBuffer(MemUtil.SizeOf<UniformBufferObject>(), BufferUsageFlags.TransferDestination | BufferUsageFlags.UniformBuffer, MemoryPropertyFlags.DeviceLocal);

            this.descriptorPool = device.CreateDescriptorPool(new DescriptorPoolCreateInfo
            {
                PoolSizes = new[]
                {
                    new DescriptorPoolSize
                    {
                        DescriptorCount = 1,
                        Type = DescriptorType.UniformBuffer
                    }
                },
                MaxSets = 1
            });

            this.descriptorSetLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutCreateInfo
            {
                Bindings = new[]
                {
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 0,
                        DescriptorType = DescriptorType.UniformBuffer,
                        StageFlags = ShaderStageFlags.Vertex,
                        DescriptorCount = 1
                    }
                }
            });

            this.descriptorSet = device.AllocateDescriptorSets(new DescriptorSetAllocateInfo
            {
                DescriptorPool = descriptorPool,
                SetLayouts = new[]
                {
                    this.descriptorSetLayout
                }
            }).Single();

            device.UpdateDescriptorSets(
                new WriteDescriptorSet
                {
                    BufferInfo = new[]
                    {
                        new DescriptorBufferInfo
                        {
                            Buffer = this.uniformBuffer.Buffer,
                            Offset = 0,
                            Range = MemUtil.SizeOf<UniformBufferObject>()
                        }
                    },
                    DestinationSet = descriptorSet,
                    DestinationBinding = 0,
                    DestinationArrayElement = 0,
                    DescriptorType = DescriptorType.UniformBuffer
                }, null);

            this.pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutCreateInfo()
            {
                SetLayouts = new[]
                {
                    this.descriptorSetLayout
                }
            });
        }

        public override void Bind(Device device, RenderPass renderPass, CommandBuffer commandBuffer, Extent2D targetExtent)
        {
            this.aspectRatio = (float)targetExtent.Width / (float)targetExtent.Height;
            
            this.pipeline = device.CreateGraphicsPipelines(null, new[]
            {
                new GraphicsPipelineCreateInfo
                {
                    Layout = this.pipelineLayout,
                    RenderPass = renderPass,
                    Subpass = 0,
                    VertexInputState = new PipelineVertexInputStateCreateInfo()
                    {
                        VertexBindingDescriptions = new [] { Vertex.GetBindingDescription() },
                        VertexAttributeDescriptions = Vertex.GetAttributeDescriptions()
                    },
                    InputAssemblyState = new PipelineInputAssemblyStateCreateInfo
                    {
                        PrimitiveRestartEnable = false,
                        Topology = PrimitiveTopology.TriangleList
                    },
                    ViewportState = new PipelineViewportStateCreateInfo
                    {
                        Viewports = new[]
                        {
                            new Viewport
                            {
                                X = 0f,
                                Y = 0f,
                                Width = targetExtent.Width,
                                Height = targetExtent.Height,
                                MaxDepth = 1,
                                MinDepth = 0
                            }
                        },
                        Scissors = new[]
                        {
                            new Rect2D
                            {
                                Offset = new Offset2D(),
                                Extent= targetExtent
                            }
                        }
                    },
                    RasterizationState = new PipelineRasterizationStateCreateInfo
                    {
                        DepthClampEnable = false,
                        RasterizerDiscardEnable = false,
                        PolygonMode = PolygonMode.Fill,
                        LineWidth = 1,
                        CullMode = CullModeFlags.Back,
                        FrontFace = FrontFace.CounterClockwise,
                        DepthBiasEnable = false
                    },
                    MultisampleState = new PipelineMultisampleStateCreateInfo
                    {
                        SampleShadingEnable = false,
                        RasterizationSamples = SampleCountFlags.SampleCount1,
                        MinSampleShading = 1
                    },
                    ColorBlendState = new PipelineColorBlendStateCreateInfo
                    {
                        Attachments = new[]
                        {
                            new PipelineColorBlendAttachmentState
                            {
                                ColorWriteMask = ColorComponentFlags.R
                                                    | ColorComponentFlags.G
                                                    | ColorComponentFlags.B
                                                    | ColorComponentFlags.A,
                                BlendEnable = false,
                                SourceColorBlendFactor = BlendFactor.One,
                                DestinationColorBlendFactor = BlendFactor.Zero,
                                ColorBlendOp = BlendOp.Add,
                                SourceAlphaBlendFactor = BlendFactor.One,
                                DestinationAlphaBlendFactor = BlendFactor.Zero,
                                AlphaBlendOp = BlendOp.Add
                            }
                        },
                        LogicOpEnable = false,
                        LogicOp = LogicOp.Copy,
                        BlendConstants = new float[] {0,0,0,0}
                    },
                    Stages = new[]
                    {
                        new PipelineShaderStageCreateInfo
                        {
                            Stage = ShaderStageFlags.Vertex,
                            Module = this.vertexShader,
                            Name = "main"
                        },
                        new PipelineShaderStageCreateInfo
                        {
                            Stage = ShaderStageFlags.Fragment,
                            Module = this.fragmentShader,
                            Name = "main"
                        }
                    }
                }
            }).Single();

            commandBuffer.BindPipeline(PipelineBindPoint.Graphics, this.pipeline);

            commandBuffer.BindVertexBuffers(0, this.vertexBuffer.Buffer, (DeviceSize)0);

            commandBuffer.BindIndexBuffer(this.indexBuffer.Buffer, 0, IndexType.UInt16);

            commandBuffer.BindDescriptorSets(PipelineBindPoint.Graphics, pipelineLayout, 0, descriptorSet, null);

            commandBuffer.DrawIndexed((uint)indexCount, 1, 0, 0, 0);
        }

        public override void Update()
        {
            double rotationTime = 10.0;

            double rotation = ((Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency) % rotationTime) / rotationTime;
            
            var ubo = new UniformBufferObject
            {
                World = mat4.RotateY((float)Math.PI * 2 * (float)rotation),
                View = mat4.LookAt(new vec3(0, 0, -3f), vec3.Zero, vec3.UnitY),
                Projection = mat4.Perspective((float)Math.PI / 4f, this.aspectRatio, 0.1f, 10f)
            };

            //ubo.Projection[1, 1] *= -1;

            this.uniformBuffer.Update(ubo);
        }

        private static uint[] LoadShaderData(string filePath, out int codeSize)
        {
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            var shaderData = new uint[(int)Math.Ceiling(fileBytes.Length / 4f)];

            System.Buffer.BlockCopy(fileBytes, 0, shaderData, 0, fileBytes.Length);

            codeSize = fileBytes.Length;

            return shaderData;
        }

        public struct UniformBufferObject
        {
            public mat4 World;
            public mat4 View;
            public mat4 Projection;
        };
    }
}
