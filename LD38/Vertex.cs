using GlmSharp;
using SharpVk;
using System.Runtime.InteropServices;

namespace LD38
{
    public struct Vertex
    {
        public Vertex(vec2 position)
        {
            this.Position = position;
        }

        public vec2 Position;

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
                        Format = Format.R32G32SFloat,
                        Offset = (uint)Marshal.OffsetOf<Vertex>("Position")
                    }
            };
        }
    }
}
