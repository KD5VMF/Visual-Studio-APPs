using OpenTK.Mathematics;
using System.Drawing;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace BioGenesisX;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var gameSettings = GameWindowSettings.Default;
        gameSettings.UpdateFrequency = 0;

        var nativeSettings = new NativeWindowSettings
        {
            Title = "BioGenesis X: Emergent Life Simulator",
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
    private bool _showUi = true;
    private bool _uiDirty = true;
    private bool _mouseUsesBottomOrigin;
    private bool _mouseOriginKnown;
    private double _uiRefreshTimer;
    private double _fpsTimer;
    private int _fpsFrames;
    private float _fps;
    private float _simSpeed = 2.0f;
    private Vec2 _cameraCenter;
    private float _cameraZoom = 1.0f;
    private bool _cameraFollowSelection = true;
    private Vector2 _lastMousePosition;
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
        _world = new World(content.X * 3, content.Y * 3, Environment.ProcessorCount);
        _renderer = new GpuRenderer();
        _overlay = new OverlayComposer();
        _cameraCenter = _world.WorldCenter;
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
        _world.Resize(Math.Max(_world.Width, content.X * 3), Math.Max(_world.Height, content.Y * 3));
        _cameraCenter = ClampCameraCenter(_cameraCenter);
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

        HandleCamera((float)e.Time);

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
        _renderer.Render(_world, content.X, content.Y, _cameraCenter, _cameraZoom);
        SwapBuffers();
    }

    protected override void OnUnload()
    {
        _world.SaveTrainingNow("shutdown");
        base.OnUnload();
        _renderer.Dispose();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
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
                    _world.SaveTrainingNow("manual-reset");
                    _world.Reset();
                    _cameraCenter = _world.WorldCenter;
                    _cameraZoom = 1.0f;
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
                case OverlayAction.ToggleUi:
                    _showUi = !_showUi;
                    if (!_showUi) _showHelp = false;
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

        var worldPoint = ScreenToWorld(raw.X, raw.Y);
        _world.SelectAt(worldPoint.X, worldPoint.Y, Math.Max(18f, 24f / _cameraZoom));
        _cameraFollowSelection = _world.Selection.Kind == SelectionKind.Agent;
        _uiDirty = true;
        RebuildOverlay(force: true);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        var before = ScreenToWorld(MousePosition.X, MousePosition.Y);
        _cameraZoom = Math.Clamp(_cameraZoom * (e.OffsetY > 0 ? 1.12f : 0.89f), 0.35f, 18f);
        var after = ScreenToWorld(MousePosition.X, MousePosition.Y);
        _cameraCenter += before - after;
        _cameraCenter = ClampCameraCenter(_cameraCenter);
        _uiDirty = true;
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
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
                _world.SaveTrainingNow("manual-reset");
                _world.Reset();
                _cameraCenter = _world.WorldCenter;
                _cameraZoom = 1.0f;
                {
                    var content = GetContentSize();
                    _renderer.UpdateTerrainTexture(_overlay.BuildTerrain(content.X, content.Y, _world));
                }
                _uiDirty = true;
                break;
            case OpenTK.Windowing.GraphicsLibraryFramework.Keys.H:
                _showHelp = !_showHelp;
                _showUi = true;
                _uiDirty = true;
                break;
            case OpenTK.Windowing.GraphicsLibraryFramework.Keys.Tab:
                _showUi = !_showUi;
                if (!_showUi) _showHelp = false;
                _uiDirty = true;
                break;
            case OpenTK.Windowing.GraphicsLibraryFramework.Keys.F:
                _cameraFollowSelection = _world.Selection.Kind == SelectionKind.Agent;
                if (_cameraFollowSelection && _world.Selection.Agent is not null)
                {
                    _cameraCenter = ClampCameraCenter(_world.Selection.Agent.Position);
                    _cameraZoom = Math.Max(_cameraZoom, 2.2f);
                }
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
        _renderer.UpdateOverlayTexture(_overlay.Compose(content.X, content.Y, _world, _paused, _showHelp, _showUi, _simSpeed, _fps, WindowState == WindowState.Fullscreen));
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

    private void HandleCamera(float dt)
    {
        var mouse = MousePosition;
        var delta = mouse - _lastMousePosition;
        _lastMousePosition = mouse;

        if (MouseState.IsButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Right))
        {
            _cameraFollowSelection = false;
            _cameraCenter -= new Vec2(delta.X / _cameraZoom, delta.Y / _cameraZoom);
            _cameraCenter = ClampCameraCenter(_cameraCenter);
        }

        if (_cameraFollowSelection && _world.Selection.Kind == SelectionKind.Agent && _world.Selection.Agent is not null && !_world.Selection.Agent.IsDead)
        {
            _cameraCenter = Vec2.Lerp(_cameraCenter, _world.Selection.Agent.Position, Math.Clamp(dt * 3.4f, 0f, 1f));
            _cameraCenter = ClampCameraCenter(_cameraCenter);
        }
    }

    private Vec2 ScreenToWorld(float screenX, float screenY)
    {
        var content = GetContentSize();
        var dx = (screenX - content.X * 0.5f) / _cameraZoom;
        var dy = (screenY - content.Y * 0.5f) / _cameraZoom;
        return new Vec2(_cameraCenter.X + dx, _cameraCenter.Y + dy);
    }

    private Vec2 ClampCameraCenter(Vec2 center)
    {
        var content = GetContentSize();
        var halfW = content.X * 0.5f / Math.Max(0.01f, _cameraZoom);
        var halfH = content.Y * 0.5f / Math.Max(0.01f, _cameraZoom);
        return new Vec2(
            Math.Clamp(center.X, halfW, Math.Max(halfW, _world.Width - halfW)),
            Math.Clamp(center.Y, halfH, Math.Max(halfH, _world.Height - halfH)));
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
