using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace NewtonsCradleStudio;

public sealed class CradleControl : FrameworkElement
{
    private readonly CradleSimulation _simulation = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private double _lastTime;
    private Point _lastPointer;
    private Point _currentPointer;
    private double _lastPointerTime;
    private bool _pointerPrimed;

    public bool IsPaused => _simulation.IsPaused;

    public CradleControl()
    {
        Focusable = true;
        SnapsToDevicePixels = true;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => _simulation.Resize(ActualWidth, ActualHeight);

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        LostMouseCapture += OnLostMouseCapture;
        MouseLeave += OnMouseLeave;

        _simulation.SetCount(5);
    }

    public void Reset()
    {
        _simulation.Reset();
    }

    public void TogglePause()
    {
        _simulation.IsPaused = !_simulation.IsPaused;
    }

    public void SetIdealTransfer(bool enabled) => _simulation.SetIdealTransfer(enabled);
    public void SetLoss(double loss) => _simulation.SetLoss(loss);
    public void SetCount(int count) => _simulation.SetCount(count);
    public void SetTimeScale(double value) => _simulation.TimeScale = value;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var width = RenderSize.Width;
        var height = RenderSize.Height;
        if (width <= 1 || height <= 1)
        {
            return;
        }

        DrawBackground(dc, width, height);
        DrawSupport(dc, width);
        DrawShadows(dc, height);
        DrawStrings(dc);
        DrawBobs(dc);
        DrawOverlay(dc, width, height);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering += OnRendering;
        _simulation.Resize(ActualWidth, ActualHeight);
        _lastTime = _clock.Elapsed.TotalSeconds;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = now - _lastTime;
        _lastTime = now;
        _simulation.Step(dt);
        InvalidateVisual();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        var point = e.GetPosition(this);
        var hit = _simulation.HitTest(point);
        if (hit < 0)
        {
            return;
        }

        CaptureMouse();
        e.Handled = true;

        var now = _clock.Elapsed.TotalSeconds;
        _pointerPrimed = true;
        _lastPointerTime = now;
        _lastPointer = point;
        _currentPointer = point;
        _simulation.BeginDrag(hit, point);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        _currentPointer = e.GetPosition(this);

        if (_simulation.DraggingIndex is not null)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                ReleaseDrag(_currentPointer);
                Cursor = _simulation.HitTest(_currentPointer) >= 0 ? Cursors.Hand : Cursors.Arrow;
                return;
            }

            if (!IsMouseCaptured)
            {
                CaptureMouse();
            }

            var now = _clock.Elapsed.TotalSeconds;
            if (!_pointerPrimed)
            {
                _pointerPrimed = true;
                _lastPointerTime = now;
                _lastPointer = _currentPointer;
            }

            _simulation.UpdateDrag(_currentPointer);
            _lastPointer = _currentPointer;
            _lastPointerTime = now;
            Cursor = Cursors.Hand;
            return;
        }

        Cursor = _simulation.HitTest(_currentPointer) >= 0 ? Cursors.Hand : Cursors.Arrow;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseDrag(e.GetPosition(this));
        e.Handled = true;
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_simulation.DraggingIndex is not null)
        {
            ReleaseDrag(_currentPointer);
        }
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_simulation.DraggingIndex is not null && e.LeftButton != MouseButtonState.Pressed)
        {
            ReleaseDrag(e.GetPosition(this));
        }
    }

    private void ReleaseDrag(Point point)
    {
        if (_simulation.DraggingIndex is null)
        {
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }

            _pointerPrimed = false;
            return;
        }

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Max(0.001, now - _lastPointerTime);
        var velocity = (Vector)(point - _lastPointer) / dt;
        velocity *= 0.22;

        _simulation.UpdateDrag(point);
        _simulation.EndDrag(velocity);
        _pointerPrimed = false;

        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void DrawBackground(DrawingContext dc, double width, double height)
    {
        var background = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromRgb(7, 12, 19), 0.0),
                new(Color.FromRgb(12, 21, 33), 0.45),
                new(Color.FromRgb(22, 30, 47), 1.0),
            },
            new Point(0, 0),
            new Point(0, 1));
        dc.DrawRectangle(background, null, new Rect(0, 0, width, height));

        var glow1 = new RadialGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromArgb(90, 88, 178, 255), 0.0),
                new(Color.FromArgb(0, 88, 178, 255), 1.0),
            })
        {
            RadiusX = 0.35,
            RadiusY = 0.35,
            Center = new Point(0.22, 0.18),
            GradientOrigin = new Point(0.22, 0.18),
        };
        dc.DrawRectangle(glow1, null, new Rect(0, 0, width, height));

        var glow2 = new RadialGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromArgb(80, 86, 255, 208), 0.0),
                new(Color.FromArgb(0, 86, 255, 208), 1.0),
            })
        {
            RadiusX = 0.28,
            RadiusY = 0.28,
            Center = new Point(0.80, 0.25),
            GradientOrigin = new Point(0.80, 0.25),
        };
        dc.DrawRectangle(glow2, null, new Rect(0, 0, width, height));

        var floor = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromArgb(10, 255, 255, 255), 0.0),
                new(Color.FromArgb(18, 255, 255, 255), 0.5),
                new(Color.FromArgb(40, 255, 255, 255), 1.0),
            },
            new Point(0, 0),
            new Point(0, 1));
        dc.DrawRoundedRectangle(floor, null, new Rect(32, height - 160, width - 64, 120), 30, 30);
    }

    private void DrawSupport(DrawingContext dc, double width)
    {
        var y = _simulation.SupportTopY;
        var beamRect = new Rect(width * 0.20, y, width * 0.60, 18);
        var beamBrush = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromRgb(210, 220, 233), 0.0),
                new(Color.FromRgb(138, 154, 176), 0.45),
                new(Color.FromRgb(227, 236, 245), 1.0),
            },
            new Point(0, 0),
            new Point(1, 1));
        dc.DrawRoundedRectangle(beamBrush, new Pen(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), 1.2), beamRect, 9, 9);

        var legPen = new Pen(new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromRgb(190, 198, 214), 0.0),
                new(Color.FromRgb(86, 97, 116), 1.0),
            },
            new Point(0, 0),
            new Point(0, 1)), 9)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };

        dc.DrawLine(legPen, new Point(width * 0.24, y + 12), new Point(width * 0.16, RenderSize.Height - 130));
        dc.DrawLine(legPen, new Point(width * 0.76, y + 12), new Point(width * 0.84, RenderSize.Height - 130));
    }

    private void DrawShadows(DrawingContext dc, double height)
    {
        foreach (var bob in _simulation.Bobs)
        {
            var p = _simulation.GetPosition(bob);
            var shadow = new RadialGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(90, 0, 0, 0), 0.0),
                    new(Color.FromArgb(0, 0, 0, 0), 1.0),
                })
            {
                RadiusX = 0.5,
                RadiusY = 0.5,
            };

            var shadowRect = new Rect(
                p.X - _simulation.Radius * 1.25,
                height - 128,
                _simulation.Radius * 2.5,
                _simulation.Radius * 0.8);
            dc.DrawEllipse(shadow, null, new Point(shadowRect.X + shadowRect.Width / 2, shadowRect.Y + shadowRect.Height / 2), shadowRect.Width / 2, shadowRect.Height / 2);
        }
    }

    private void DrawStrings(DrawingContext dc)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(225, 232, 240, 248)), 2.3)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };

        foreach (var bob in _simulation.Bobs)
        {
            var anchor = new Point(bob.AnchorX, bob.AnchorY);
            var pos = _simulation.GetPosition(bob);
            dc.DrawLine(pen, anchor, pos);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(225, 233, 242)), null, anchor, 4.6, 4.6);
        }
    }

    private void DrawBobs(DrawingContext dc)
    {
        for (var i = 0; i < _simulation.Bobs.Count; i++)
        {
            var bob = _simulation.Bobs[i];
            var pos = _simulation.GetPosition(bob);
            var radius = _simulation.Radius;

            var bodyBrush = new RadialGradientBrush
            {
                RadiusX = 0.75,
                RadiusY = 0.75,
                Center = new Point(0.35, 0.30),
                GradientOrigin = new Point(0.32, 0.26),
                GradientStops = new GradientStopCollection
                {
                    new(Color.FromRgb(255, 255, 255), 0.0),
                    new(Color.FromRgb(193, 204, 220), 0.18),
                    new(Color.FromRgb(123, 136, 156), 0.52),
                    new(Color.FromRgb(70, 80, 98), 0.85),
                    new(Color.FromRgb(32, 38, 48), 1.0),
                },
            };

            var rimPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), 1.1);
            dc.DrawEllipse(bodyBrush, rimPen, pos, radius, radius);

            var highlight = new RadialGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(170, 255, 255, 255), 0.0),
                    new(Color.FromArgb(0, 255, 255, 255), 1.0),
                })
            {
                RadiusX = 0.9,
                RadiusY = 0.9,
                Center = new Point(0.32, 0.28),
                GradientOrigin = new Point(0.32, 0.28),
            };
            dc.DrawEllipse(highlight, null, new Point(pos.X - radius * 0.18, pos.Y - radius * 0.18), radius * 0.55, radius * 0.55);

            if (_simulation.DraggingIndex == i)
            {
                var glow = new RadialGradientBrush(
                    new GradientStopCollection
                    {
                        new(Color.FromArgb(70, 110, 210, 255), 0.0),
                        new(Color.FromArgb(0, 110, 210, 255), 1.0),
                    });
                dc.DrawEllipse(glow, null, pos, radius * 1.55, radius * 1.55);
            }
        }
    }

    private void DrawOverlay(DrawingContext dc, double width, double height)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var text = new FormattedText(
            $"Balls: {_simulation.Count}   Time: {_simulation.TimeScale:0.00}x   Mode: {(_simulation.IdealTransfer ? "Ideal" : "Loss")}",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            15,
            new SolidColorBrush(Color.FromArgb(210, 232, 241, 250)),
            dpi);
        dc.DrawText(text, new Point(20, height - 34));

        if (_simulation.DraggingIndex is int dragIndex)
        {
            var dragPoint = _simulation.GetPosition(dragIndex);
            var dragPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 120, 200, 255)), 2.0)
            {
                DashStyle = DashStyles.Dash,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            };
            dc.DrawLine(dragPen, dragPoint, _currentPointer);
        }

        var glass = new Pen(new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)), 1.0);
        dc.DrawRoundedRectangle(null, glass, new Rect(8, 8, width - 16, height - 16), 22, 22);
    }
}
