using GlmSharp;
using SharpVk;
using System.Runtime.InteropServices;

namespace LD38
{
    public struct Vertex
    {
        public Vertex(vec3 position, vec3 normal, vec4 diffuse, vec4 specular)
        {
            this.Position = position;
            this.Normal = normal;
            this.Diffuse = diffuse;
            this.Specular = specular;
        }

        public vec3 Position;

        public vec3 Normal;

        public vec4 Diffuse;

        public vec4 Specular;

        public static VertexInputBindingDescription GetBindingDescription()
        {
            return new VertexInputBindingDescription()
            {
                Binding = 0,
                Stride = MemUtil.SizeOf<Vertex>(),
                InputRate = VertexInputRate.Vertex
            };
        }

        public static VertexInputAttributeDescription[] GetAttributeDescriptions()
        {
            return new VertexInputAttributeDescription[]
            {
                new VertexInputAttributeDescription
                {
                    Binding = 0,
                    Location = 0,
                    Format = Format.R32G32B32SFloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Position))
                },
                new VertexInputAttributeDescription
                {
                    Binding = 0,
                    Location = 1,
                    Format = Format.R32G32B32SFloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Normal))
                },
                new VertexInputAttributeDescription
                {
                    Binding = 0,
                    Location = 2,
                    Format = Format.R32G32B32A32SFloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Diffuse))
                },
                new VertexInputAttributeDescription
                {
                    Binding = 0,
                    Location = 3,
                    Format = Format.R32G32B32A32SFloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Specular))
                }
            };
        }
    }
}
