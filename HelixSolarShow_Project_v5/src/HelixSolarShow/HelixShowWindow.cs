using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using GlfwKeys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

namespace HelixSolarShow;

internal sealed class HelixShowWindow : GameWindow
{
    private readonly List<Body> _bodies = new();
    private readonly List<Vector3> _stars = new();

    private SphereMesh _sphere;
    private ShaderProgram _sphereShader;
    private ShaderProgram _lineShader;
    private ShaderProgram _pointShader;
    private ShaderProgram _backgroundShader;
    private ShaderProgram _billboardShader;
    private ShaderProgram _atmosphereShader;

    private int _trailVao;
    private int _trailVbo;
    private int _starVao;
    private int _starVbo;
    private int _quadVao;
    private int _quadVbo;

    private double _simDays;
    private double _realSeconds;
    private float _currentDaysPerSecond = 160.0f;

    private const float ForwardUnitsPerDay = 0.38f;
    private const int TrailSamples = 420;
    private const float TrailSpanDays = 420.0f;

    public HelixShowWindow(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws)
    {
        VSync = VSyncMode.On;
    }

    protected override void OnLoad()
    {
        try
        {
            base.OnLoad();
            CrashReporter.LogInfo("HelixShowWindow.OnLoad entered.");

            CursorState = CursorState.Hidden;

            GL.ClearColor(0.005f, 0.005f, 0.012f, 1f);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(TriangleFace.Back);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Enable(EnableCap.Multisample);

            _sphere = new SphereMesh();
            _sphereShader = new ShaderProgram(SphereVertexShader, SphereFragmentShader);
            _lineShader = new ShaderProgram(LineVertexShader, LineFragmentShader);
            _pointShader = new ShaderProgram(PointVertexShader, PointFragmentShader);
            _backgroundShader = new ShaderProgram(ScreenVertexShader, BackgroundFragmentShader);
            _billboardShader = new ShaderProgram(BillboardVertexShader, BillboardFragmentShader);
            _atmosphereShader = new ShaderProgram(AtmosphereVertexShader, AtmosphereFragmentShader);

            BuildQuad();
            BuildTrails();
            BuildBodies();
            BuildStars();
            UploadStars();

            string renderer = GL.GetString(StringName.Renderer) ?? "Unknown Renderer";
            string vendor = GL.GetString(StringName.Vendor) ?? "Unknown Vendor";
            string version = GL.GetString(StringName.Version) ?? "Unknown OpenGL";
            CrashReporter.LogInfo($"OnLoad completed. Renderer={renderer}; Vendor={vendor}; OpenGL={version}.");
        }
        catch (Exception ex)
        {
            CrashReporter.ReportFatal("HelixShowWindow.OnLoad", ex, CaptureContext());
            Close();
        }
    }

    private void BuildQuad()
    {
        float[] quad =
        {
            -1f, -1f,
             1f, -1f,
            -1f,  1f,
             1f,  1f
        };

        _quadVao = GL.GenVertexArray();
        _quadVbo = GL.GenBuffer();
        GL.BindVertexArray(_quadVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, quad.Length * sizeof(float), quad, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.BindVertexArray(0);
    }

    private void BuildTrails()
    {
        _trailVao = GL.GenVertexArray();
        _trailVbo = GL.GenBuffer();
        GL.BindVertexArray(_trailVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _trailVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, TrailSamples * 3 * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.BindVertexArray(0);
    }

    private void BuildBodies()
    {
        _bodies.Clear();

        _bodies.Add(new Body("Sun", -1, 0f, 0f, 0f, 4.6f, new Vector3(1.0f, 0.76f, 0.28f), 1.65f, 26.6f, 0f, 0f));
        _bodies.Add(new Body("Mercury", 0, 11f, 88f, 0.15f, 0.42f, new Vector3(0.70f, 0.67f, 0.64f), 0.03f, 58.6f, 0.02f, 0.6f));
        _bodies.Add(new Body("Venus", 0, 17f, 224.7f, 1.4f, 0.72f, new Vector3(0.91f, 0.78f, 0.45f), 0.05f, -243f, 0.03f, 1.0f));
        _bodies.Add(new Body("Earth", 0, 24f, 365.25f, 0.8f, 0.82f, new Vector3(0.25f, 0.48f, 1.0f), 0.05f, 1f, 0.04f, 0.8f));
        _bodies.Add(new Body("Moon", 3, 1.8f, 27.3f, 2.1f, 0.22f, new Vector3(0.75f, 0.75f, 0.78f), 0.01f, 27.3f, 0.01f, 1.4f));
        _bodies.Add(new Body("Mars", 0, 31f, 687f, 2.0f, 0.60f, new Vector3(0.86f, 0.41f, 0.22f), 0.04f, 1.03f, 0.06f, 1.2f));
        _bodies.Add(new Body("Jupiter", 0, 47f, 4332.6f, 0.9f, 1.72f, new Vector3(0.83f, 0.64f, 0.46f), 0.05f, 0.41f, 0.05f, 0.9f));
        _bodies.Add(new Body("Io", 6, 2.2f, 1.77f, 0.7f, 0.16f, new Vector3(0.94f, 0.78f, 0.30f), 0.01f, 1.77f, 0.00f, 0.8f));
        _bodies.Add(new Body("Europa", 6, 3.0f, 3.55f, 1.2f, 0.17f, new Vector3(0.82f, 0.82f, 0.74f), 0.01f, 3.55f, 0.00f, 0.9f));
        _bodies.Add(new Body("Saturn", 0, 63f, 10759f, 2.6f, 1.45f, new Vector3(0.92f, 0.82f, 0.56f), 0.05f, 0.45f, 0.04f, 1.0f, true));
        _bodies.Add(new Body("Titan", 9, 3.6f, 15.95f, 1.3f, 0.20f, new Vector3(0.86f, 0.68f, 0.32f), 0.01f, 15.95f, 0.00f, 0.6f));
        _bodies.Add(new Body("Uranus", 0, 78f, 30688f, 1.7f, 1.15f, new Vector3(0.54f, 0.87f, 0.92f), 0.05f, -0.72f, 0.03f, 1.6f, true));
        _bodies.Add(new Body("Neptune", 0, 93f, 60182f, 1.8f, 1.12f, new Vector3(0.26f, 0.48f, 0.94f), 0.05f, 0.67f, 0.03f, 0.7f));
        _bodies.Add(new Body("Pluto", 0, 108f, 90560f, 1.3f, 0.16f, new Vector3(0.73f, 0.62f, 0.52f), 0.02f, -6.39f, 0.02f, 0.5f));
    }

    private void BuildStars()
    {
        _stars.Clear();
        var rng = new Random(1337);
        for (int i = 0; i < 12000; i++)
        {
            float x = (float)(rng.NextDouble() * 12000.0 - 6000.0);
            float y = (float)(rng.NextDouble() * 6000.0 - 3000.0);
            float z = (float)(rng.NextDouble() * 6000.0 - 3000.0);
            _stars.Add(new Vector3(x, y, z));
        }
    }

    private void UploadStars()
    {
        _starVao = GL.GenVertexArray();
        _starVbo = GL.GenBuffer();
        GL.BindVertexArray(_starVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _starVbo);
        var starData = _stars.SelectMany(v => new[] { v.X, v.Y, v.Z }).ToArray();
        GL.BufferData(BufferTarget.ArrayBuffer, starData.Length * sizeof(float), starData, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.BindVertexArray(0);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        try
        {
            base.OnResize(e);
            GL.Viewport(0, 0, Size.X, Size.Y);
        }
        catch (Exception ex)
        {
            CrashReporter.ReportFatal("HelixShowWindow.OnResize", ex, CaptureContext());
            Close();
        }
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        try
        {
            base.OnUpdateFrame(args);
            _realSeconds += args.Time;

            float targetDaysPerSecond = GetDirectedDaysPerSecond((float)_realSeconds);
            float smooth = 1.0f - MathF.Exp((float)(-args.Time * 1.8));
            _currentDaysPerSecond = MathHelper.Lerp(_currentDaysPerSecond, targetDaysPerSecond, smooth);
            _simDays += args.Time * _currentDaysPerSecond;

            if (KeyboardState.IsKeyPressed(GlfwKeys.Escape) || KeyboardState.IsKeyPressed(GlfwKeys.Q))
                Close();
        }
        catch (Exception ex)
        {
            CrashReporter.ReportFatal("HelixShowWindow.OnUpdateFrame", ex, CaptureContext());
            Close();
        }
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        try
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            float aspect = Math.Max(1f, Size.X / (float)Math.Max(1, Size.Y));
            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(52f), aspect, 0.1f, 12000f);

            float t = (float)_realSeconds;
            CameraShot shot = GetCameraShot(t, (float)_simDays);
            Matrix4 view = Matrix4.LookAt(shot.CameraPos, shot.Target, shot.Up);
            Vector3 cameraDir = Vector3.Normalize(shot.Target - shot.CameraPos);
            Vector3 cameraRight = Vector3.Normalize(Vector3.Cross(cameraDir, shot.Up));
            Vector3 cameraUp = Vector3.Normalize(Vector3.Cross(cameraRight, cameraDir));

            RenderBackground(t);
            RenderStars(view, projection, t);
            RenderTrails(view, projection);
            RenderBodies(view, projection, shot.CameraPos);
            RenderAtmospheres(view, projection, shot.CameraPos);
            RenderSunGlow(view, projection, cameraRight, cameraUp);

            SwapBuffers();
        }
        catch (Exception ex)
        {
            CrashReporter.ReportFatal("HelixShowWindow.OnRenderFrame", ex, CaptureContext());
            Close();
        }
    }

    private float GetDirectedDaysPerSecond(float t)
    {
        float cycle = t % 180.0f;

        if (cycle < 28.0f) return SmoothBand(cycle, 0.0f, 28.0f, 120.0f, 520.0f);
        if (cycle < 52.0f) return SmoothBand(cycle, 28.0f, 52.0f, 110.0f, 42.0f);
        if (cycle < 70.0f) return SmoothBand(cycle, 52.0f, 70.0f, 12.0f, 4.0f);
        if (cycle < 98.0f) return SmoothBand(cycle, 70.0f, 98.0f, 180.0f, 760.0f);
        if (cycle < 122.0f) return SmoothBand(cycle, 98.0f, 122.0f, 26.0f, 7.0f);
        if (cycle < 146.0f) return SmoothBand(cycle, 122.0f, 146.0f, 30.0f, 8.0f);
        if (cycle < 164.0f) return SmoothBand(cycle, 146.0f, 164.0f, 60.0f, 18.0f);
        return SmoothBand(cycle, 164.0f, 180.0f, 240.0f, 1100.0f);
    }

    private static float SmoothBand(float t, float start, float end, float minValue, float maxValue)
    {
        if (end <= start)
            return maxValue;

        float u = Math.Clamp((t - start) / (end - start), 0.0f, 1.0f);
        float s = u * u * (3.0f - 2.0f * u);
        return MathHelper.Lerp(minValue, maxValue, s);
    }

    private readonly record struct CameraShot(Vector3 CameraPos, Vector3 Target, Vector3 Up);

    private CameraShot GetCameraShot(float t, float simDays)
    {
        float cycle = t % 180.0f;

        if (cycle < 28.0f)
        {
            Vector3 sun = ComputeWorldPosition(0, simDays);
            float yaw = t * 0.16f;
            float pitch = 0.26f + 0.05f * MathF.Sin(t * 0.14f);
            Vector3 target = sun + new Vector3(18f, 0f, 0f);
            return new CameraShot(target + OrbitOffset(150f, yaw, pitch), target, Vector3.UnitY);
        }

        if (cycle < 52.0f)
        {
            Vector3 sun = ComputeWorldPosition(0, simDays);
            float yaw = t * 0.11f;
            float pitch = 0.08f;
            Vector3 target = sun + new Vector3(28f, 0f, 0f);
            return new CameraShot(target + OrbitOffset(95f, yaw, pitch), target, Vector3.UnitY);
        }

        if (cycle < 70.0f)
        {
            Vector3 earth = ComputeWorldPosition(3, simDays);
            float yaw = t * 0.36f;
            float pitch = 0.18f + 0.04f * MathF.Sin(t * 0.23f);
            return new CameraShot(earth + OrbitOffset(8.5f, yaw, pitch), earth, Vector3.UnitY);
        }

        if (cycle < 98.0f)
        {
            Vector3 sun = ComputeWorldPosition(0, simDays);
            float yaw = t * 0.22f;
            float pitch = 0.33f;
            Vector3 target = sun + new Vector3(34f, MathF.Sin(t * 0.07f) * 6f, 0f);
            return new CameraShot(target + OrbitOffset(180f, yaw, pitch), target, Vector3.UnitY);
        }

        if (cycle < 122.0f)
        {
            Vector3 jupiter = ComputeWorldPosition(6, simDays);
            Vector3 sun = ComputeWorldPosition(0, simDays);
            Vector3 outward = (jupiter - sun).LengthSquared > 0.0001f ? Vector3.Normalize(jupiter - sun) : Vector3.UnitY;
            Vector3 target = jupiter + outward * 1.5f;
            Vector3 offset = outward * 18f + new Vector3(0f, 4.5f, 8f);
            return new CameraShot(target + offset, target, Vector3.UnitY);
        }

        if (cycle < 146.0f)
        {
            Vector3 saturn = ComputeWorldPosition(9, simDays);
            Vector3 sun = ComputeWorldPosition(0, simDays);
            Vector3 outward = (saturn - sun).LengthSquared > 0.0001f ? Vector3.Normalize(saturn - sun) : Vector3.UnitY;
            Vector3 target = saturn + outward * 1.4f;
            Vector3 offset = outward * 17f + new Vector3(0f, 3.5f, 7f);
            return new CameraShot(target + offset, target, Vector3.UnitY);
        }

        if (cycle < 164.0f)
        {
            Vector3 sun = ComputeWorldPosition(0, simDays);
            float yaw = t * 0.13f;
            float pitch = 0.04f;
            Vector3 target = sun + new Vector3(48f, 0f, 0f);
            return new CameraShot(target + OrbitOffset(120f, yaw, pitch), target, Vector3.UnitY);
        }

        Vector3 finalSun = ComputeWorldPosition(0, simDays);
        float finalYaw = t * 0.28f;
        float finalPitch = 0.42f;
        Vector3 finalTarget = finalSun + new Vector3(44f, MathF.Sin(t * 0.09f) * 8f, 0f);
        return new CameraShot(finalTarget + OrbitOffset(210f, finalYaw, finalPitch), finalTarget, Vector3.UnitY);
    }

    private static Vector3 OrbitOffset(float distance, float yaw, float pitch)
    {
        float cp = MathF.Cos(pitch);
        return new Vector3(
            MathF.Cos(yaw) * cp * distance,
            MathF.Sin(pitch) * distance,
            MathF.Sin(yaw) * cp * distance);
    }

    private void RenderBackground(float time)
    {
        GL.Disable(EnableCap.DepthTest);
        GL.DepthMask(false);
        _backgroundShader.Use();
        _backgroundShader.SetFloat("uTime", time);
        _backgroundShader.SetVector2("uResolution", new Vector2(Math.Max(1, Size.X), Math.Max(1, Size.Y)));
        GL.BindVertexArray(_quadVao);
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        GL.BindVertexArray(0);
        GL.DepthMask(true);
        GL.Enable(EnableCap.DepthTest);
    }

    private void RenderStars(Matrix4 view, Matrix4 projection, float time)
    {
        GL.DepthMask(false);
        _pointShader.Use();
        _pointShader.SetMatrix4("uView", view);
        _pointShader.SetMatrix4("uProjection", projection);
        _pointShader.SetFloat("uPointSize", 2.4f);
        _pointShader.SetFloat("uTime", time);
        GL.BindVertexArray(_starVao);
        GL.DrawArrays(PrimitiveType.Points, 0, _stars.Count);
        GL.BindVertexArray(0);
        GL.DepthMask(true);
    }

    private void RenderTrails(Matrix4 view, Matrix4 projection)
    {
        _lineShader.Use();
        _lineShader.SetMatrix4("uView", view);
        _lineShader.SetMatrix4("uProjection", projection);
        GL.BindVertexArray(_trailVao);

        for (int i = 1; i < _bodies.Count; i++)
        {
            var body = _bodies[i];
            var trail = new float[TrailSamples * 3];
            for (int s = 0; s < TrailSamples; s++)
            {
                double back = TrailSpanDays * (s / (double)(TrailSamples - 1));
                Vector3 pos = ComputeWorldPosition(i, _simDays - back);
                trail[s * 3 + 0] = pos.X;
                trail[s * 3 + 1] = pos.Y;
                trail[s * 3 + 2] = pos.Z;
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, _trailVbo);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, trail.Length * sizeof(float), trail);

            float alpha = body.ParentIndex == 0 ? 0.34f : 0.20f;
            float glowBoost = body.Name is "Jupiter" or "Saturn" ? 1.2f : 1.0f;
            _lineShader.SetVector4("uColor", new Vector4(body.Color * glowBoost, alpha));
            GL.DrawArrays(PrimitiveType.LineStrip, 0, TrailSamples);
        }

        GL.BindVertexArray(0);
    }

    private void RenderBodies(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
    {
        _sphereShader.Use();
        _sphereShader.SetMatrix4("uView", view);
        _sphereShader.SetMatrix4("uProjection", projection);
        _sphereShader.SetVector3("uCameraPos", cameraPos);
        _sphereShader.SetVector3("uLightPos", ComputeWorldPosition(0, _simDays));
        GL.BindVertexArray(_sphere.VertexArray);

        for (int i = 0; i < _bodies.Count; i++)
        {
            var body = _bodies[i];
            Vector3 pos = ComputeWorldPosition(i, _simDays);
            float spin = body.RotationDays != 0f ? (float)(_simDays / body.RotationDays) * MathF.Tau : 0f;

            Matrix4 model =
                Matrix4.CreateRotationY(spin) *
                Matrix4.CreateScale(body.Size) *
                Matrix4.CreateTranslation(pos);

            _sphereShader.SetMatrix4("uModel", model);
            _sphereShader.SetVector3("uBaseColor", body.Color);
            _sphereShader.SetFloat("uEmission", body.Emission);
            _sphereShader.SetInt("uSurfaceKind", GetSurfaceKind(body, i));
            _sphereShader.SetFloat("uTime", (float)_realSeconds * 0.25f + i * 1.7f);
            GL.DrawElements(PrimitiveType.Triangles, _sphere.IndexCount, DrawElementsType.UnsignedInt, 0);

            if (body.HasRings)
                RenderRings(pos, body.Size * 1.55f, body.Size * 2.45f, view, projection, body.Color * 0.95f + new Vector3(0.05f));
        }

        GL.BindVertexArray(0);
    }

    private void RenderAtmospheres(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
    {
        GL.DepthMask(false);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);

        _atmosphereShader.Use();
        _atmosphereShader.SetMatrix4("uView", view);
        _atmosphereShader.SetMatrix4("uProjection", projection);
        _atmosphereShader.SetVector3("uCameraPos", cameraPos);
        _atmosphereShader.SetVector3("uLightPos", ComputeWorldPosition(0, _simDays));
        GL.BindVertexArray(_sphere.VertexArray);

        for (int i = 0; i < _bodies.Count; i++)
        {
            var body = _bodies[i];
            if (!TryGetAtmosphere(body, out Vector3 atmoColor, out float strength, out float scale))
                continue;

            Vector3 pos = ComputeWorldPosition(i, _simDays);
            Matrix4 model = Matrix4.CreateScale(body.Size * scale) * Matrix4.CreateTranslation(pos);
            _atmosphereShader.SetMatrix4("uModel", model);
            _atmosphereShader.SetVector3("uColor", atmoColor);
            _atmosphereShader.SetFloat("uStrength", strength);
            GL.DrawElements(PrimitiveType.Triangles, _sphere.IndexCount, DrawElementsType.UnsignedInt, 0);
        }

        GL.BindVertexArray(0);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.DepthMask(true);
    }

    private void RenderSunGlow(Matrix4 view, Matrix4 projection, Vector3 cameraRight, Vector3 cameraUp)
    {
        Vector3 sun = ComputeWorldPosition(0, _simDays);

        GL.DepthMask(false);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);

        _billboardShader.Use();
        _billboardShader.SetMatrix4("uView", view);
        _billboardShader.SetMatrix4("uProjection", projection);
        _billboardShader.SetVector3("uCenter", sun);
        _billboardShader.SetVector3("uCameraRight", cameraRight);
        _billboardShader.SetVector3("uCameraUp", cameraUp);
        _billboardShader.SetFloat("uTime", (float)_realSeconds);

        GL.BindVertexArray(_quadVao);

        _billboardShader.SetFloat("uRadius", 13.5f);
        _billboardShader.SetVector4("uColor", new Vector4(1.0f, 0.82f, 0.32f, 0.55f));
        _billboardShader.SetFloat("uSoftness", 2.4f);
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        _billboardShader.SetFloat("uRadius", 21.0f);
        _billboardShader.SetVector4("uColor", new Vector4(1.0f, 0.48f, 0.12f, 0.18f));
        _billboardShader.SetFloat("uSoftness", 3.2f);
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        GL.BindVertexArray(0);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.DepthMask(true);
    }

    private void RenderRings(Vector3 center, float innerRadius, float outerRadius, Matrix4 view, Matrix4 projection, Vector3 color)
    {
        Span<float> verts = stackalloc float[6 * 64];
        int count = 0;
        for (int i = 0; i <= 63; i++)
        {
            float a = i / 63f * MathF.Tau;
            float c = MathF.Cos(a);
            float s = MathF.Sin(a);
            verts[count++] = center.X + c * innerRadius;
            verts[count++] = center.Y + s * innerRadius * 0.16f;
            verts[count++] = center.Z + s * innerRadius;
            verts[count++] = center.X + c * outerRadius;
            verts[count++] = center.Y + s * outerRadius * 0.16f;
            verts[count++] = center.Z + s * outerRadius;
        }

        int vao = GL.GenVertexArray();
        int vbo = GL.GenBuffer();
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts.ToArray(), BufferUsageHint.StreamDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

        _lineShader.Use();
        _lineShader.SetMatrix4("uView", view);
        _lineShader.SetMatrix4("uProjection", projection);
        _lineShader.SetVector4("uColor", new Vector4(color, 0.38f));
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 64 * 2);

        GL.BindVertexArray(0);
        GL.DeleteBuffer(vbo);
        GL.DeleteVertexArray(vao);
    }

    private static int GetSurfaceKind(Body body, int index)
    {
        if (index == 0) return 7;
        return body.Name switch
        {
            "Earth" => 2,
            "Jupiter" or "Saturn" => 3,
            "Moon" or "Europa" or "Pluto" => 4,
            "Uranus" or "Neptune" => 5,
            "Mars" => 6,
            _ => 1
        };
    }

    private static bool TryGetAtmosphere(Body body, out Vector3 color, out float strength, out float scale)
    {
        switch (body.Name)
        {
            case "Venus":
                color = new Vector3(1.0f, 0.82f, 0.45f);
                strength = 0.55f;
                scale = 1.05f;
                return true;
            case "Earth":
                color = new Vector3(0.36f, 0.62f, 1.0f);
                strength = 0.75f;
                scale = 1.05f;
                return true;
            case "Mars":
                color = new Vector3(0.92f, 0.42f, 0.24f);
                strength = 0.22f;
                scale = 1.03f;
                return true;
            case "Jupiter":
                color = new Vector3(0.92f, 0.72f, 0.50f);
                strength = 0.18f;
                scale = 1.02f;
                return true;
            case "Saturn":
                color = new Vector3(0.96f, 0.86f, 0.62f);
                strength = 0.16f;
                scale = 1.02f;
                return true;
            case "Uranus":
                color = new Vector3(0.62f, 0.95f, 1.0f);
                strength = 0.26f;
                scale = 1.03f;
                return true;
            case "Neptune":
                color = new Vector3(0.34f, 0.56f, 1.0f);
                strength = 0.30f;
                scale = 1.03f;
                return true;
            case "Titan":
                color = new Vector3(0.90f, 0.68f, 0.38f);
                strength = 0.14f;
                scale = 1.03f;
                return true;
            default:
                color = Vector3.Zero;
                strength = 0f;
                scale = 1f;
                return false;
        }
    }

    private Vector3 ComputeWorldPosition(int index, double simDays)
    {
        var body = _bodies[index];
        if (body.ParentIndex < 0)
            return new Vector3((float)(simDays * ForwardUnitsPerDay), 0f, 0f);

        Vector3 parent = ComputeWorldPosition(body.ParentIndex, simDays);
        double angle = simDays / body.OrbitDays * Math.PI * 2.0 + body.Phase;
        float ca = (float)Math.Cos(angle);
        float sa = (float)Math.Sin(angle);

        Vector3 rel = new Vector3(
            0f,
            body.OrbitRadius * ca,
            body.OrbitRadius * sa);

        float incline = body.Inclination;
        if (Math.Abs(incline) > 0.0001f)
        {
            float c = MathF.Cos(incline);
            float s = MathF.Sin(incline);
            rel = new Vector3(
                rel.X,
                rel.Y * c - rel.Z * s,
                rel.Y * s + rel.Z * c);
        }

        float wobble = body.VerticalWobble * MathF.Sin((float)angle * 0.7f + body.Phase * 1.3f);
        rel.X += wobble;

        return parent + rel;
    }

    private string CaptureContext()
    {
        return $"simDays={_simDays:F3}; realSeconds={_realSeconds:F3}; bodies={_bodies.Count}; size={Size.X}x{Size.Y}";
    }

    protected override void OnUnload()
    {
        try
        {
            base.OnUnload();
            CrashReporter.LogInfo("HelixShowWindow.OnUnload entered.");
            _sphereShader?.Dispose();
            _lineShader?.Dispose();
            _pointShader?.Dispose();
            _backgroundShader?.Dispose();
            _billboardShader?.Dispose();
            _atmosphereShader?.Dispose();
            _sphere?.Dispose();
            if (_trailVbo != 0) GL.DeleteBuffer(_trailVbo);
            if (_trailVao != 0) GL.DeleteVertexArray(_trailVao);
            if (_starVbo != 0) GL.DeleteBuffer(_starVbo);
            if (_starVao != 0) GL.DeleteVertexArray(_starVao);
            if (_quadVbo != 0) GL.DeleteBuffer(_quadVbo);
            if (_quadVao != 0) GL.DeleteVertexArray(_quadVao);
            CrashReporter.LogInfo("HelixShowWindow.OnUnload completed.");
        }
        catch (Exception ex)
        {
            CrashReporter.ReportFatal("HelixShowWindow.OnUnload", ex, CaptureContext());
        }
    }

    private readonly record struct Body(
        string Name,
        int ParentIndex,
        float OrbitRadius,
        float OrbitDays,
        float Phase,
        float Size,
        Vector3 Color,
        float Emission,
        float RotationDays,
        float VerticalWobble,
        float Inclination,
        bool HasRings = false);

    private const string SphereVertexShader = @"
#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

out vec3 vWorldPos;
out vec3 vNormal;
out vec3 vObjectPos;

void main()
{
    vec4 world = uModel * vec4(aPosition, 1.0);
    vWorldPos = world.xyz;
    vNormal = normalize(mat3(transpose(inverse(uModel))) * aNormal);
    vObjectPos = aPosition;
    gl_Position = uProjection * uView * world;
}";

    private const string SphereFragmentShader = @"
#version 330 core
in vec3 vWorldPos;
in vec3 vNormal;
in vec3 vObjectPos;

uniform vec3 uCameraPos;
uniform vec3 uLightPos;
uniform vec3 uBaseColor;
uniform float uEmission;
uniform int uSurfaceKind;
uniform float uTime;

out vec4 FragColor;

float hash(vec3 p)
{
    return fract(sin(dot(p, vec3(12.9898, 78.233, 45.164))) * 43758.5453);
}

float noise(vec3 p)
{
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = hash(i + vec3(0.0, 0.0, 0.0));
    float n100 = hash(i + vec3(1.0, 0.0, 0.0));
    float n010 = hash(i + vec3(0.0, 1.0, 0.0));
    float n110 = hash(i + vec3(1.0, 1.0, 0.0));
    float n001 = hash(i + vec3(0.0, 0.0, 1.0));
    float n101 = hash(i + vec3(1.0, 0.0, 1.0));
    float n011 = hash(i + vec3(0.0, 1.0, 1.0));
    float n111 = hash(i + vec3(1.0, 1.0, 1.0));

    float nx00 = mix(n000, n100, f.x);
    float nx10 = mix(n010, n110, f.x);
    float nx01 = mix(n001, n101, f.x);
    float nx11 = mix(n011, n111, f.x);
    float nxy0 = mix(nx00, nx10, f.y);
    float nxy1 = mix(nx01, nx11, f.y);
    return mix(nxy0, nxy1, f.z);
}

float fbm(vec3 p)
{
    float value = 0.0;
    float amp = 0.5;
    for (int i = 0; i < 4; ++i)
    {
        value += noise(p) * amp;
        p *= 2.02;
        amp *= 0.5;
    }
    return value;
}

vec3 surfaceColor(int kind, vec3 p, vec3 baseColor)
{
    float lat = asin(clamp(p.y, -1.0, 1.0));
    float lon = atan(p.z, p.x);

    if (kind == 7)
    {
        float plasma = fbm(p * 6.0 + vec3(uTime * 0.18, -uTime * 0.11, uTime * 0.07));
        float cells = 0.5 + 0.5 * sin((lon + plasma * 0.8) * 14.0 + uTime * 0.9);
        return mix(vec3(1.0, 0.45, 0.08), vec3(1.0, 0.88, 0.42), cells * 0.7 + plasma * 0.6);
    }

    if (kind == 2)
    {
        float continents = smoothstep(0.48, 0.62, fbm(p * 4.4 + vec3(2.0)));
        vec3 ocean = vec3(0.07, 0.18, 0.50);
        vec3 land = vec3(0.10, 0.38, 0.14);
        vec3 snow = vec3(0.92, 0.95, 1.0);
        vec3 color = mix(ocean, land, continents);
        float caps = smoothstep(0.68, 0.92, abs(p.y));
        color = mix(color, snow, caps * 0.75);
        float clouds = smoothstep(0.57, 0.72, fbm(p * 10.0 + vec3(uTime * 0.025, 0.0, 0.0)));
        color = mix(color, vec3(0.96, 0.98, 1.0), clouds * 0.28);
        return color;
    }

    if (kind == 3)
    {
        float bands = 0.5 + 0.5 * sin(lat * 24.0 + fbm(vec3(lon * 3.0, lat * 2.5, 0.0)) * 4.0 + uTime * 0.08);
        float storms = smoothstep(0.62, 0.80, fbm(p * 9.0 + vec3(1.7, 2.1, uTime * 0.03)));
        vec3 warm = baseColor * mix(0.82, 1.18, bands);
        return mix(warm, vec3(0.96, 0.82, 0.62), storms * 0.18);
    }

    if (kind == 4)
    {
        float craters = smoothstep(0.55, 0.78, fbm(p * 12.0 + vec3(4.0)));
        vec3 gray = baseColor * mix(0.78, 1.05, fbm(p * 7.0));
        return mix(gray, gray * 0.66, craters * 0.35);
    }

    if (kind == 5)
    {
        float band = 0.5 + 0.5 * sin(lat * 14.0 + uTime * 0.04);
        vec3 ice = mix(baseColor * 0.84, vec3(0.72, 0.90, 1.0), band * 0.35);
        return ice;
    }

    if (kind == 6)
    {
        float terrain = fbm(p * 6.0 + vec3(1.0));
        vec3 mars = mix(vec3(0.47, 0.19, 0.10), vec3(0.83, 0.40, 0.20), terrain);
        float caps = smoothstep(0.78, 0.96, abs(p.y));
        return mix(mars, vec3(0.96, 0.95, 0.92), caps * 0.72);
    }

    float rocky = fbm(p * 7.0 + vec3(3.0));
    return baseColor * mix(0.75, 1.18, rocky);
}

void main()
{
    vec3 n = normalize(vNormal);
    vec3 l = normalize(uLightPos - vWorldPos);
    vec3 v = normalize(uCameraPos - vWorldPos);
    vec3 h = normalize(l + v);

    float diff = max(dot(n, l), 0.0);
    float spec = pow(max(dot(n, h), 0.0), 64.0);
    float rim = pow(1.0 - max(dot(n, v), 0.0), 2.8);

    vec3 color = surfaceColor(uSurfaceKind, normalize(vObjectPos), uBaseColor);

    if (uSurfaceKind == 7)
    {
        vec3 emissive = color * (1.15 + uEmission * 0.8);
        vec3 hotRim = vec3(1.0, 0.85, 0.45) * rim * 0.9;
        FragColor = vec4(emissive + hotRim, 1.0);
        return;
    }

    vec3 ambient = color * 0.09;
    vec3 lit = color * (0.18 + diff * 0.95);
    vec3 sparkle = vec3(spec) * 0.48;
    vec3 fresnel = color * rim * 0.18;
    vec3 emissive = color * uEmission * 0.45;
    FragColor = vec4(ambient + lit + sparkle + fresnel + emissive, 1.0);
}";

    private const string AtmosphereVertexShader = @"
#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

out vec3 vWorldPos;
out vec3 vNormal;

void main()
{
    vec4 world = uModel * vec4(aPosition, 1.0);
    vWorldPos = world.xyz;
    vNormal = normalize(mat3(transpose(inverse(uModel))) * aNormal);
    gl_Position = uProjection * uView * world;
}";

    private const string AtmosphereFragmentShader = @"
#version 330 core
in vec3 vWorldPos;
in vec3 vNormal;

uniform vec3 uCameraPos;
uniform vec3 uLightPos;
uniform vec3 uColor;
uniform float uStrength;

out vec4 FragColor;

void main()
{
    vec3 n = normalize(vNormal);
    vec3 v = normalize(uCameraPos - vWorldPos);
    vec3 l = normalize(uLightPos - vWorldPos);

    float fresnel = pow(1.0 - max(dot(n, v), 0.0), 3.3);
    float day = pow(max(dot(n, l), 0.0), 0.6);
    float back = pow(max(dot(-n, l), 0.0), 1.2);

    float alpha = (fresnel * 0.90 + back * 0.35) * uStrength;
    vec3 color = uColor * (0.25 + day * 0.85) + uColor * back * 0.25;
    FragColor = vec4(color, alpha);
}";

    private const string BillboardVertexShader = @"
#version 330 core
layout(location = 0) in vec2 aPosition;

uniform mat4 uView;
uniform mat4 uProjection;
uniform vec3 uCenter;
uniform vec3 uCameraRight;
uniform vec3 uCameraUp;
uniform float uRadius;

out vec2 vUv;

void main()
{
    vUv = aPosition;
    vec3 world = uCenter + (uCameraRight * aPosition.x + uCameraUp * aPosition.y) * uRadius;
    gl_Position = uProjection * uView * vec4(world, 1.0);
}";

    private const string BillboardFragmentShader = @"
#version 330 core
in vec2 vUv;

uniform vec4 uColor;
uniform float uSoftness;
uniform float uTime;

out vec4 FragColor;

void main()
{
    float r = length(vUv);
    float spokes = 0.5 + 0.5 * sin(atan(vUv.y, vUv.x) * 12.0 + uTime * 0.8);
    float glow = exp(-pow(r * uSoftness, 2.0)) * (0.82 + spokes * 0.18);
    FragColor = vec4(uColor.rgb * glow, uColor.a * glow);
}";

    private const string ScreenVertexShader = @"
#version 330 core
layout(location = 0) in vec2 aPosition;
out vec2 vUv;
void main()
{
    vUv = aPosition * 0.5 + 0.5;
    gl_Position = vec4(aPosition, 0.0, 1.0);
}";

    private const string BackgroundFragmentShader = @"
#version 330 core
in vec2 vUv;

uniform vec2 uResolution;
uniform float uTime;

out vec4 FragColor;

float hash(vec2 p)
{
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float noise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash(i + vec2(0.0, 0.0));
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 5; ++i)
    {
        v += noise(p) * a;
        p *= 2.03;
        a *= 0.5;
    }
    return v;
}

void main()
{
    vec2 uv = (gl_FragCoord.xy / max(uResolution.xy, vec2(1.0))) * 2.0 - 1.0;
    uv.x *= uResolution.x / max(uResolution.y, 1.0);

    vec3 col = mix(vec3(0.005, 0.007, 0.018), vec3(0.020, 0.024, 0.055), 1.0 - vUv.y);

    float neb1 = fbm(uv * 1.6 + vec2(uTime * 0.008, -uTime * 0.004));
    float neb2 = fbm(uv * 3.2 + vec2(-uTime * 0.005, uTime * 0.003));
    vec3 nebula = vec3(0.18, 0.10, 0.28) * pow(neb1, 2.2) + vec3(0.04, 0.10, 0.20) * pow(neb2, 2.4);

    float vignette = smoothstep(1.45, 0.35, length(uv));
    col += nebula * vignette * 0.8;
    col *= vignette + 0.18;

    FragColor = vec4(col, 1.0);
}";

    private const string LineVertexShader = @"
#version 330 core
layout(location = 0) in vec3 aPosition;
uniform mat4 uView;
uniform mat4 uProjection;
void main()
{
    gl_Position = uProjection * uView * vec4(aPosition, 1.0);
}";

    private const string LineFragmentShader = @"
#version 330 core
uniform vec4 uColor;
out vec4 FragColor;
void main()
{
    FragColor = uColor;
}";

    private const string PointVertexShader = @"
#version 330 core
layout(location = 0) in vec3 aPosition;
uniform mat4 uView;
uniform mat4 uProjection;
uniform float uPointSize;
uniform float uTime;
out float vTwinkle;
void main()
{
    gl_Position = uProjection * uView * vec4(aPosition, 1.0);
    gl_PointSize = uPointSize;
    vTwinkle = 0.55 + 0.45 * sin(dot(aPosition.xy, vec2(0.017, 0.013)) + uTime * 0.35);
}";

    private const string PointFragmentShader = @"
#version 330 core
in float vTwinkle;
out vec4 FragColor;
void main()
{
    vec2 uv = gl_PointCoord * 2.0 - 1.0;
    float d = dot(uv, uv);
    float a = smoothstep(1.0, 0.08, d) * vTwinkle;
    FragColor = vec4(vec3(1.0, 0.98, 0.94), a * 0.95);
}";
}
