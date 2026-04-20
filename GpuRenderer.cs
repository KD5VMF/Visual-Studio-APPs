using System.Drawing;
using System.Drawing.Imaging;
using ImagingPixelFormat = System.Drawing.Imaging.PixelFormat;
using GLPixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace LifeForgeAccelerated;

internal sealed class GpuRenderer : IDisposable
{
    private readonly List<DrawItem> _shadows = new(4096);
    private readonly List<DrawItem> _bodies = new(4096);
    private readonly List<DrawItem> _rings = new(2048);
    private readonly List<float> _instanceBuffer = new(4096 * 12);

    private int _circleProgram;
    private int _textureProgram;
    private int _quadVao;
    private int _quadVbo;
    private int _quadEbo;
    private int _instanceVbo;
    private int _fullscreenVao;
    private int _fullscreenVbo;
    private int _fullscreenEbo;
    private int _terrainTexture;
    private int _overlayTexture;
    private int _terrainWidth;
    private int _terrainHeight;
    private int _overlayWidth;
    private int _overlayHeight;

    public void Initialize()
    {
        _circleProgram = CreateProgram(CircleVertexShader, CircleFragmentShader);
        _textureProgram = CreateProgram(TextureVertexShader, TextureFragmentShader);

        var quadVertices = new float[]
        {
            -1f, -1f,
             1f, -1f,
             1f,  1f,
            -1f,  1f
        };
        var quadIndices = new uint[] { 0, 1, 2, 2, 3, 0 };

        _quadVao = GL.GenVertexArray();
        _quadVbo = GL.GenBuffer();
        _quadEbo = GL.GenBuffer();
        _instanceVbo = GL.GenBuffer();

        GL.BindVertexArray(_quadVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _quadEbo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, quadIndices.Length * sizeof(uint), quadIndices, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, 1, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        var stride = 12 * sizeof(float);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, 0);
        GL.VertexAttribDivisor(1, 1);
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, 4 * sizeof(float));
        GL.VertexAttribDivisor(2, 1);
        GL.EnableVertexAttribArray(3);
        GL.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, stride, 8 * sizeof(float));
        GL.VertexAttribDivisor(3, 1);
        GL.BindVertexArray(0);

        var fullscreenVertices = new float[]
        {
            -1f, -1f, 0f, 1f,
             1f, -1f, 1f, 1f,
             1f,  1f, 1f, 0f,
            -1f,  1f, 0f, 0f
        };

        _fullscreenVao = GL.GenVertexArray();
        _fullscreenVbo = GL.GenBuffer();
        _fullscreenEbo = GL.GenBuffer();
        GL.BindVertexArray(_fullscreenVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _fullscreenVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, fullscreenVertices.Length * sizeof(float), fullscreenVertices, BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _fullscreenEbo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, quadIndices.Length * sizeof(uint), quadIndices, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
        GL.BindVertexArray(0);

        _terrainTexture = GL.GenTexture();
        _overlayTexture = GL.GenTexture();

        ConfigureTexture(_terrainTexture);
        ConfigureTexture(_overlayTexture);

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.DepthTest);
        GL.ClearColor(0.05f, 0.07f, 0.11f, 1f);
    }

    public void UpdateTerrainTexture(Bitmap bitmap)
    {
        UpdateTexture(bitmap, _terrainTexture, ref _terrainWidth, ref _terrainHeight);
    }

    public void UpdateOverlayTexture(Bitmap bitmap)
    {
        UpdateTexture(bitmap, _overlayTexture, ref _overlayWidth, ref _overlayHeight);
    }

    public void Render(World world, int width, int height)
    {
        GL.Clear(ClearBufferMask.ColorBufferBit);

        if (_terrainWidth > 0 && _terrainHeight > 0)
        {
            DrawTexture(_terrainTexture, 1f);
        }

        world.BuildDrawLists(_shadows, _bodies, _rings);
        DrawInstances(_shadows, width, height);
        DrawInstances(_bodies, width, height);
        DrawInstances(_rings, width, height);

        if (_overlayWidth > 0 && _overlayHeight > 0)
        {
            DrawTexture(_overlayTexture, 1f);
        }
    }

    private void DrawInstances(List<DrawItem> items, int width, int height)
    {
        if (items.Count == 0) return;

        _instanceBuffer.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            _instanceBuffer.Add(item.X);
            _instanceBuffer.Add(item.Y);
            _instanceBuffer.Add(item.Radius);
            _instanceBuffer.Add(item.RadiusY);
            _instanceBuffer.Add(item.Elevation);
            _instanceBuffer.Add(item.Type);
            _instanceBuffer.Add(item.HeadingX);
            _instanceBuffer.Add(item.HeadingY);
            _instanceBuffer.Add(item.Color.R / 255f);
            _instanceBuffer.Add(item.Color.G / 255f);
            _instanceBuffer.Add(item.Color.B / 255f);
            _instanceBuffer.Add(item.Alpha);
        }

        GL.UseProgram(_circleProgram);
        GL.Uniform2(GL.GetUniformLocation(_circleProgram, "uViewport"), (float)width, (float)height);
        GL.BindVertexArray(_quadVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVbo);
        var upload = _instanceBuffer.ToArray();
        GL.BufferData(BufferTarget.ArrayBuffer, upload.Length * sizeof(float), upload, BufferUsageHint.DynamicDraw);
        GL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero, items.Count);
        GL.BindVertexArray(0);
    }

    private void DrawTexture(int texture, float alpha)
    {
        GL.UseProgram(_textureProgram);
        GL.Uniform1(GL.GetUniformLocation(_textureProgram, "uTexture"), 0);
        GL.Uniform1(GL.GetUniformLocation(_textureProgram, "uAlpha"), alpha);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, texture);
        GL.BindVertexArray(_fullscreenVao);
        GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);
    }

    private void UpdateTexture(Bitmap bitmap, int texture, ref int width, ref int height)
    {
        width = bitmap.Width;
        height = bitmap.Height;

        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, ImagingPixelFormat.Format32bppArgb);

        GL.BindTexture(TextureTarget.Texture2D, texture);
        GL.TexImage2D(
            TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba,
            bitmap.Width,
            bitmap.Height,
            0,
            GLPixelFormat.Bgra,
            PixelType.UnsignedByte,
            data.Scan0);

        bitmap.UnlockBits(data);
        bitmap.Dispose();
    }

    private static void ConfigureTexture(int texture)
    {
        GL.BindTexture(TextureTarget.Texture2D, texture);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }

    private static int CreateProgram(string vertex, string fragment)
    {
        var vs = CompileShader(ShaderType.VertexShader, vertex);
        var fs = CompileShader(ShaderType.FragmentShader, fragment);
        var program = GL.CreateProgram();
        GL.AttachShader(program, vs);
        GL.AttachShader(program, fs);
        GL.LinkProgram(program);
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out var linked);
        if (linked == 0)
        {
            var log = GL.GetProgramInfoLog(program);
            throw new InvalidOperationException($"Shader link failed: {log}");
        }

        GL.DetachShader(program, vs);
        GL.DetachShader(program, fs);
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);
        return program;
    }

    private static int CompileShader(ShaderType type, string source)
    {
        var shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out var compiled);
        if (compiled == 0)
        {
            var log = GL.GetShaderInfoLog(shader);
            throw new InvalidOperationException($"{type} compile failed: {log}");
        }
        return shader;
    }

    public void Dispose()
    {
        if (_circleProgram != 0) GL.DeleteProgram(_circleProgram);
        if (_textureProgram != 0) GL.DeleteProgram(_textureProgram);
        if (_quadVbo != 0) GL.DeleteBuffer(_quadVbo);
        if (_quadEbo != 0) GL.DeleteBuffer(_quadEbo);
        if (_instanceVbo != 0) GL.DeleteBuffer(_instanceVbo);
        if (_quadVao != 0) GL.DeleteVertexArray(_quadVao);
        if (_fullscreenVao != 0) GL.DeleteVertexArray(_fullscreenVao);
        if (_fullscreenVbo != 0) GL.DeleteBuffer(_fullscreenVbo);
        if (_fullscreenEbo != 0) GL.DeleteBuffer(_fullscreenEbo);
        if (_terrainTexture != 0) GL.DeleteTexture(_terrainTexture);
        if (_overlayTexture != 0) GL.DeleteTexture(_overlayTexture);
    }

    private const string CircleVertexShader = """
        #version 330 core
        layout(location = 0) in vec2 aCorner;
        layout(location = 1) in vec4 iRect;
        layout(location = 2) in vec4 iMisc;
        layout(location = 3) in vec4 iColor;

        uniform vec2 uViewport;

        out vec2 vUv;
        out vec4 vColor;
        out vec2 vHeading;
        flat out int vType;

        void main()
        {
            vec2 pos = vec2(iRect.x + aCorner.x * iRect.z, iRect.y + aCorner.y * iRect.w - iMisc.x);
            vec2 ndc = vec2((pos.x / uViewport.x) * 2.0 - 1.0, 1.0 - ((pos.y / uViewport.y) * 2.0));
            gl_Position = vec4(ndc, 0.0, 1.0);
            vUv = aCorner;
            vColor = iColor;
            vHeading = vec2(iMisc.z, iMisc.w);
            vType = int(iMisc.y + 0.5);
        }
        """;

    private const string CircleFragmentShader = """
        #version 330 core
        in vec2 vUv;
        in vec4 vColor;
        in vec2 vHeading;
        flat in int vType;

        out vec4 FragColor;

        void main()
        {
            float d = length(vUv);
            if (d > 1.0)
            {
                discard;
            }

            if (vType == 1)
            {
                float alpha = (1.0 - d) * 0.65 * vColor.a;
                FragColor = vec4(0.0, 0.0, 0.0, alpha);
                return;
            }

            if (vType == 2)
            {
                float outer = smoothstep(1.0, 0.95, d);
                float inner = smoothstep(0.84, 0.79, d);
                float band = clamp(outer - inner, 0.0, 1.0);
                if (band <= 0.001)
                {
                    discard;
                }
                FragColor = vec4(vColor.rgb, vColor.a * band);
                return;
            }

            vec3 normal = vec3(vUv.xy, sqrt(max(0.0, 1.0 - d * d)));
            vec3 lightDir = normalize(vec3(-0.45, -0.60, 0.68));
            float shade = max(0.20, dot(normal, lightDir));
            float rim = smoothstep(1.0, 0.72, d);
            vec2 heading = length(vHeading) > 0.0001 ? normalize(vHeading) : vec2(1.0, 0.0);
            float nose = max(dot(vUv, heading), 0.0);
            float spec = smoothstep(0.95, 0.10, length(vUv - vec2(-0.30, -0.34)));
            vec3 color = (vColor.rgb * (0.55 + shade * 0.50)) + vec3(spec * 0.22) + vec3(nose * 0.05);
            float alpha = vColor.a * rim;
            FragColor = vec4(color, alpha);
        }
        """;

    private const string TextureVertexShader = """
        #version 330 core
        layout(location = 0) in vec2 aPosition;
        layout(location = 1) in vec2 aTexCoord;
        out vec2 vTexCoord;
        void main()
        {
            gl_Position = vec4(aPosition, 0.0, 1.0);
            vTexCoord = aTexCoord;
        }
        """;

    private const string TextureFragmentShader = """
        #version 330 core
        in vec2 vTexCoord;
        out vec4 FragColor;
        uniform sampler2D uTexture;
        uniform float uAlpha;
        void main()
        {
            vec4 color = texture(uTexture, vTexCoord);
            FragColor = vec4(color.rgb, color.a * uAlpha);
        }
        """;
}
