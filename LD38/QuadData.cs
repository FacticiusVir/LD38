using GlmSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LD38
{
    public static class QuadData
    {

        public static readonly Vertex[] Vertices =
        {
            new Vertex(new vec3(0, 0, 0), new vec3(0, 0, 1)),
            new Vertex(new vec3(1, 0, 0), new vec3(0, 0, 1)),
            new Vertex(new vec3(1, 1, 0), new vec3(0, 0, 1)),
            new Vertex(new vec3(1, 1, 0), new vec3(0, 0, 1)),
            new Vertex(new vec3(0, 1, 0), new vec3(0, 0, 1)),
            new Vertex(new vec3(0, 0, 0), new vec3(0, 0, 1))
        };

        public static readonly ushort[] Indices = { 0, 1, 2, 2, 4, 0 };
    }

    public static class SphereData
    {
        public static void Get(int detailLevel, out (vec3, vec3)[] vertices, out ushort[] indices)
        {
            var vectorList = new List<vec3>();
            var indexList = new List<int>();

            var vertexList = new List<(vec3, vec3)>();

            GeometryProvider.Icosahedron(vectorList, indexList);

            for (var i = 0; i < detailLevel; i++)
            {
                GeometryProvider.Subdivide(vectorList, indexList, true);
            }

            var random = new Random();

            /// normalize vectors to "inflate" the icosahedron into a sphere.
            for (var i = 0; i < vectorList.Count; i++)
            {
                float factor = ((float)random.NextDouble() - 0.5f) / 10f + 1f;

                vectorList[i] = vectorList[i].Normalized * factor;
            }

            for (int index = 0; index < indexList.Count; index += 3)
            {
                vec3 normal = (vectorList[indexList[index]] + vectorList[indexList[index + 1]] + vectorList[indexList[index + 2]]).Normalized;

                vertexList.Add((vectorList[indexList[index]], normal));
                vertexList.Add((vectorList[indexList[index + 1]], normal));
                vertexList.Add((vectorList[indexList[index + 2]], normal));
            }

            vertices = vertexList.ToArray();
            indices = Enumerable.Range(0, vertexList.Count).Select(x => (ushort)x).ToArray();

            //vertices = vectorList.Select(x => (x, x)).ToArray();
            //indices = indexList.Select(x => (ushort)x).ToArray();
        }
    }

    public static class GeometryProvider
    {

        private static int GetMidpointIndex(Dictionary<(int, int), int> midpointIndices, List<vec3> vertices, int i0, int i1)
        {
            var edgeKey = (Math.Min(i0, i1), Math.Max(i0, i1));

            var midpointIndex = -1;

            if (!midpointIndices.TryGetValue(edgeKey, out midpointIndex))
            {
                var v0 = vertices[i0];
                var v1 = vertices[i1];

                var midpoint = (v0 + v1) / 2f;

                if (vertices.Contains(midpoint))
                    midpointIndex = vertices.IndexOf(midpoint);
                else
                {
                    midpointIndex = vertices.Count;
                    vertices.Add(midpoint);
                    midpointIndices.Add(edgeKey, midpointIndex);
                }
            }


            return midpointIndex;

        }

        /// <remarks>
        ///      i0
        ///     /  \
        ///    m02-m01
        ///   /  \ /  \
        /// i2---m12---i1
        /// </remarks>
        /// <param name="vectors"></param>
        /// <param name="indices"></param>
        public static void Subdivide(List<vec3> vectors, List<int> indices, bool removeSourceTriangles)
        {
            var midpointIndices = new Dictionary<(int, int), int>();

            var newIndices = new List<int>(indices.Count * 4);

            if (!removeSourceTriangles)
                newIndices.AddRange(indices);

            for (var i = 0; i < indices.Count - 2; i += 3)
            {
                var i0 = indices[i];
                var i1 = indices[i + 1];
                var i2 = indices[i + 2];

                var m01 = GetMidpointIndex(midpointIndices, vectors, i0, i1);
                var m12 = GetMidpointIndex(midpointIndices, vectors, i1, i2);
                var m02 = GetMidpointIndex(midpointIndices, vectors, i2, i0);

                newIndices.AddRange(
                    new[] {
                    i0,m01,m02
                    ,
                    i1,m12,m01
                    ,
                    i2,m02,m12
                    ,
                    m02,m01,m12
                    }
                    );

            }

            indices.Clear();
            indices.AddRange(newIndices);
        }

        /// <summary>
        /// create a regular icosahedron (20-sided polyhedron)
        /// </summary>
        /// <param name="primitiveType"></param>
        /// <param name="size"></param>
        /// <param name="vertices"></param>
        /// <param name="indices"></param>
        /// <remarks>
        /// You can create this programmatically instead of using the given vertex 
        /// and index list, but it's kind of a pain and rather pointless beyond a 
        /// learning exercise.
        /// </remarks>

        /// note: icosahedron definition may have come from the OpenGL red book. I don't recall where I found it. 
        public static void Icosahedron(List<vec3> vertices, List<int> indices)
        {

            indices.AddRange(
                new int[]
                {
                0,4,1,
                0,9,4,
                9,5,4,
                4,5,8,
                4,8,1,
                8,10,1,
                8,3,10,
                5,3,8,
                5,2,3,
                2,7,3,
                7,10,3,
                7,6,10,
                7,11,6,
                11,0,6,
                0,1,6,
                6,1,10,
                9,0,11,
                9,11,2,
                9,2,5,
                7,2,11
                }
                .Select(i => i + vertices.Count)
            );

            var X = 0.525731112119133606f;
            var Z = 0.850650808352039932f;

            vertices.AddRange(
                new[]
                {
                new vec3(-X, 0f, Z),
                new vec3(X, 0f, Z),
                new vec3(-X, 0f, -Z),
                new vec3(X, 0f, -Z),
                new vec3(0f, Z, X),
                new vec3(0f, Z, -X),
                new vec3(0f, -Z, X),
                new vec3(0f, -Z, -X),
                new vec3(Z, X, 0f),
                new vec3(-Z, X, 0f),
                new vec3(Z, -X, 0f),
                new vec3(-Z, -X, 0f)
                }
            );


        }



    }
}
