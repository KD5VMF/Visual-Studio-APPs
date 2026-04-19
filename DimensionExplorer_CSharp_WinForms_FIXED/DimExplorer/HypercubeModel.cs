using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DimensionExplorer
{
    public sealed class HypercubeModel
    {
        public int Dimension { get; private set; }
        public double[][] Vertices { get; private set; }
        public Tuple<int, int>[] Edges { get; private set; }

        public HypercubeModel(int dimension)
        {
            if (dimension < 1 || dimension > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(dimension), "Dimension must be between 1 and 12.");
            }

            Dimension = dimension;
            Vertices = BuildVertices(dimension);
            Edges = BuildEdges(dimension);
        }

        private static double[][] BuildVertices(int dimension)
        {
            int count = 1 << dimension;
            double[][] vertices = new double[count][];

            for (int i = 0; i < count; i++)
            {
                double[] v = new double[dimension];
                for (int bit = 0; bit < dimension; bit++)
                {
                    v[bit] = ((i & (1 << bit)) == 0) ? -1.0 : 1.0;
                }
                vertices[i] = v;
            }

            return vertices;
        }

        private static Tuple<int, int>[] BuildEdges(int dimension)
        {
            int count = 1 << dimension;
            List<Tuple<int, int>> edges = new List<Tuple<int, int>>(count * dimension / 2);

            for (int i = 0; i < count; i++)
            {
                for (int bit = 0; bit < dimension; bit++)
                {
                    int j = i ^ (1 << bit);
                    if (i < j)
                    {
                        edges.Add(Tuple.Create(i, j));
                    }
                }
            }

            return edges.ToArray();
        }

        public static void RotateInPlace(double[] point, int axisA, int axisB, double angle)
        {
            if (axisA == axisB || axisA < 0 || axisB < 0 || axisA >= point.Length || axisB >= point.Length)
            {
                return;
            }

            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            double a = point[axisA];
            double b = point[axisB];

            point[axisA] = a * cos - b * sin;
            point[axisB] = a * sin + b * cos;
        }

        public static ProjectedVertex ProjectToScreen(double[] point, int width, int height, float scale, float higherDimPerspective, float cameraDistance)
        {
            double[] temp = point.ToArray();

            for (int d = temp.Length - 1; d >= 3; d--)
            {
                double denom = higherDimPerspective - temp[d];
                double factor = Math.Abs(denom) < 0.0001 ? 1.0 : higherDimPerspective / denom;
                for (int i = 0; i < d; i++)
                {
                    temp[i] *= factor;
                }
            }

            double x = temp.Length > 0 ? temp[0] : 0.0;
            double y = temp.Length > 1 ? temp[1] : 0.0;
            double z = temp.Length > 2 ? temp[2] : 0.0;

            double denom3 = cameraDistance - z;
            double factor3 = Math.Abs(denom3) < 0.0001 ? 1.0 : cameraDistance / denom3;

            float sx = width * 0.5f + (float)(x * factor3 * scale);
            float sy = height * 0.5f - (float)(y * factor3 * scale);
            float apparent = (float)Math.Max(2.0, 10.0 * factor3);

            return new ProjectedVertex(new PointF(sx, sy), (float)z, apparent);
        }
    }

    public struct ProjectedVertex
    {
        public PointF Screen;
        public float Z;
        public float Size;

        public ProjectedVertex(PointF screen, float z, float size)
        {
            Screen = screen;
            Z = z;
            Size = size;
        }
    }
}
