using System;
using System.Threading.Tasks;

namespace GreatFluidDynamics.Rebuilt;

internal enum RenderMode
{
    Dye,
    Velocity,
    Pressure,
    Divergence
}

internal sealed class FluidSim
{
    private int _width;
    private int _height;
    private int _count;

    private float[] _u = Array.Empty<float>();
    private float[] _v = Array.Empty<float>();
    private float[] _u2 = Array.Empty<float>();
    private float[] _v2 = Array.Empty<float>();
    private float[] _pressure = Array.Empty<float>();
    private float[] _pressure2 = Array.Empty<float>();
    private float[] _divergence = Array.Empty<float>();
    private float[] _curl = Array.Empty<float>();

    private float[] _r = Array.Empty<float>();
    private float[] _g = Array.Empty<float>();
    private float[] _b = Array.Empty<float>();
    private float[] _r2 = Array.Empty<float>();
    private float[] _g2 = Array.Empty<float>();
    private float[] _b2 = Array.Empty<float>();

    private bool[] _solid = Array.Empty<bool>();
    private readonly ParallelOptions _parallelOptions = new();

    public FluidSim(int width, int height)
    {
        Resize(width, height);
        PressureIterations = 22;
        ThreadCount = Math.Max(1, Environment.ProcessorCount / 2);
    }

    public int Width => _width;
    public int Height => _height;
    public int ThreadCount
    {
        get => _parallelOptions.MaxDegreeOfParallelism <= 0 ? Environment.ProcessorCount : _parallelOptions.MaxDegreeOfParallelism;
        set => _parallelOptions.MaxDegreeOfParallelism = Math.Max(1, value);
    }

    public int PressureIterations { get; set; } = 22;
    public float VelocityDamping { get; set; } = 0.9992f;
    public float DyeDamping { get; set; } = 0.9987f;
    public float Buoyancy { get; set; } = 0.08f;
    public float VorticityConfinement { get; set; } = 0.55f;
    public float VelocityToCellScale { get; set; } = 65f;

    public void Resize(int width, int height)
    {
        _width = Math.Max(64, width);
        _height = Math.Max(64, height);
        _count = _width * _height;

        _u = new float[_count];
        _v = new float[_count];
        _u2 = new float[_count];
        _v2 = new float[_count];
        _pressure = new float[_count];
        _pressure2 = new float[_count];
        _divergence = new float[_count];
        _curl = new float[_count];

        _r = new float[_count];
        _g = new float[_count];
        _b = new float[_count];
        _r2 = new float[_count];
        _g2 = new float[_count];
        _b2 = new float[_count];

        _solid = new bool[_count];
        MakeBoundaryWalls();
    }

    public void Clear()
    {
        Array.Clear(_u);
        Array.Clear(_v);
        Array.Clear(_u2);
        Array.Clear(_v2);
        Array.Clear(_pressure);
        Array.Clear(_pressure2);
        Array.Clear(_divergence);
        Array.Clear(_curl);
        Array.Clear(_r);
        Array.Clear(_g);
        Array.Clear(_b);
        Array.Clear(_r2);
        Array.Clear(_g2);
        Array.Clear(_b2);
        Array.Clear(_solid);
        MakeBoundaryWalls();
    }

    public void ClearObstacles()
    {
        Array.Clear(_solid);
        MakeBoundaryWalls();
    }

    private void MakeBoundaryWalls()
    {
        for (int x = 0; x < _width; x++)
        {
            _solid[x] = true;
            _solid[x + (_height - 1) * _width] = true;
        }

        for (int y = 0; y < _height; y++)
        {
            _solid[y * _width] = true;
            _solid[y * _width + (_width - 1)] = true;
        }
    }

    public void PaintSolidCircle(int cx, int cy, int radius, bool value)
    {
        int r2 = radius * radius;
        int minX = Math.Max(1, cx - radius);
        int maxX = Math.Min(_width - 2, cx + radius);
        int minY = Math.Max(1, cy - radius);
        int maxY = Math.Min(_height - 2, cy + radius);

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - cy;
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - cx;
                if (dx * dx + dy * dy <= r2)
                {
                    _solid[Index(x, y)] = value;
                }
            }
        }

        MakeBoundaryWalls();
    }

    public void AddImpulseAndDye(int cx, int cy, int radius, float fx, float fy, float r, float g, float b)
    {
        int r2 = radius * radius;
        int minX = Math.Max(1, cx - radius);
        int maxX = Math.Min(_width - 2, cx + radius);
        int minY = Math.Max(1, cy - radius);
        int maxY = Math.Min(_height - 2, cy + radius);

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - cy;
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - cx;
                int d2 = dx * dx + dy * dy;
                if (d2 > r2)
                {
                    continue;
                }

                int idx = Index(x, y);
                if (_solid[idx])
                {
                    continue;
                }

                float falloff = 1f - (float)Math.Sqrt(d2) / Math.Max(1, radius);
                float amount = falloff * falloff;
                _u[idx] += fx * amount;
                _v[idx] += fy * amount;
                _r[idx] += r * amount;
                _g[idx] += g * amount;
                _b[idx] += b * amount;
            }
        }
    }

    public void Step(float dt)
    {
        dt = Math.Clamp(dt, 1f / 240f, 1f / 20f);

        ApplyBuoyancy(dt);
        ApplyVorticityConfinement(dt);

        DampVelocity();
        AdvectVelocity(dt);
        Project();

        AdvectDye(dt);
        DampDye();

        ZeroSolidCells();
    }

    private void DampVelocity()
    {
        float damp = VelocityDamping;
        Parallel.For(0, _count, _parallelOptions, i =>
        {
            _u[i] *= damp;
            _v[i] *= damp;
        });
    }

    private void DampDye()
    {
        float damp = DyeDamping;
        Parallel.For(0, _count, _parallelOptions, i =>
        {
            _r[i] *= damp;
            _g[i] *= damp;
            _b[i] *= damp;
        });
    }

    private void ApplyBuoyancy(float dt)
    {
        float buoy = Buoyancy * dt;
        Parallel.For(1, _height - 1, _parallelOptions, y =>
        {
            int row = y * _width;
            for (int x = 1; x < _width - 1; x++)
            {
                int i = row + x;
                if (_solid[i]) continue;

                float density = (_r[i] + _g[i] + _b[i]) * (1f / 3f);
                _v[i] -= density * buoy;
            }
        });
    }

    private void ApplyVorticityConfinement(float dt)
    {
        Parallel.For(1, _height - 1, _parallelOptions, y =>
        {
            int row = y * _width;
            for (int x = 1; x < _width - 1; x++)
            {
                int i = row + x;
                if (_solid[i])
                {
                    _curl[i] = 0f;
                    continue;
                }

                float dvDx = 0.5f * (_v[Index(x + 1, y)] - _v[Index(x - 1, y)]);
                float duDy = 0.5f * (_u[Index(x, y + 1)] - _u[Index(x, y - 1)]);
                _curl[i] = dvDx - duDy;
            }
        });

        float conf = VorticityConfinement * dt;
        Parallel.For(2, _height - 2, _parallelOptions, y =>
        {
            int row = y * _width;
            for (int x = 2; x < _width - 2; x++)
            {
                int i = row + x;
                if (_solid[i]) continue;

                float left = MathF.Abs(_curl[Index(x - 1, y)]);
                float right = MathF.Abs(_curl[Index(x + 1, y)]);
                float up = MathF.Abs(_curl[Index(x, y - 1)]);
                float down = MathF.Abs(_curl[Index(x, y + 1)]);

                float nx = right - left;
                float ny = down - up;
                float len = MathF.Sqrt(nx * nx + ny * ny) + 1e-5f;
                nx /= len;
                ny /= len;

                float w = _curl[i];
                _u[i] += ny * -w * conf;
                _v[i] += nx *  w * conf;
            }
        });
    }

    private void AdvectVelocity(float dt)
    {
        float scale = VelocityToCellScale * dt;
        Parallel.For(1, _height - 1, _parallelOptions, y =>
        {
            int row = y * _width;
            for (int x = 1; x < _width - 1; x++)
            {
                int i = row + x;
                if (_solid[i])
                {
                    _u2[i] = 0f;
                    _v2[i] = 0f;
                    continue;
                }

                float sx = x - _u[i] * scale;
                float sy = y - _v[i] * scale;
                _u2[i] = Sample(_u, sx, sy);
                _v2[i] = Sample(_v, sx, sy);
            }
        });

        Swap(ref _u, ref _u2);
        Swap(ref _v, ref _v2);
    }

    private void AdvectDye(float dt)
    {
        float scale = VelocityToCellScale * dt;
        Parallel.For(1, _height - 1, _parallelOptions, y =>
        {
            int row = y * _width;
            for (int x = 1; x < _width - 1; x++)
            {
                int i = row + x;
                if (_solid[i])
                {
                    _r2[i] = 0f;
                    _g2[i] = 0f;
                    _b2[i] = 0f;
                    continue;
                }

                float sx = x - _u[i] * scale;
                float sy = y - _v[i] * scale;
                _r2[i] = Sample(_r, sx, sy);
                _g2[i] = Sample(_g, sx, sy);
                _b2[i] = Sample(_b, sx, sy);
            }
        });

        Swap(ref _r, ref _r2);
        Swap(ref _g, ref _g2);
        Swap(ref _b, ref _b2);
    }

    private void Project()
    {
        Parallel.For(1, _height - 1, _parallelOptions, y =>
        {
            int row = y * _width;
            for (int x = 1; x < _width - 1; x++)
            {
                int i = row + x;
                if (_solid[i])
                {
                    _divergence[i] = 0f;
                    _pressure[i] = 0f;
                    continue;
                }

                float left = _solid[Index(x - 1, y)] ? 0f : _u[Index(x - 1, y)];
                float right = _solid[Index(x + 1, y)] ? 0f : _u[Index(x + 1, y)];
                float up = _solid[Index(x, y - 1)] ? 0f : _v[Index(x, y - 1)];
                float down = _solid[Index(x, y + 1)] ? 0f : _v[Index(x, y + 1)];

                _divergence[i] = -0.5f * (right - left + down - up);
                _pressure[i] = 0f;
            }
        });

        for (int k = 0; k < PressureIterations; k++)
        {
            Parallel.For(1, _height - 1, _parallelOptions, y =>
            {
                int row = y * _width;
                for (int x = 1; x < _width - 1; x++)
                {
                    int i = row + x;
                    if (_solid[i])
                    {
                        _pressure2[i] = 0f;
                        continue;
                    }

                    float sum = 0f;
                    int count = 0;

                    if (!_solid[Index(x - 1, y)]) { sum += _pressure[Index(x - 1, y)]; count++; }
                    if (!_solid[Index(x + 1, y)]) { sum += _pressure[Index(x + 1, y)]; count++; }
                    if (!_solid[Index(x, y - 1)]) { sum += _pressure[Index(x, y - 1)]; count++; }
                    if (!_solid[Index(x, y + 1)]) { sum += _pressure[Index(x, y + 1)]; count++; }

                    _pressure2[i] = count > 0 ? (_divergence[i] + sum) / count : 0f;
                }
            });

            Swap(ref _pressure, ref _pressure2);
        }

        Parallel.For(1, _height - 1, _parallelOptions, y =>
        {
            int row = y * _width;
            for (int x = 1; x < _width - 1; x++)
            {
                int i = row + x;
                if (_solid[i])
                {
                    _u[i] = 0f;
                    _v[i] = 0f;
                    continue;
                }

                float pL = _solid[Index(x - 1, y)] ? _pressure[i] : _pressure[Index(x - 1, y)];
                float pR = _solid[Index(x + 1, y)] ? _pressure[i] : _pressure[Index(x + 1, y)];
                float pU = _solid[Index(x, y - 1)] ? _pressure[i] : _pressure[Index(x, y - 1)];
                float pD = _solid[Index(x, y + 1)] ? _pressure[i] : _pressure[Index(x, y + 1)];

                _u[i] -= 0.5f * (pR - pL);
                _v[i] -= 0.5f * (pD - pU);
            }
        });
    }

    private void ZeroSolidCells()
    {
        Parallel.For(0, _count, _parallelOptions, i =>
        {
            if (_solid[i])
            {
                _u[i] = 0f;
                _v[i] = 0f;
                _r[i] = 0f;
                _g[i] = 0f;
                _b[i] = 0f;
            }
        });
    }

    public void RenderToBgra(Span<byte> pixels, RenderMode mode, bool shading)
    {
        switch (mode)
        {
            case RenderMode.Dye:
                RenderDye(pixels, shading);
                break;
            case RenderMode.Velocity:
                RenderVelocity(pixels);
                break;
            case RenderMode.Pressure:
                RenderScalar(pixels, _pressure, 1.6f, true);
                break;
            case RenderMode.Divergence:
                RenderScalar(pixels, _divergence, 6.0f, false);
                break;
        }
    }

    private void RenderDye(Span<byte> pixels, bool shading)
    {
        int p = 0;
        for (int y = 0; y < _height; y++)
        {
            int row = y * _width;
            for (int x = 0; x < _width; x++)
            {
                int i = row + x;

                if (_solid[i])
                {
                    pixels[p++] = 30;
                    pixels[p++] = 36;
                    pixels[p++] = 44;
                    pixels[p++] = 255;
                    continue;
                }

                float r = _r[i];
                float g = _g[i];
                float b = _b[i];

                if (shading && x > 0 && x < _width - 1 && y > 0 && y < _height - 1)
                {
                    float lumL = Luma(_r[Index(x - 1, y)], _g[Index(x - 1, y)], _b[Index(x - 1, y)]);
                    float lumR = Luma(_r[Index(x + 1, y)], _g[Index(x + 1, y)], _b[Index(x + 1, y)]);
                    float lumU = Luma(_r[Index(x, y - 1)], _g[Index(x, y - 1)], _b[Index(x, y - 1)]);
                    float lumD = Luma(_r[Index(x, y + 1)], _g[Index(x, y + 1)], _b[Index(x, y + 1)]);

                    float gx = lumR - lumL;
                    float gy = lumD - lumU;
                    float light = 1.06f + (-gx * 0.45f) + (-gy * 0.25f);
                    light = Math.Clamp(light, 0.72f, 1.45f);
                    r *= light;
                    g *= light;
                    b *= light;
                }

                r = 1f - MathF.Exp(-r * 1.35f);
                g = 1f - MathF.Exp(-g * 1.35f);
                b = 1f - MathF.Exp(-b * 1.35f);

                r = MathF.Sqrt(Math.Clamp(r, 0f, 1f));
                g = MathF.Sqrt(Math.Clamp(g, 0f, 1f));
                b = MathF.Sqrt(Math.Clamp(b, 0f, 1f));

                byte rr = (byte)(r * 255f);
                byte gg = (byte)(g * 255f);
                byte bb = (byte)(b * 255f);

                pixels[p++] = bb;
                pixels[p++] = gg;
                pixels[p++] = rr;
                pixels[p++] = 255;
            }
        }
    }

    private void RenderVelocity(Span<byte> pixels)
    {
        int p = 0;
        for (int i = 0; i < _count; i++)
        {
            if (_solid[i])
            {
                pixels[p++] = 30;
                pixels[p++] = 36;
                pixels[p++] = 44;
                pixels[p++] = 255;
                continue;
            }

            float u = _u[i];
            float v = _v[i];
            float mag = MathF.Sqrt(u * u + v * v);
            float angle = (MathF.Atan2(v, u) + MathF.PI) / (2f * MathF.PI);
            HsvToRgb(angle, 0.85f, Math.Clamp(mag * 0.18f, 0f, 1f), out float r, out float g, out float b);

            pixels[p++] = (byte)(b * 255f);
            pixels[p++] = (byte)(g * 255f);
            pixels[p++] = (byte)(r * 255f);
            pixels[p++] = 255;
        }
    }

    private void RenderScalar(Span<byte> pixels, float[] field, float scale, bool pressure)
    {
        int p = 0;
        for (int i = 0; i < _count; i++)
        {
            if (_solid[i])
            {
                pixels[p++] = 30;
                pixels[p++] = 36;
                pixels[p++] = 44;
                pixels[p++] = 255;
                continue;
            }

            float v = Math.Clamp(field[i] * scale, -1f, 1f);
            float n = (v + 1f) * 0.5f;

            float r = pressure ? n : 1f - n;
            float g = 1f - MathF.Abs(v);
            float b = 1f - n;

            pixels[p++] = (byte)(Math.Clamp(b, 0f, 1f) * 255f);
            pixels[p++] = (byte)(Math.Clamp(g, 0f, 1f) * 255f);
            pixels[p++] = (byte)(Math.Clamp(r, 0f, 1f) * 255f);
            pixels[p++] = 255;
        }
    }

    private static float Luma(float r, float g, float b) => (0.2126f * r) + (0.7152f * g) + (0.0722f * b);

    private float Sample(float[] field, float x, float y)
    {
        x = Math.Clamp(x, 1.001f, _width - 2.001f);
        y = Math.Clamp(y, 1.001f, _height - 2.001f);

        int x0 = (int)x;
        int y0 = (int)y;
        int x1 = x0 + 1;
        int y1 = y0 + 1;
        float tx = x - x0;
        float ty = y - y0;

        float v00 = field[Index(x0, y0)];
        float v10 = field[Index(x1, y0)];
        float v01 = field[Index(x0, y1)];
        float v11 = field[Index(x1, y1)];

        float a = v00 + (v10 - v00) * tx;
        float b = v01 + (v11 - v01) * tx;
        return a + (b - a) * ty;
    }

    private int Index(int x, int y) => x + y * _width;

    private static void Swap(ref float[] a, ref float[] b)
    {
        (a, b) = (b, a);
    }

    private static void HsvToRgb(float h, float s, float v, out float r, out float g, out float b)
    {
        h = h - MathF.Floor(h);
        float c = v * s;
        float x = c * (1f - MathF.Abs((h * 6f % 2f) - 1f));
        float m = v - c;

        if (h < 1f / 6f)      { r = c; g = x; b = 0; }
        else if (h < 2f / 6f) { r = x; g = c; b = 0; }
        else if (h < 3f / 6f) { r = 0; g = c; b = x; }
        else if (h < 4f / 6f) { r = 0; g = x; b = c; }
        else if (h < 5f / 6f) { r = x; g = 0; b = c; }
        else                  { r = c; g = 0; b = x; }

        r += m;
        g += m;
        b += m;
    }
}
