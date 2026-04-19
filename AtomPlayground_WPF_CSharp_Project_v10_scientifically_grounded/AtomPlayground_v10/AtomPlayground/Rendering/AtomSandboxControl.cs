using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AtomPlayground.Models;
using AtomPlayground.Simulation;

namespace AtomPlayground.Rendering;

public sealed class AtomSandboxControl : FrameworkElement
{
    private readonly SimulationWorld _world = new();
    private DateTime _lastFrame = DateTime.UtcNow;
    private double _zoom = 1.00;
    private AtomState? _slingAtom;
    private Point _slingAnchor;
    private Point _slingPreview;
    private bool _isSlingDragging;
    private double _fps;
    private readonly Typeface _headerTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private readonly Typeface _bodyTypeface = new(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private bool _initialized;
    private string? _fatalError;

    public AtomSandboxControl()
    {
        Focusable = true;
        SnapsToDevicePixels = true;
        ClipToBounds = true;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => UpdateWorldViewportBounds();
        _world.EventLogged += (_, e) => EventLogged?.Invoke(this, e);
    }

    public SimulationWorld World => _world;

    public event EventHandler? SelectionChanged;
    public event EventHandler<WorldEvent>? EventLogged;

    public AtomState? PrimarySelection => _world.GetSelectedAtoms().FirstOrDefault();

    public void InitializeIfNeeded()
    {
        if (_initialized)
        {
            return;
        }

        UpdateWorldViewportBounds();
        _world.Reset();
        _initialized = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        return finalSize;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeIfNeeded();
        _lastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering -= CompositionTargetOnRendering;
        CompositionTarget.Rendering += CompositionTargetOnRendering;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= CompositionTargetOnRendering;
    }

    private void CompositionTargetOnRendering(object? sender, EventArgs e)
    {
        if (!_initialized || !IsLoaded || ActualWidth <= 0 || ActualHeight <= 0 || _fatalError is not null)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            var dt = (now - _lastFrame).TotalSeconds;
            _lastFrame = now;
            if (dt > 0)
            {
                _fps = 1.0 / Math.Max(0.0001, dt);
            }

            UpdateWorldViewportBounds();
            _world.Update(dt);

            if (_slingAtom is not null && _isSlingDragging)
            {
                _slingAtom.Position = _slingAnchor;
                _slingAtom.Velocity = default;
            }

            InvalidateVisual();
        }
        catch (Exception ex)
        {
            _fatalError = ex.ToString();
            try
            {
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup-error.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Renderer failure\n{ex}\n{new string('-', 80)}\n");
            }
            catch
            {
            }
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var viewport = new Rect(new Point(0, 0), RenderSize);
        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(8, 14, 26), Color.FromRgb(19, 26, 44), 65), null, viewport);

        if (_fatalError is not null)
        {
            var errorText = new FormattedText(
                "Renderer error. See startup-error.log next to the EXE.\n\n" + _fatalError,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                _bodyTypeface,
                16,
                Brushes.OrangeRed,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = Math.Max(200, RenderSize.Width - 40)
            };
            dc.DrawText(errorText, new Point(20, 20));
            return;
        }

        dc.PushClip(new RectangleGeometry(viewport, 18, 18));
        dc.PushTransform(new ScaleTransform(_zoom, _zoom));

        if (_world.ShowGrid)
        {
            DrawGrid(dc);
        }

        if (_world.ShowBonds)
        {
            DrawBonds(dc);
        }

        if (_world.ShowTrails)
        {
            DrawTrails(dc);
        }

        foreach (var atom in _world.Atoms.OrderBy(a => a.MassNumber))
        {
            if (_isSlingDragging && ReferenceEquals(atom, _slingAtom))
            {
                continue;
            }

            DrawAtom(dc, atom);
        }

        if (_isSlingDragging && _slingAtom is not null)
        {
            DrawSlingOverlay(dc, _slingAtom);
        }

        dc.Pop();
        dc.Pop();

        if (_world.ShowHud)
        {
            DrawHud(dc);
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        _zoom = e.Delta > 0 ? _zoom * 1.10 : _zoom / 1.10;
        _zoom = Math.Clamp(_zoom, 0.55, 2.40);
        UpdateWorldViewportBounds();
        InvalidateVisual();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var mousePoint = e.GetPosition(this);

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        CaptureMouse();

        var worldPoint = ScreenToWorld(mousePoint);
        var hit = HitTestAtom(worldPoint);
        if (hit is null)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                _world.ClearSelection();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            _world.ToggleSelection(hit);
        }
        else
        {
            _world.SelectSingle(hit);
        }

        _slingAtom = hit;
        _slingAnchor = hit.Position;
        _slingPreview = hit.Position;
        _isSlingDragging = false;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var screenPoint = e.GetPosition(this);

        if (_slingAtom is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            var worldPoint = ClampToVisibleWorld(ScreenToWorld(screenPoint), _slingAtom.RenderRadius + 22);
            if ((worldPoint - _slingAnchor).Length >= 6)
            {
                _isSlingDragging = true;
            }

            if (_isSlingDragging)
            {
                _slingPreview = worldPoint;
                _slingAtom.Velocity *= 0.2;
                InvalidateVisual();
            }
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        ReleaseMouseCapture();

        if (e.ChangedButton == MouseButton.Left && _slingAtom is not null && _isSlingDragging)
        {
            var pullVector = _slingAnchor - _slingPreview;
            var launchVelocity = pullVector * 2.4;
            var speed = launchVelocity.Length;
            if (speed > 420)
            {
                launchVelocity *= 420 / speed;
            }

            _world.LaunchAtom(_slingAtom, _slingPreview, launchVelocity);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        _slingAtom = null;
        _isSlingDragging = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Space:
                _world.Paused = !_world.Paused;
                e.Handled = true;
                break;
            case Key.Delete:
                _world.RemoveSelectedAtoms();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
        }
    }

    private void DrawGrid(DrawingContext dc)
    {
        var worldLeft = ScreenToWorld(new Point(0, 0));
        var worldRight = ScreenToWorld(new Point(RenderSize.Width, RenderSize.Height));
        const int spacing = 140;

        var thinPen = new Pen(new SolidColorBrush(Color.FromArgb(48, 100, 135, 180)), 1.0 / _zoom);
        var thickPen = new Pen(new SolidColorBrush(Color.FromArgb(70, 130, 180, 230)), 1.2 / _zoom);
        thinPen.Freeze();
        thickPen.Freeze();

        var startX = Math.Floor(worldLeft.X / spacing) * spacing;
        var endX = Math.Ceiling(worldRight.X / spacing) * spacing;
        var startY = Math.Floor(worldLeft.Y / spacing) * spacing;
        var endY = Math.Ceiling(worldRight.Y / spacing) * spacing;

        for (var x = startX; x <= endX; x += spacing)
        {
            var pen = ((int)(x / spacing)) % 5 == 0 ? thickPen : thinPen;
            dc.DrawLine(pen, new Point(x, startY), new Point(x, endY));
        }

        for (var y = startY; y <= endY; y += spacing)
        {
            var pen = ((int)(y / spacing)) % 5 == 0 ? thickPen : thinPen;
            dc.DrawLine(pen, new Point(startX, y), new Point(endX, y));
        }
    }

    private void DrawBonds(DrawingContext dc)
    {
        foreach (var bond in _world.Bonds)
        {
            var a = bond.A.Position;
            var b = bond.B.Position;
            var color = bond.Ionic ? Color.FromArgb(180, 125, 220, 255) : Color.FromArgb(170, 120, 255, 210);
            var pen = new Pen(new SolidColorBrush(color), (bond.Ionic ? 2.4 : 2.0 + bond.Order * 0.8) / _zoom)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawLine(pen, a, b);
        }
    }

    private void DrawTrails(DrawingContext dc)
    {
        foreach (var atom in _world.Atoms)
        {
            if (atom.Trail.Count < 2)
            {
                continue;
            }

            var points = atom.Trail.ToArray();
            for (var i = 1; i < points.Length; i++)
            {
                var alpha = (byte)Math.Clamp(25 + i * 4, 25, 140);
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, atom.PrimaryColor.R, atom.PrimaryColor.G, atom.PrimaryColor.B)), 1.4 / _zoom);
                dc.DrawLine(pen, points[i - 1], points[i]);
            }
        }
    }

    private void DrawAtom(DrawingContext dc, AtomState atom, Point? overrideCenter = null)
    {
        var center = overrideCenter ?? atom.Position;
        var glowRadius = atom.RenderRadius * 2.2;
        var glowBrush = new RadialGradientBrush(
            Color.FromArgb(atom.Selected ? (byte)90 : (byte)55, atom.PrimaryColor.R, atom.PrimaryColor.G, atom.PrimaryColor.B),
            Color.FromArgb(0, atom.PrimaryColor.R, atom.PrimaryColor.G, atom.PrimaryColor.B))
        {
            RadiusX = 1,
            RadiusY = 1,
            GradientOrigin = new Point(0.45, 0.45),
            Center = new Point(0.5, 0.5)
        };
        dc.DrawEllipse(glowBrush, null, center, glowRadius, glowRadius);

        var nucleusShellBrush = new RadialGradientBrush(
            Color.FromArgb(240, 255, 255, 255),
            Color.FromArgb(230, atom.PrimaryColor.R, atom.PrimaryColor.G, atom.PrimaryColor.B))
        {
            RadiusX = 0.95,
            RadiusY = 0.95,
            GradientOrigin = new Point(0.35, 0.35),
            Center = new Point(0.5, 0.5)
        };
        dc.DrawEllipse(nucleusShellBrush, new Pen(new SolidColorBrush(Color.FromRgb(245, 250, 255)), 1.6 / _zoom), center, atom.RenderRadius, atom.RenderRadius);

        if (_world.ShowParticleInternals)
        {
            DrawNucleonInternals(dc, atom, center);
        }

        if (_world.ShowShells)
        {
            DrawElectronShells(dc, atom, center);
        }

        if (atom.Selected)
        {
            var selectionPen = new Pen(new SolidColorBrush(Color.FromRgb(120, 220, 255)), 2.5 / _zoom);
            dc.DrawEllipse(null, selectionPen, center, atom.RenderRadius + 9, atom.RenderRadius + 9);
        }

        var label = new FormattedText(
            atom.Symbol,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            _headerTypeface,
            Math.Max(10, atom.RenderRadius * 0.88),
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(label, new Point(center.X - label.Width / 2, center.Y - label.Height / 2));
    }

    private void DrawNucleonInternals(DrawingContext dc, AtomState atom, Point center)
    {
        var total = atom.MassNumber;
        if (total <= 0)
        {
            return;
        }

        var coreRadius = Math.Max(5.0, atom.RenderRadius - 2.5);
        var dotRadius = Math.Clamp(coreRadius / (Math.Sqrt(total) + 2.0), 1.4, 4.2);
        var innerRadius = Math.Max(0, coreRadius - dotRadius - 1.5);
        var points = BuildPackedPoints(center, total, innerRadius, atom.Id.GetHashCode());
        var protonBrush = new RadialGradientBrush(Color.FromRgb(255, 250, 250), Color.FromRgb(255, 120, 120));
        var neutronBrush = new RadialGradientBrush(Color.FromRgb(250, 255, 255), Color.FromRgb(120, 180, 255));
        var protonPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 220, 220)), 0.4 / _zoom);
        var neutronPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 220, 235, 255)), 0.4 / _zoom);

        for (var i = 0; i < points.Count; i++)
        {
            var isProton = ShouldDrawProton(i, total, atom.Protons);
            dc.DrawEllipse(isProton ? protonBrush : neutronBrush, isProton ? protonPen : neutronPen, points[i], dotRadius, dotRadius);
        }
    }

    private void DrawElectronShells(DrawingContext dc, AtomState atom, Point center)
    {
        var shellCounts = GetShellCounts(atom.Electrons);
        var baseRadius = atom.RenderRadius + 18;
        var shellPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 180, 220, 255)), 1.0 / _zoom);

        for (var shellIndex = 0; shellIndex < shellCounts.Count; shellIndex++)
        {
            var shellRadius = baseRadius + shellIndex * 18;
            dc.DrawEllipse(null, shellPen, center, shellRadius, shellRadius);

            var count = shellCounts[shellIndex];
            for (var i = 0; i < count; i++)
            {
                var angle = atom.RotationPhase * (0.7 + shellIndex * 0.2) + i * (Math.PI * 2.0 / count);
                var px = center.X + Math.Cos(angle) * shellRadius;
                var py = center.Y + Math.Sin(angle) * shellRadius;
                var electronBrush = new RadialGradientBrush(Color.FromRgb(240, 250, 255), Color.FromRgb(90, 180, 255));
                dc.DrawEllipse(electronBrush, null, new Point(px, py), 3.5 + shellIndex * 0.25, 3.5 + shellIndex * 0.25);
            }
        }
    }

    private void DrawSlingOverlay(DrawingContext dc, AtomState atom)
    {
        var linePen = new Pen(new SolidColorBrush(Color.FromArgb(215, 120, 220, 255)), 2.6 / _zoom)
        {
            DashStyle = DashStyles.Solid,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawLine(linePen, _slingAnchor, _slingPreview);

        var anchorBrush = new SolidColorBrush(Color.FromArgb(130, 120, 220, 255));
        dc.DrawEllipse(anchorBrush, null, _slingAnchor, 4.5 / _zoom, 4.5 / _zoom);

        DrawAtom(dc, atom, _slingPreview);

        var pullVector = _slingAnchor - _slingPreview;
        var launchSpeed = Math.Min(420, pullVector.Length * 2.4);
        var cue = new FormattedText(
            $"Launch speed: {launchSpeed:0}",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            _bodyTypeface,
            13 / _zoom,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(cue, new Point(_slingPreview.X + (18 / _zoom), _slingPreview.Y - (30 / _zoom)));
    }

    private void DrawHud(DrawingContext dc)
    {
        var panel = new Rect(18, 18, 340, 154);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(180, 8, 14, 24)), new Pen(new SolidColorBrush(Color.FromArgb(120, 70, 120, 190)), 1), panel, 16, 16);

        var lines = new[]
        {
            $"Mode: {_world.Mode}",
            $"Atoms: {_world.Atoms.Count}",
            $"Bonds: {_world.Bonds.Count}",
            $"Time scale: {_world.TimeScale:0.0}x",
            $"Collision energy: {_world.SmashEnergy:0}",
            $"Particle view: full electrons + nucleons",
            $"FPS: {_fps:0}",
            $"Sim time: {_world.SimulatedSeconds:0.0}s"
        };

        var y = 32.0;
        foreach (var line in lines)
        {
            var ft = new FormattedText(
                line,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                _bodyTypeface,
                16,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft, new Point(32, y));
            y += 18.5;
        }
    }

    private AtomState? HitTestAtom(Point worldPoint)
    {
        return _world.Atoms
            .OrderByDescending(a => a.Selected)
            .ThenByDescending(a => a.MassNumber)
            .FirstOrDefault(a => (a.Position - worldPoint).Length <= a.RenderRadius + 10);
    }

    private static bool ShouldDrawProton(int index, int total, int protonCount)
    {
        if (protonCount <= 0)
        {
            return false;
        }

        return Math.Round((index + 1) * protonCount / (double)total) > Math.Round(index * protonCount / (double)total);
    }

    private static List<Point> BuildPackedPoints(Point center, int count, double radius, int seed)
    {
        var points = new List<Point>(count);
        if (count <= 0)
        {
            return points;
        }

        if (count == 1 || radius <= 0.01)
        {
            points.Add(center);
            return points;
        }

        var goldenAngle = Math.PI * (3 - Math.Sqrt(5));
        var twist = (seed & 1023) / 1023.0 * Math.PI * 2.0;
        for (var i = 0; i < count; i++)
        {
            var t = count == 1 ? 0.0 : i / (double)(count - 1);
            var r = radius * Math.Sqrt(t);
            var angle = i * goldenAngle + twist;
            points.Add(new Point(center.X + Math.Cos(angle) * r, center.Y + Math.Sin(angle) * r));
        }

        return points;
    }

    public void ResetView()
    {
        _zoom = 1.0;
        UpdateWorldViewportBounds();
        InvalidateVisual();
    }

    private void UpdateWorldViewportBounds()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        _world.SetBounds(new Size(Math.Max(500, ActualWidth / _zoom), Math.Max(340, ActualHeight / _zoom)));
    }

    private Point ScreenToWorld(Point screenPoint)
    {
        return new Point(screenPoint.X / _zoom, screenPoint.Y / _zoom);
    }

    private Point ClampToVisibleWorld(Point point, double margin)
    {
        var bounds = _world.Bounds;
        var clampedX = Math.Clamp(point.X, margin, Math.Max(margin, bounds.Width - margin));
        var clampedY = Math.Clamp(point.Y, margin, Math.Max(margin, bounds.Height - margin));
        return new Point(clampedX, clampedY);
    }

    private static List<int> GetShellCounts(int electrons)
    {
        var capacities = new[] { 2, 8, 18, 32, 32, 18, 8 };
        var remaining = Math.Max(0, electrons);
        var result = new List<int>();

        foreach (var cap in capacities)
        {
            if (remaining <= 0)
            {
                break;
            }

            var fill = Math.Min(cap, remaining);
            result.Add(fill);
            remaining -= fill;
        }

        if (result.Count == 0)
        {
            result.Add(0);
        }

        return result;
    }
}
