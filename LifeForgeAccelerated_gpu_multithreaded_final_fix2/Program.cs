using OpenTK.Mathematics;
using System.Drawing;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace LifeForgeAccelerated;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var gameSettings = GameWindowSettings.Default;
        gameSettings.UpdateFrequency = 0;

        var nativeSettings = new NativeWindowSettings
        {
            Title = "LifeForge Accelerated",
            ClientSize = new Vector2i(1600, 900),
            NumberOfSamples = 4,
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible,
            StartFocused = true,
            StartVisible = true,
            WindowBorder = WindowBorder.Resizable,
            WindowState = WindowState.Normal
        };

        using var window = new LifeForgeWindow(gameSettings, nativeSettings);
        window.Run();
    }
}

internal sealed class LifeForgeWindow : GameWindow
{
    private readonly World _world;
    private readonly GpuRenderer _renderer;
    private readonly OverlayComposer _overlay;

    private bool _paused;
    private bool _showHelp;
    private bool _uiDirty = true;
    private bool _mouseUsesBottomOrigin;
    private bool _mouseOriginKnown;
    private double _uiRefreshTimer;
    private double _fpsTimer;
    private int _fpsFrames;
    private float _fps;
    private float _simSpeed = 2.0f;
    private WindowState _restoreWindowState = WindowState.Normal;
    private Vector2i _restoreClientSize;
    private Vector2i _restoreLocation;

    public LifeForgeWindow(GameWindowSettings gameSettings, NativeWindowSettings nativeSettings)
        : base(gameSettings, nativeSettings)
    {
        VSync = VSyncMode.On;
        _restoreClientSize = nativeSettings.ClientSize;
        _restoreLocation = Location;

        var content = GetContentSize();
        _world = new World(content.X, content.Y, Environment.ProcessorCount);
        _renderer = new GpuRenderer();
        _overlay = new OverlayComposer();
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        _renderer.Initialize();

        var content = GetContentSize();
        GL.Viewport(0, 0, content.X, content.Y);
        _renderer.UpdateTerrainTexture(_overlay.BuildTerrain(content.X, content.Y, _world));
        RebuildOverlay(force: true);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        var content = GetContentSize();
        GL.Viewport(0, 0, content.X, content.Y);
        _world.Resize(content.X, content.Y);
        _renderer.UpdateTerrainTexture(_overlay.BuildTerrain(content.X, content.Y, _world));
        RebuildOverlay(force: true);
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);

        if (!IsFocused)
        {
            return;
        }

        if (!_paused)
        {
            _world.Advance((float)e.Time, _simSpeed);
        }

        _uiRefreshTimer += e.Time;
        _fpsTimer += e.Time;
        _fpsFrames++;

        if (_fpsTimer >= 0.25)
        {
            _fps = (float)(_fpsFrames / _fpsTimer);
            _fpsFrames = 0;
            _fpsTimer = 0;
            _uiDirty = true;
        }

        if (_uiRefreshTimer >= 0.20)
        {
            _uiRefreshTimer = 0;
            RebuildOverlay(force: false);
        }
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);
        var content = GetContentSize();
        _renderer.Render(_world, content.X, content.Y);
        SwapBuffers();
    }

    protected override void OnUnload()
    {
        base.OnUnload();
        _renderer.Dispose();
    }

    protected override void OnMouseDown(OpenTK.Windowing.Common.MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        var content = GetContentSize();
        var mouse = MousePosition;
        var raw = new PointF(
            Math.Clamp((float)mouse.X, 0f, content.X),
            Math.Clamp((float)mouse.Y, 0f, content.Y));
        var flipped = new PointF(raw.X, content.Y - raw.Y);

        if (TryHandleOverlayClick(raw, flipped, out var action))
        {
            switch (action)
            {
                case OverlayAction.TogglePause:
                    _paused = !_paused;
                    _uiDirty = true;
                    break;
                case OverlayAction.ResetWorld:
                    _world.Reset();
                    _renderer.UpdateTerrainTexture(_overlay.BuildTerrain(content.X, content.Y, _world));
                    _uiDirty = true;
                    break;
                case OverlayAction.ToggleFullscreen:
                    ToggleFullscreen();
                    break;
                case OverlayAction.ToggleHelp:
                    _showHelp = !_showHelp;
                    _uiDirty = true;
                    break;
                case OverlayAction.SpeedDown:
                    _simSpeed = MathF.Max(0.25f, _simSpeed - 0.25f);
                    _uiDirty = true;
                    break;
                case OverlayAction.SpeedUp:
                    _simSpeed = MathF.Min(12.0f, _simSpeed + 0.25f);
                    _uiDirty = true;
                    break;
                case OverlayAction.WorkerDown:
                    _world.SetWorkerCount(Math.Max(1, _world.WorkerThreads - 1));
                    _uiDirty = true;
                    break;
                case OverlayAction.WorkerUp:
                    _world.SetWorkerCount(Math.Min(Environment.ProcessorCount, _world.WorkerThreads + 1));
                    _uiDirty = true;
                    break;
            }

            RebuildOverlay(force: true);
            return;
        }

        var worldPoint = ConvertMouseToWorldPoint(raw, flipped);
        _world.SelectAt(worldPoint.X, worldPoint.Y, 34f);
        _uiDirty = true;
        RebuildOverlay(force: true);
    }

    protected override void OnKeyDown(OpenTK.Windowing.Common.KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case OpenTK.Windowing.GraphicsLibraryFramework.Keys.Space:
                _paused = !_paused;
                _uiDirty = true;
                break;
            case OpenTK.Windowing.GraphicsLibraryFramework.Keys.F11:
                ToggleFullscreen();
                break;
            case OpenTK.Windowing.GraphicsLibraryFramework.Keys.Escape:
                if (WindowState == WindowState.Fullscreen)
                {
                    ToggleFullscreen();
                }
                break;
            case OpenTK.Windowing.GraphicsLibraryFramework.Keys.R:
                _world.Reset();
                {
                    var content = GetContentSize();
                    _renderer.UpdateTerrainTexture(_overlay.BuildTerrain(content.X, content.Y, _world));
                }
                _uiDirty = true;
                break;
            case OpenTK.Windowing.GraphicsLibraryFramework.Keys.H:
                _showHelp = !_showHelp;
                _uiDirty = true;
                break;
            case OpenTK.Windowing.GraphicsLibraryFramework.Keys.Minus:
            case OpenTK.Windowing.GraphicsLibraryFramework.Keys.KeyPadSubtract:
                _simSpeed = MathF.Max(0.25f, _simSpeed - 0.25f);
                _uiDirty = true;
                break;
            case OpenTK.Windowing.GraphicsLibraryFramework.Keys.Equal:
            case OpenTK.Windowing.GraphicsLibraryFramework.Keys.KeyPadAdd:
                _simSpeed = MathF.Min(12.0f, _simSpeed + 0.25f);
                _uiDirty = true;
                break;
            case OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftBracket:
                _world.SetWorkerCount(Math.Max(1, _world.WorkerThreads - 1));
                _uiDirty = true;
                break;
            case OpenTK.Windowing.GraphicsLibraryFramework.Keys.RightBracket:
                _world.SetWorkerCount(Math.Min(Environment.ProcessorCount, _world.WorkerThreads + 1));
                _uiDirty = true;
                break;
        }

        RebuildOverlay(force: true);
    }

    private void ToggleFullscreen()
    {
        if (WindowState == WindowState.Fullscreen)
        {
            WindowState = _restoreWindowState == WindowState.Fullscreen ? WindowState.Normal : _restoreWindowState;
            ClientSize = _restoreClientSize;
            Location = _restoreLocation;
        }
        else
        {
            _restoreWindowState = WindowState;
            _restoreClientSize = GetContentSize();
            _restoreLocation = Location;
            WindowState = WindowState.Fullscreen;
        }

        _uiDirty = true;
        RebuildOverlay(force: true);
    }

    private void RebuildOverlay(bool force)
    {
        if (!force && !_uiDirty)
        {
            return;
        }

        _uiDirty = false;
        var content = GetContentSize();
        _renderer.UpdateOverlayTexture(_overlay.Compose(content.X, content.Y, _world, _paused, _showHelp, _simSpeed, _fps, WindowState == WindowState.Fullscreen));
    }

    private bool TryHandleOverlayClick(PointF raw, PointF flipped, out OverlayAction action)
    {
        if (_mouseOriginKnown)
        {
            return _overlay.TryHandleClick(_mouseUsesBottomOrigin ? flipped : raw, out action);
        }

        if (_overlay.TryHandleClick(raw, out action))
        {
            _mouseUsesBottomOrigin = false;
            _mouseOriginKnown = true;
            return true;
        }

        if (_overlay.TryHandleClick(flipped, out action))
        {
            _mouseUsesBottomOrigin = true;
            _mouseOriginKnown = true;
            return true;
        }

        action = OverlayAction.None;
        return false;
    }

    private PointF ConvertMouseToWorldPoint(PointF raw, PointF flipped)
    {
        return _mouseOriginKnown && _mouseUsesBottomOrigin ? flipped : raw;
    }

    private Vector2i GetContentSize()
    {
        var size = ClientSize;
        if (size.X <= 0 || size.Y <= 0)
        {
            size = Size;
        }

        return new Vector2i(Math.Max(1, size.X), Math.Max(1, size.Y));
    }
}
