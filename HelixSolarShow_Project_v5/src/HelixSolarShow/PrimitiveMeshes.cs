using OpenTK.Graphics.OpenGL4;

namespace HelixSolarShow;

internal sealed class SphereMesh : IDisposable
{
    public int VertexArray { get; }
    public int VertexBuffer { get; }
    public int ElementBuffer { get; }
    public int IndexCount { get; }

    public SphereMesh(int slices = 96, int stacks = 64)
    {
        var vertices = new List<float>();
        var indices = new List<uint>();

        for (int stack = 0; stack <= stacks; stack++)
        {
            float v = stack / (float)stacks;
            float phi = MathF.PI * (v - 0.5f);
            float cosPhi = MathF.Cos(phi);
            float sinPhi = MathF.Sin(phi);

            for (int slice = 0; slice <= slices; slice++)
            {
                float u = slice / (float)slices;
                float theta = MathF.Tau * u;
                float cosTheta = MathF.Cos(theta);
                float sinTheta = MathF.Sin(theta);

                float x = cosPhi * cosTheta;
                float y = sinPhi;
                float z = cosPhi * sinTheta;

                vertices.Add(x);
                vertices.Add(y);
                vertices.Add(z);
                vertices.Add(x);
                vertices.Add(y);
                vertices.Add(z);
            }
        }

        for (uint stack = 0; stack < stacks; stack++)
        {
            for (uint slice = 0; slice < slices; slice++)
            {
                uint first = stack * (uint)(slices + 1) + slice;
                uint second = first + (uint)slices + 1;

                indices.Add(first);
                indices.Add(second);
                indices.Add(first + 1);

                indices.Add(second);
                indices.Add(second + 1);
                indices.Add(first + 1);
            }
        }

        IndexCount = indices.Count;
        VertexArray = GL.GenVertexArray();
        VertexBuffer = GL.GenBuffer();
        ElementBuffer = GL.GenBuffer();

        GL.BindVertexArray(VertexArray);
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBuffer);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ElementBuffer);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(uint), indices.ToArray(), BufferUsageHint.StaticDraw);

        int stride = 6 * sizeof(float);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));

        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(ElementBuffer);
        GL.DeleteBuffer(VertexBuffer);
        GL.DeleteVertexArray(VertexArray);
    }
}
