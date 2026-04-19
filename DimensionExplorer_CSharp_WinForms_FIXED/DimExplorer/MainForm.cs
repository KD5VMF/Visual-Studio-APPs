using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DimensionExplorer
{
    public sealed class MainForm : Form
    {
        private RenderPanel _canvas;
        private NumericUpDown _dimensionPicker;
        private TrackBar _speedBar;
        private TrackBar _scaleBar;
        private TrackBar _perspectiveBar;
        private CheckBox _autoRotateBox;
        private CheckBox _showVerticesBox;
        private CheckBox _showLabelsBox;
        private CheckBox _showAxesBox;
        private Label _infoLabel;
        private Label _statsLabel;
        private RichTextBox _descriptionBox;
        private Button _resetButton;
        private Button _randomSpinButton;
        private readonly System.Windows.Forms.Timer _timer;

        private HypercubeModel _model = new HypercubeModel(4);
        private double[,] _angles = new double[12, 12];
        private readonly Dictionary<Tuple<int, int>, double> _autoRates = new Dictionary<Tuple<int, int>, double>();
        private Point _lastMouse;
        private bool _dragging;
        private readonly Random _random = new Random();

        public MainForm()
        {
            Text = "Dimension Explorer - 1D to 12D";
            Width = 1600;
            Height = 980;
            MinimumSize = new Size(1200, 760);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(12, 16, 24);
            ForeColor = Color.Gainsboro;

            _canvas = new RenderPanel();
            _canvas.Dock = DockStyle.Fill;
            _canvas.BackColor = Color.FromArgb(5, 8, 16);
            _canvas.Margin = new Padding(0);
            _canvas.Paint += Canvas_Paint;
            _canvas.MouseDown += Canvas_MouseDown;
            _canvas.MouseMove += Canvas_MouseMove;
            _canvas.MouseUp += delegate { _dragging = false; };
            _canvas.MouseWheel += Canvas_MouseWheel;

            Control sidebar = BuildSidebar();

            Controls.Add(_canvas);
            Controls.Add(sidebar);

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 16;
            _timer.Tick += delegate { TickFrame(); };
            _timer.Start();

            InitializeAutoRates();
            UpdateModel(4);
        }

        private Control BuildSidebar()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Right;
            panel.Width = 380;
            panel.BackColor = Color.FromArgb(18, 24, 36);
            panel.Padding = new Padding(16);

            Label title = new Label();
            title.Text = "Interactive Dimension Explorer";
            title.Dock = DockStyle.Top;
            title.Height = 40;
            title.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            title.ForeColor = Color.White;

            _infoLabel = new Label();
            _infoLabel.Dock = DockStyle.Top;
            _infoLabel.Height = 52;
            _infoLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            _infoLabel.ForeColor = Color.FromArgb(180, 200, 220);
            _infoLabel.Text = "";

            _statsLabel = new Label();
            _statsLabel.Dock = DockStyle.Top;
            _statsLabel.Height = 48;
            _statsLabel.Font = new Font("Consolas", 10, FontStyle.Bold);
            _statsLabel.ForeColor = Color.FromArgb(160, 230, 255);
            _statsLabel.Text = "";

            Label dimLabel = MakeLabel("Dimension");
            _dimensionPicker = new NumericUpDown();
            _dimensionPicker.Minimum = 1;
            _dimensionPicker.Maximum = 12;
            _dimensionPicker.Value = 4;
            _dimensionPicker.Width = 120;
            _dimensionPicker.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            _dimensionPicker.BackColor = Color.FromArgb(35, 45, 64);
            _dimensionPicker.ForeColor = Color.White;
            _dimensionPicker.BorderStyle = BorderStyle.FixedSingle;
            _dimensionPicker.ValueChanged += delegate { UpdateModel((int)_dimensionPicker.Value); };

            Label speedLabel = MakeLabel("Auto rotation speed");
            _speedBar = MakeTrackBar(0, 100, 32);

            Label scaleLabel = MakeLabel("Zoom");
            _scaleBar = MakeTrackBar(40, 400, 150);

            Label perspectiveLabel = MakeLabel("Higher-dimension perspective");
            _perspectiveBar = MakeTrackBar(15, 100, 40);

            _autoRotateBox = MakeCheckBox("Auto rotate", true);
            _showVerticesBox = MakeCheckBox("Show vertices", true);
            _showLabelsBox = MakeCheckBox("Show vertex labels", false);
            _showAxesBox = MakeCheckBox("Show XYZ axes", true);

            _resetButton = MakeButton("Reset view", delegate { ResetAngles(); });
            _randomSpinButton = MakeButton("Randomize spin", delegate { RandomizeAngles(); });

            _descriptionBox = new RichTextBox();
            _descriptionBox.Dock = DockStyle.Fill;
            _descriptionBox.ReadOnly = true;
            _descriptionBox.BorderStyle = BorderStyle.None;
            _descriptionBox.BackColor = Color.FromArgb(14, 18, 28);
            _descriptionBox.ForeColor = Color.Gainsboro;
            _descriptionBox.Font = new Font("Segoe UI", 10);
            _descriptionBox.DetectUrls = false;
            _descriptionBox.ScrollBars = RichTextBoxScrollBars.Vertical;

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Top;
            flow.AutoSize = true;
            flow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.Padding = new Padding(0);
            flow.Margin = new Padding(0);

            flow.Controls.Add(title);
            flow.Controls.Add(_infoLabel);
            flow.Controls.Add(_statsLabel);
            flow.Controls.Add(dimLabel);
            flow.Controls.Add(_dimensionPicker);
            flow.Controls.Add(speedLabel);
            flow.Controls.Add(_speedBar);
            flow.Controls.Add(scaleLabel);
            flow.Controls.Add(_scaleBar);
            flow.Controls.Add(perspectiveLabel);
            flow.Controls.Add(_perspectiveBar);
            flow.Controls.Add(_autoRotateBox);
            flow.Controls.Add(_showVerticesBox);
            flow.Controls.Add(_showLabelsBox);
            flow.Controls.Add(_showAxesBox);
            flow.Controls.Add(_resetButton);
            flow.Controls.Add(_randomSpinButton);
            flow.Controls.Add(MakeLabel("What you are looking at"));

            panel.Controls.Add(_descriptionBox);
            panel.Controls.Add(flow);

            return panel;
        }

        private static Label MakeLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            label.ForeColor = Color.White;
            label.Margin = new Padding(0, 10, 0, 4);
            return label;
        }

        private static TrackBar MakeTrackBar(int min, int max, int value)
        {
            TrackBar bar = new TrackBar();
            bar.Minimum = min;
            bar.Maximum = max;
            bar.Value = value;
            bar.TickFrequency = Math.Max(1, (max - min) / 10);
            bar.Width = 320;
            bar.AutoSize = false;
            bar.Height = 38;
            bar.BackColor = Color.FromArgb(18, 24, 36);
            return bar;
        }

        private static CheckBox MakeCheckBox(string text, bool value)
        {
            CheckBox box = new CheckBox();
            box.Text = text;
            box.Checked = value;
            box.AutoSize = true;
            box.Font = new Font("Segoe UI", 10);
            box.ForeColor = Color.Gainsboro;
            box.Margin = new Padding(0, 8, 0, 0);
            return box;
        }

        private static Button MakeButton(string text, Action onClick)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = 160;
            button.Height = 38;
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Color.FromArgb(44, 76, 120);
            button.ForeColor = Color.White;
            button.Margin = new Padding(0, 10, 0, 0);
            button.FlatAppearance.BorderSize = 0;
            button.Click += delegate { onClick(); };
            return button;
        }

        private void InitializeAutoRates()
        {
            _autoRates.Clear();
            Tuple<int, int, double>[] pairs =
            {
                Tuple.Create(0,1, 0.020), Tuple.Create(0,2, 0.013), Tuple.Create(1,2, 0.017),
                Tuple.Create(0,3, 0.011), Tuple.Create(1,3, 0.014), Tuple.Create(2,3, 0.010),
                Tuple.Create(0,4, 0.007), Tuple.Create(1,4, 0.008), Tuple.Create(2,4, 0.009),
                Tuple.Create(3,4, 0.006), Tuple.Create(0,5, 0.005), Tuple.Create(2,5, 0.006),
                Tuple.Create(4,5, 0.004), Tuple.Create(1,6, 0.004), Tuple.Create(3,6, 0.004),
                Tuple.Create(5,6, 0.003), Tuple.Create(2,7, 0.003), Tuple.Create(6,7, 0.0025),
                Tuple.Create(7,8, 0.0022), Tuple.Create(8,9, 0.0018), Tuple.Create(9,10,0.0015), Tuple.Create(10,11,0.0012)
            };

            foreach (Tuple<int, int, double> pair in pairs)
            {
                _autoRates[Tuple.Create(pair.Item1, pair.Item2)] = pair.Item3;
            }
        }

        private void UpdateModel(int dimension)
        {
            _model = new HypercubeModel(dimension);
            ResetAngles();
            UpdateSidebarText();
            _canvas.Invalidate();
        }

        private void UpdateSidebarText()
        {
            _infoLabel.Text = string.Format("Dimensional object: {0}D hypercube\nDrag to spin. Mouse wheel zooms.", _model.Dimension);
            _statsLabel.Text = string.Format("Vertices: {0:N0}\nEdges:    {1:N0}", _model.Vertices.Length, _model.Edges.Length);
            _descriptionBox.Text = GetDescription(_model.Dimension);
        }

        private void ResetAngles()
        {
            _angles = new double[12, 12];
            for (int i = 0; i < Math.Min(_model.Dimension, 3); i++)
            {
                for (int j = i + 1; j < Math.Min(_model.Dimension, 4); j++)
                {
                    _angles[i, j] = 0.18 * (j - i);
                }
            }
            _canvas.Invalidate();
        }

        private void RandomizeAngles()
        {
            for (int i = 0; i < 12; i++)
            {
                for (int j = i + 1; j < 12; j++)
                {
                    _angles[i, j] = (_random.NextDouble() * 2.0 - 1.0) * Math.PI;
                }
            }
            _canvas.Invalidate();
        }

        private void TickFrame()
        {
            if (_autoRotateBox.Checked)
            {
                double speedScale = _speedBar.Value / 32.0;
                foreach (KeyValuePair<Tuple<int, int>, double> kv in _autoRates)
                {
                    if (kv.Key.Item1 < _model.Dimension && kv.Key.Item2 < _model.Dimension)
                    {
                        _angles[kv.Key.Item1, kv.Key.Item2] += kv.Value * speedScale;
                    }
                }
            }

            _canvas.Invalidate();
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            _dragging = true;
            _lastMouse = e.Location;
            _canvas.Focus();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging)
            {
                return;
            }

            int dx = e.X - _lastMouse.X;
            int dy = e.Y - _lastMouse.Y;
            _lastMouse = e.Location;

            if (_model.Dimension >= 2)
            {
                _angles[0, 1] += dx * 0.005;
            }
            if (_model.Dimension >= 3)
            {
                _angles[1, 2] += dy * 0.005;
            }
            if (_model.Dimension >= 4)
            {
                _angles[0, 3] += (dx + dy) * 0.0025;
            }

            _canvas.Invalidate();
        }

        private void Canvas_MouseWheel(object sender, MouseEventArgs e)
        {
            int next = Math.Max(_scaleBar.Minimum, Math.Min(_scaleBar.Maximum, _scaleBar.Value + Math.Sign(e.Delta) * 10));
            _scaleBar.Value = next;
            _canvas.Invalidate();
        }

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.Clear(_canvas.BackColor);

            DrawBackground(g, _canvas.ClientRectangle);

            ProjectedVertex[] projected = new ProjectedVertex[_model.Vertices.Length];
            for (int i = 0; i < _model.Vertices.Length; i++)
            {
                double[] p = (double[])_model.Vertices[i].Clone();
                ApplyRotations(p);
                projected[i] = HypercubeModel.ProjectToScreen(
                    p,
                    _canvas.ClientSize.Width,
                    _canvas.ClientSize.Height,
                    _scaleBar.Value,
                    _perspectiveBar.Value / 10f,
                    6.0f);
            }

            if (_showAxesBox.Checked)
            {
                DrawAxes(g);
            }

            DrawEdges(g, projected);

            if (_showVerticesBox.Checked)
            {
                DrawVertices(g, projected);
            }

            DrawOverlay(g);
        }

        private void ApplyRotations(double[] point)
        {
            for (int i = 0; i < _model.Dimension; i++)
            {
                for (int j = i + 1; j < _model.Dimension; j++)
                {
                    double angle = _angles[i, j];
                    if (Math.Abs(angle) > 0.000001)
                    {
                        HypercubeModel.RotateInPlace(point, i, j, angle);
                    }
                }
            }
        }

        private void DrawBackground(Graphics g, Rectangle rect)
        {
            using (LinearGradientBrush brush = new LinearGradientBrush(
                rect,
                Color.FromArgb(3, 8, 18),
                Color.FromArgb(13, 20, 34),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(brush, rect);
            }

            using (Pen gridPen = new Pen(Color.FromArgb(22, 40, 64), 1f))
            {
                for (int x = 0; x < rect.Width; x += 40)
                {
                    g.DrawLine(gridPen, x, 0, x, rect.Height);
                }
                for (int y = 0; y < rect.Height; y += 40)
                {
                    g.DrawLine(gridPen, 0, y, rect.Width, y);
                }
            }
        }

        private void DrawAxes(Graphics g)
        {
            PointF center = new PointF(_canvas.ClientSize.Width / 2f, _canvas.ClientSize.Height / 2f);
            using (Pen xPen = new Pen(Color.FromArgb(220, 100, 100), 2f))
            using (Pen yPen = new Pen(Color.FromArgb(100, 220, 120), 2f))
            using (Pen zPen = new Pen(Color.FromArgb(110, 170, 255), 2f))
            using (SolidBrush fontBrush = new SolidBrush(Color.FromArgb(170, 200, 240)))
            using (Font font = new Font("Segoe UI", 9, FontStyle.Bold))
            {
                g.DrawLine(xPen, center, new PointF(center.X + 80, center.Y));
                g.DrawLine(yPen, center, new PointF(center.X, center.Y - 80));
                g.DrawLine(zPen, center, new PointF(center.X - 55, center.Y + 55));

                g.DrawString("X", font, fontBrush, center.X + 86, center.Y - 10);
                g.DrawString("Y", font, fontBrush, center.X + 8, center.Y - 92);
                g.DrawString("Z", font, fontBrush, center.X - 68, center.Y + 48);
            }
        }

        private void DrawEdges(Graphics g, ProjectedVertex[] projected)
        {
            foreach (Tuple<int, int> edge in _model.Edges)
            {
                ProjectedVertex pa = projected[edge.Item1];
                ProjectedVertex pb = projected[edge.Item2];
                float depth = (pa.Z + pb.Z) * 0.5f;
                int alpha = (int)Math.Max(55f, Math.Min(220f, 120 + (depth + 2f) * 35f));
                int r = (int)Math.Max(90, Math.Min(230, 90 + (_model.Dimension * 10)));
                int gVal = (int)Math.Max(100, Math.Min(240, 160 + depth * 16));
                int bVal = (int)Math.Max(120, Math.Min(255, 255 - _model.Dimension * 6 + depth * 10));
                float width = Math.Max(1f, 1.5f + depth * 0.2f);

                using (Pen pen = new Pen(Color.FromArgb(alpha, r, gVal, bVal), width))
                {
                    g.DrawLine(pen, pa.Screen, pb.Screen);
                }
            }
        }

        private void DrawVertices(Graphics g, ProjectedVertex[] projected)
        {
            using (Font labelFont = new Font("Consolas", 8f, FontStyle.Bold))
            using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(200, 210, 230)))
            {
                for (int i = 0; i < projected.Length; i++)
                {
                    ProjectedVertex pv = projected[i];
                    float size = Math.Max(4f, Math.Min(12f, pv.Size * 0.6f));
                    RectangleF rect = new RectangleF(pv.Screen.X - size / 2f, pv.Screen.Y - size / 2f, size, size);
                    int alpha = (int)Math.Max(90f, Math.Min(255f, 140 + pv.Z * 30f));

                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, 240, 250, 255)))
                    using (Pen glow = new Pen(Color.FromArgb(alpha / 2, 100, 210, 255), 2f))
                    {
                        g.FillEllipse(brush, rect);
                        g.DrawEllipse(glow, rect);
                    }

                    if (_showLabelsBox.Checked && _model.Dimension <= 6)
                    {
                        g.DrawString(Convert.ToString(i, 2).PadLeft(_model.Dimension, '0'), labelFont, labelBrush, pv.Screen.X + 6, pv.Screen.Y - 6);
                    }
                }
            }
        }

        private void DrawOverlay(Graphics g)
        {
            string headline;
            switch (_model.Dimension)
            {
                case 1:
                    headline = "1D - Line segment";
                    break;
                case 2:
                    headline = "2D - Square";
                    break;
                case 3:
                    headline = "3D - Cube";
                    break;
                case 4:
                    headline = "4D - Tesseract";
                    break;
                default:
                    headline = _model.Dimension + "D - Hypercube projection";
                    break;
            }

            string sub = string.Format("Zoom {0}%   |   Perspective {1:0.0}   |   Speed {2}%", _scaleBar.Value, _perspectiveBar.Value / 10f, _speedBar.Value);

            using (Font titleFont = new Font("Segoe UI", 18, FontStyle.Bold))
            using (Font subFont = new Font("Segoe UI", 10, FontStyle.Regular))
            using (SolidBrush titleBrush = new SolidBrush(Color.White))
            using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(180, 210, 230)))
            using (SolidBrush panelBrush = new SolidBrush(Color.FromArgb(90, 10, 18, 30)))
            {
                UiExtensions.FillRoundedRectangle(g, panelBrush, new RectangleF(18, 18, 440, 78), 14);
                g.DrawString(headline, titleFont, titleBrush, 30, 28);
                g.DrawString(sub, subFont, subBrush, 32, 64);
            }
        }

        private static string GetDescription(int dimension)
        {
            switch (dimension)
            {
                case 1:
                    return "A 1D hypercube is a line segment: two end points connected by one edge.\n\nThis is the simplest case. It only needs one coordinate to describe a position on it.";
                case 2:
                    return "A 2D hypercube is a square.\n\nTake the 1D line and move it in a new perpendicular direction. You get four vertices and four edges.";
                case 3:
                    return "A 3D hypercube is the ordinary cube.\n\nTake the square and push it through a brand-new perpendicular direction. Now you have depth as well as width and height.";
                case 4:
                    return "A 4D hypercube is a tesseract.\n\nYou cannot see true 4D directly on a flat monitor, so this program rotates the object in 4D and projects it down to 3D, then to your 2D screen.";
                case 5:
                    return "A 5D hypercube extends the same idea again: take the tesseract and move it along a new perpendicular axis.\n\nThe shape now has 32 vertices and 80 edges. What you see is a projection of a projection.";
                case 6:
                    return "A 6D hypercube contains 64 vertices and 192 edges.\n\nAt this point, intuition starts to break down, but the math stays clean: every added dimension doubles the number of vertices.";
                case 7:
                    return "A 7D hypercube has 128 vertices.\n\nDragging the mouse changes several rotation planes so you can watch the projected structure breathe and fold through lower dimensions.";
                case 8:
                    return "An 8D hypercube has 256 vertices and 1,024 edges, because the edge count rule is n * 2^(n-1).\n\nThis is where dense lattice-like patterns emerge.";
                case 9:
                    return "A 9D hypercube has 512 vertices and 2,304 edges.\n\nThe app handles it by rotating in many planes and compressing the geometry step by step into something your screen can display.";
                case 10:
                    return "A 10D hypercube has 1,024 vertices and 5,120 edges.\n\nYou are not seeing a single obvious box anymore. You are seeing the shadow of a very high-dimensional object.";
                case 11:
                    return "An 11D hypercube has 2,048 vertices and 11,264 edges.\n\nThe projection becomes richly tangled, but every line still comes from the exact same hypercube rule: connect vertices that differ by one coordinate sign.";
                case 12:
                    return "A 12D hypercube has 4,096 vertices and 24,576 edges.\n\nThat is a lot of structure for a desktop app, but your workstation should still be able to explore it interactively.";
                default:
                    return "";
            }
        }
    }

    internal sealed class RenderPanel : Panel
    {
        public RenderPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            TabStop = true;
        }
    }

    internal static class UiExtensions
    {
        public static void FillRoundedRectangle(Graphics g, Brush brush, RectangleF rect, float radius)
        {
            using (GraphicsPath path = RoundedRect(rect, radius))
            {
                g.FillPath(brush, path);
            }
        }

        private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            float d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
