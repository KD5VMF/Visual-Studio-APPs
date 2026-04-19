using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GreatFluidDynamics.Rebuilt;

internal sealed class MainForm : Form
{
    private readonly RenderPanel _canvas = new();
    private readonly Panel _topBar = new();
    private readonly Label _hintLabel = new();
    private readonly Button _pauseButton = new();
    private readonly Button _clearButton = new();
    private readonly Button _clearObsButton = new();
    private readonly Button _fullscreenButton = new();
    private readonly ComboBox _modeCombo = new();
    private readonly ComboBox _resolutionCombo = new();
    private readonly NumericUpDown _threadsBox = new();
    private readonly NumericUpDown _pressureBox = new();

    private FluidSim _sim;
    private Bitmap _frameBitmap;
    private byte[] _pixels;
    private readonly Stopwatch _frameClock = Stopwatch.StartNew();
    private readonly Stopwatch _fpsClock = Stopwatch.StartNew();

    private bool _paused;
    private bool _paintObstacle;
    private bool _eraseObstacle;
    private bool _shading = true;
    private RenderMode _renderMode = RenderMode.Dye;

    private float _fps;
    private double _lastFrameMs;
    private int _framesThisSecond;
    private float _accumulator;
    private Point _lastMouse;
    private bool _mouseDown;
    private float _hue;
    private FormWindowState _restoreWindowState;
    private FormBorderStyle _restoreBorderStyle;
    private bool _restoreTopMost;
    private Rectangle _restoreBounds;

    public MainForm()
    {
        Text = "Great Fluid Dynamics Rebuilt";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 700);
        BackColor = Color.FromArgb(8, 10, 14);
        KeyPreview = true;

        _sim = new FluidSim(384, 216);
        _frameBitmap = new Bitmap(_sim.Width, _sim.Height, PixelFormat.Format32bppArgb);
        _pixels = new byte[_sim.Width * _sim.Height * 4];

        InitializeUi();

        _canvas.Paint += Canvas_Paint;
        _canvas.MouseDown += Canvas_MouseDown;
        _canvas.MouseMove += Canvas_MouseMove;
        _canvas.MouseUp += (_, __) => { _mouseDown = false; _eraseObstacle = false; };
        _canvas.MouseWheel += Canvas_MouseWheel;

        KeyDown += MainForm_KeyDown;
        Application.Idle += Application_Idle;

        ResetDefaults();
    }

    private void InitializeUi()
    {
        _topBar.Dock = DockStyle.Top;
        _topBar.Height = 42;
        _topBar.Padding = new Padding(8, 6, 8, 6);
        _topBar.BackColor = Color.FromArgb(18, 22, 30);

        _canvas.Dock = DockStyle.Fill;
        _canvas.BackColor = Color.Black;

        _pauseButton.Text = "Pause";
        _clearButton.Text = "Clear Dye";
        _clearObsButton.Text = "Clear Obstacles";
        _fullscreenButton.Text = "Fullscreen";

        foreach (var button in new[] { _pauseButton, _clearButton, _clearObsButton, _fullscreenButton })
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(70, 90, 120);
            button.ForeColor = Color.WhiteSmoke;
            button.BackColor = Color.FromArgb(28, 34, 46);
            button.Height = 28;
            button.Width = button == _clearObsButton ? 112 : 98;
            button.Margin = new Padding(6, 0, 0, 0);
        }

        _pauseButton.Click += (_, __) => TogglePause();
        _clearButton.Click += (_, __) => _sim.Clear();
        _clearObsButton.Click += (_, __) => _sim.ClearObstacles();
        _fullscreenButton.Click += (_, __) => ToggleFullscreen();

        _modeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _modeCombo.Width = 110;
        _modeCombo.DataSource = Enum.GetValues<RenderMode>();
        _modeCombo.SelectedItem = RenderMode.Dye;
        _modeCombo.SelectedIndexChanged += (_, __) =>
        {
            if (_modeCombo.SelectedItem is RenderMode mode)
            {
                _renderMode = mode;
            }
        };

        _resolutionCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _resolutionCombo.Width = 120;
        _resolutionCombo.Items.AddRange(new object[]
        {
            "256 x 144",
            "320 x 180",
            "384 x 216",
            "512 x 288",
            "640 x 360"
        });
        _resolutionCombo.SelectedIndexChanged += (_, __) =>
        {
            if (_resolutionCombo.SelectedItem is string text)
            {
                ApplyResolution(text);
            }
        };

        _threadsBox.Minimum = 1;
        _threadsBox.Maximum = Math.Max(1, Environment.ProcessorCount);
        _threadsBox.Value = Math.Max(1, Environment.ProcessorCount / 2);
        _threadsBox.Width = 60;
        _threadsBox.ValueChanged += (_, __) => _sim.ThreadCount = (int)_threadsBox.Value;

        _pressureBox.Minimum = 8;
        _pressureBox.Maximum = 64;
        _pressureBox.Value = 22;
        _pressureBox.Width = 52;
        _pressureBox.ValueChanged += (_, __) => _sim.PressureIterations = (int)_pressureBox.Value;

        _hintLabel.AutoSize = true;
        _hintLabel.ForeColor = Color.Gainsboro;
        _hintLabel.Text = "LMB: dye + force | RMB: obstacle | Shift+RMB: erase obstacle | Space: pause | F: fullscreen | H: shading";
        _hintLabel.Margin = new Padding(14, 7, 0, 0);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            BackColor = Color.Transparent
        };

        flow.Controls.Add(_pauseButton);
        flow.Controls.Add(_clearButton);
        flow.Controls.Add(_clearObsButton);
        flow.Controls.Add(_fullscreenButton);
        flow.Controls.Add(MakeLabel("Mode"));
        flow.Controls.Add(_modeCombo);
        flow.Controls.Add(MakeLabel("Grid"));
        flow.Controls.Add(_resolutionCombo);
        flow.Controls.Add(MakeLabel("Threads"));
        flow.Controls.Add(_threadsBox);
        flow.Controls.Add(MakeLabel("Pressure"));
        flow.Controls.Add(_pressureBox);
        flow.Controls.Add(_hintLabel);

        _topBar.Controls.Add(flow);

        Controls.Add(_canvas);
        Controls.Add(_topBar);
    }

    private static Label MakeLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.Gainsboro,
            Margin = new Padding(12, 7, 0, 0)
        };
    }

    private void ResetDefaults()
    {
        _resolutionCombo.SelectedItem = "384 x 216";
        _threadsBox.Value = Math.Max(1, Math.Min(_threadsBox.Maximum, Math.Max(1, Environment.ProcessorCount / 2)));
        _pressureBox.Value = 22;
    }

    private void ApplyResolution(string text)
    {
        string[] parts = text.Split('x', StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return;

        if (!int.TryParse(parts[0], out int w)) return;
        if (!int.TryParse(parts[1], out int h)) return;

        _sim.Resize(w, h);
        _frameBitmap.Dispose();
        _frameBitmap = new Bitmap(_sim.Width, _sim.Height, PixelFormat.Format32bppArgb);
        _pixels = new byte[_sim.Width * _sim.Height * 4];
    }

    private void Application_Idle(object? sender, EventArgs e)
    {
        while (NativeMethods.AppStillIdle)
        {
            RunFrame();
        }
    }

    private void RunFrame()
    {
        double elapsed = _frameClock.Elapsed.TotalSeconds;
        _frameClock.Restart();
        _lastFrameMs = elapsed * 1000.0;
        _fpsClock.ElapsedMilliseconds.Equals(0);

        float dt = (float)Math.Min(0.05, elapsed);
        _accumulator += dt;
        const float fixedDt = 1f / 90f;
        int simSteps = 0;

        while (_accumulator >= fixedDt && simSteps < 4)
        {
            if (!_paused)
            {
                _sim.Step(fixedDt);
            }
            _accumulator -= fixedDt;
            simSteps++;
        }

        _sim.RenderToBgra(_pixels, _renderMode, _shading);
        UpdateBitmap();
        _canvas.Invalidate();

        _framesThisSecond++;
        if (_fpsClock.Elapsed.TotalSeconds >= 1.0)
        {
            _fps = (float)(_framesThisSecond / _fpsClock.Elapsed.TotalSeconds);
            _framesThisSecond = 0;
            _fpsClock.Restart();
        }
    }

    private void UpdateBitmap()
    {
        Rectangle rect = new Rectangle(0, 0, _frameBitmap.Width, _frameBitmap.Height);
        BitmapData data = _frameBitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(_pixels, 0, data.Scan0, _pixels.Length);
        }
        finally
        {
            _frameBitmap.UnlockBits(data);
        }
    }

    private void Canvas_Paint(object? sender, PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Black);
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.SmoothingMode = SmoothingMode.HighSpeed;
        e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;

        Rectangle dest = GetDestinationRect(_canvas.ClientRectangle, _sim.Width, _sim.Height);
        e.Graphics.DrawImage(_frameBitmap, dest);

        DrawHud(e.Graphics, dest);
    }

    private void DrawHud(Graphics g, Rectangle dest)
    {
        using SolidBrush bg = new(Color.FromArgb(160, 8, 12, 18));
        using Pen pen = new(Color.FromArgb(120, 95, 140, 210), 1f);
        using SolidBrush fg = new(Color.WhiteSmoke);

        string line1 = $"Real-Time CFD | Render: {_renderMode} | Grid: {_sim.Width}x{_sim.Height} | Threads: {_sim.ThreadCount}";
        string line2 = $"Frame: {_lastFrameMs:F2} ms | FPS: {_fps:F1} | Pressure Iterations: {_sim.PressureIterations}";
        string line3 = "Current path: CPU solver + optimized bitmap renderer. This rebuild is honest and much cleaner, but not GPU compute yet.";

        SizeF s1 = g.MeasureString(line1, Font);
        SizeF s2 = g.MeasureString(line2, Font);
        SizeF s3 = g.MeasureString(line3, Font);
        int boxW = (int)Math.Ceiling(Math.Max(s1.Width, Math.Max(s2.Width, s3.Width))) + 18;
        int boxH = 58;
        Rectangle hud = new(dest.X + 10, dest.Y + 10, boxW, boxH);

        g.FillRectangle(bg, hud);
        g.DrawRectangle(pen, hud);
        g.DrawString(line1, Font, fg, hud.X + 8, hud.Y + 7);
        g.DrawString(line2, Font, fg, hud.X + 8, hud.Y + 24);
        g.DrawString(line3, Font, fg, hud.X + 8, hud.Y + 41);
    }

    private static Rectangle GetDestinationRect(Rectangle client, int srcW, int srcH)
    {
        float scale = Math.Min(client.Width / (float)srcW, client.Height / (float)srcH);
        int drawW = (int)(srcW * scale);
        int drawH = (int)(srcH * scale);
        int x = client.X + (client.Width - drawW) / 2;
        int y = client.Y + (client.Height - drawH) / 2;
        return new Rectangle(x, y, drawW, drawH);
    }

    private Point ToSimPoint(Point clientPoint)
    {
        Rectangle dest = GetDestinationRect(_canvas.ClientRectangle, _sim.Width, _sim.Height);
        if (!dest.Contains(clientPoint))
        {
            return new Point(-1, -1);
        }

        float sx = (clientPoint.X - dest.X) / (float)dest.Width;
        float sy = (clientPoint.Y - dest.Y) / (float)dest.Height;
        int x = Math.Clamp((int)(sx * _sim.Width), 0, _sim.Width - 1);
        int y = Math.Clamp((int)(sy * _sim.Height), 0, _sim.Height - 1);
        return new Point(x, y);
    }

    private void Canvas_MouseDown(object? sender, MouseEventArgs e)
    {
        _mouseDown = true;
        _eraseObstacle = (ModifierKeys & Keys.Shift) == Keys.Shift && e.Button == MouseButtons.Right;
        _lastMouse = e.Location;
        ApplyMouseTool(e.Location, e.Button, 0, 0);
    }

    private void Canvas_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_mouseDown) return;

        int dx = e.Location.X - _lastMouse.X;
        int dy = e.Location.Y - _lastMouse.Y;
        ApplyMouseTool(e.Location, e.Button, dx, dy);
        _lastMouse = e.Location;
    }

    private void Canvas_MouseWheel(object? sender, MouseEventArgs e)
    {
        _sim.VorticityConfinement = Math.Clamp(_sim.VorticityConfinement + (e.Delta > 0 ? 0.03f : -0.03f), 0.05f, 1.25f);
    }

    private void ApplyMouseTool(Point location, MouseButtons button, int dx, int dy)
    {
        Point p = ToSimPoint(location);
        if (p.X < 0) return;

        if (button == MouseButtons.Right)
        {
            _sim.PaintSolidCircle(p.X, p.Y, 8, !_eraseObstacle);
            return;
        }

        if (button != MouseButtons.Left) return;

        _hue += 0.004f;
        float r = 0f, g = 0f, b = 0f;
        ColorFromHue(_hue, out r, out g, out b);

        float fx = dx * 0.016f;
        float fy = dy * 0.016f;
        _sim.AddImpulseAndDye(p.X, p.Y, 12, fx, fy, r * 1.3f, g * 1.3f, b * 1.3f);
    }

    private static void ColorFromHue(float hue, out float r, out float g, out float b)
    {
        hue -= MathF.Floor(hue);
        float c = 1f;
        float x = 1f - MathF.Abs((hue * 6f % 2f) - 1f);

        if (hue < 1f / 6f)      { r = c; g = x; b = 0f; }
        else if (hue < 2f / 6f) { r = x; g = c; b = 0f; }
        else if (hue < 3f / 6f) { r = 0f; g = c; b = x; }
        else if (hue < 4f / 6f) { r = 0f; g = x; b = c; }
        else if (hue < 5f / 6f) { r = x; g = 0f; b = c; }
        else                    { r = c; g = 0f; b = x; }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Space:
                TogglePause();
                e.Handled = true;
                break;
            case Keys.C:
                _sim.Clear();
                break;
            case Keys.F:
                ToggleFullscreen();
                break;
            case Keys.H:
                _shading = !_shading;
                break;
            case Keys.D1:
                _modeCombo.SelectedItem = RenderMode.Dye;
                break;
            case Keys.D2:
                _modeCombo.SelectedItem = RenderMode.Velocity;
                break;
            case Keys.D3:
                _modeCombo.SelectedItem = RenderMode.Pressure;
                break;
            case Keys.D4:
                _modeCombo.SelectedItem = RenderMode.Divergence;
                break;
            case Keys.Oemplus:
            case Keys.Add:
                if (_threadsBox.Value < _threadsBox.Maximum) _threadsBox.Value += 1;
                break;
            case Keys.OemMinus:
            case Keys.Subtract:
                if (_threadsBox.Value > _threadsBox.Minimum) _threadsBox.Value -= 1;
                break;
        }
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _pauseButton.Text = _paused ? "Resume" : "Pause";
    }

    private void ToggleFullscreen()
    {
        if (FormBorderStyle != FormBorderStyle.None)
        {
            _restoreWindowState = WindowState;
            _restoreBorderStyle = FormBorderStyle;
            _restoreTopMost = TopMost;
            _restoreBounds = Bounds;

            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            TopMost = true;
        }
        else
        {
            TopMost = _restoreTopMost;
            FormBorderStyle = _restoreBorderStyle;
            Bounds = _restoreBounds;
            WindowState = _restoreWindowState;
        }
    }
}
