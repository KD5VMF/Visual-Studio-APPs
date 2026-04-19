using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ImagingPixelFormat = System.Drawing.Imaging.PixelFormat;
using System.Drawing.Text;

namespace LifeForgeAccelerated;

internal enum OverlayAction
{
    None,
    TogglePause,
    ResetWorld,
    ToggleFullscreen,
    ToggleHelp,
    SpeedDown,
    SpeedUp,
    WorkerDown,
    WorkerUp
}

internal sealed class OverlayComposer
{
    private readonly Dictionary<OverlayAction, RectangleF> _buttonRects = new();

    private readonly struct ButtonInfo
    {
        public ButtonInfo(OverlayAction action, string text)
        {
            Action = action;
            Text = text;
        }

        public OverlayAction Action { get; }
        public string Text { get; }
    }

    public Bitmap BuildTerrain(int width, int height, World world)
    {
        var bitmap = new Bitmap(Math.Max(1, width), Math.Max(1, height), ImagingPixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        using var bg = new LinearGradientBrush(new Rectangle(0, 0, width, height), Color.FromArgb(28, 36, 52), Color.FromArgb(16, 22, 32), 90f);
        g.FillRectangle(bg, 0, 0, width, height);

        var rng = new Random(10);
        for (var y = 0; y < height; y += 12)
        {
            var hue = 36 + (int)(world.TerrainHeightAt(new Vec2(0, y)) * 18f);
            using var band = new SolidBrush(Color.FromArgb(20, 18 + hue, 34 + hue, 22 + hue / 2));
            g.FillRectangle(band, 0, y, width, 12);
        }

        for (var i = 0; i < (width * height) / 1900; i++)
        {
            var x = rng.Next(width);
            var y = rng.Next(height);
            var size = rng.Next(1, 4);
            var alpha = rng.Next(10, 24);
            using var dot = new SolidBrush(Color.FromArgb(alpha, 170, 210, 170));
            g.FillEllipse(dot, x, y, size, size);
        }

        using var vignette = new GraphicsPath();
        vignette.AddEllipse(-width * 0.12f, -height * 0.10f, width * 1.24f, height * 1.20f);
        using var pgb = new PathGradientBrush(vignette)
        {
            CenterColor = Color.FromArgb(0, 0, 0, 0),
            SurroundColors = new[] { Color.FromArgb(135, 0, 0, 0) }
        };
        g.FillRectangle(pgb, 0, 0, width, height);

        return bitmap;
    }

    public Bitmap Compose(int width, int height, World world, bool paused, bool showHelp, float simSpeed, float fps, bool fullscreen)
    {
        _buttonRects.Clear();
        var bitmap = new Bitmap(Math.Max(1, width), Math.Max(1, height), ImagingPixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);
        _buttonRects.Clear();

        using var titleFont = new Font("Segoe UI", 13.5f, FontStyle.Bold);
        using var bodyFont = new Font("Consolas", 10.25f, FontStyle.Regular);
        using var smallFont = new Font("Segoe UI", 8.9f, FontStyle.Regular);
        using var white = new SolidBrush(Color.WhiteSmoke);
        using var soft = new SolidBrush(Color.FromArgb(224, 232, 240));
        using var faint = new SolidBrush(Color.FromArgb(180, 200, 214));

        var hudWidth = MathF.Min(392f, MathF.Max(300f, width * 0.26f));
        var worldRight = width - hudWidth - 24f;
        var leftPanelWidth = MathF.Max(360f, worldRight - 14f);
        var titleWidth = MathF.Min(leftPanelWidth, 980f);

        var top = new RectangleF(14, 14, titleWidth, 84f);
        DrawPanel(g, top, 18, Color.FromArgb(175, 8, 12, 20), Color.FromArgb(88, 255, 255, 255));
        g.DrawString("LifeForge Accelerated", titleFont, white, top.X + 16, top.Y + 10);
        g.DrawString("GPU rendering + multithreaded ecosystem simulation", smallFont, faint, top.X + 18, top.Y + 36);
        g.DrawString("Neural agents ON   Learning ON   Traits + partial learned bias inherited", smallFont, soft, top.X + 18, top.Y + 57);

        var controls = new[]
        {
            new ButtonInfo(OverlayAction.TogglePause, paused ? "Resume" : "Pause"),
            new ButtonInfo(OverlayAction.ResetWorld, "Reset"),
            new ButtonInfo(OverlayAction.ToggleFullscreen, fullscreen ? "Window" : "Fullscreen"),
            new ButtonInfo(OverlayAction.ToggleHelp, showHelp ? "Hide Help" : "Help"),
            new ButtonInfo(OverlayAction.SpeedDown, "Speed -"),
            new ButtonInfo(OverlayAction.SpeedUp, "Speed +"),
            new ButtonInfo(OverlayAction.WorkerDown, "Threads -"),
            new ButtonInfo(OverlayAction.WorkerUp, "Threads +")
        };

        var controlInnerWidth = MathF.Min(leftPanelWidth - 28f, 980f - 24f);
        if (controlInnerWidth < 280f) controlInnerWidth = MathF.Max(280f, leftPanelWidth - 28f);
        var buttonLayout = MeasureButtonFlow(g, controls, smallFont, controlInnerWidth, 36f, 8f);

        var metaLines = BuildMetaLines(g, bodyFont, controlInnerWidth,
            $"Speed {simSpeed:F2}x   Threads {world.WorkerThreads} / {Environment.ProcessorCount}   FPS {fps:F1}",
            $"Prey NNs {world.Preys.Count}   Predator NNs {world.Predators.Count}   Heredity active");

        var metaHeight = metaLines.Length * 20f;
        var metaWidth = 0f;
        foreach (var line in metaLines)
        {
            metaWidth = MathF.Max(metaWidth, g.MeasureString(line, bodyFont).Width);
        }

        var controlHeight = 18f + metaHeight + 8f + buttonLayout.Height + 10f;
        var desiredControlWidth = MathF.Max(360f, MathF.Max(metaWidth, buttonLayout.Width) + 28f);
        var controlRect = new RectangleF(14, MathF.Max(108f, height - controlHeight - 14f), MathF.Min(MathF.Min(leftPanelWidth, 980f), desiredControlWidth), controlHeight);
        DrawPanel(g, controlRect, 18, Color.FromArgb(175, 8, 12, 20), Color.FromArgb(88, 255, 255, 255));

        var innerX = controlRect.X + 14f;
        var controlInnerDrawWidth = controlRect.Width - 28f;
        var textY = controlRect.Y + 12f;
        foreach (var line in metaLines)
        {
            g.DrawString(line, bodyFont, soft, innerX, textY);
            textY += 19f;
        }

        DrawButtonFlow(g, controls, smallFont, innerX, textY + 8f, controlInnerDrawWidth, 36f, 8f);

        var hud = new RectangleF(width - hudWidth - 14, 14, hudWidth, height - 28);
        DrawPanel(g, hud, 18, Color.FromArgb(165, 8, 12, 20), Color.FromArgb(88, 255, 255, 255));

        var x = hud.X + 16;
        var y = hud.Y + 14;
        g.DrawString("World", titleFont, white, x, y);
        y += 34;

        DrawStatRow(g, bodyFont, soft, x, y + 0, 116, "Time", FormatTime(world.SimulatedTime));
        DrawStatRow(g, bodyFont, soft, x, y + 19, 116, "Steps", world.StepCounter.ToString("N0"));
        DrawStatRow(g, bodyFont, soft, x, y + 38, 116, "Workers", world.WorkerThreads.ToString());
        DrawStatRow(g, bodyFont, soft, x, y + 57, 116, "Prey", world.Preys.Count.ToString());
        DrawStatRow(g, bodyFont, soft, x, y + 76, 116, "Predators", world.Predators.Count.ToString());
        DrawStatRow(g, bodyFont, soft, x, y + 95, 116, "Plants", world.Plants.Count.ToString());
        DrawStatRow(g, bodyFont, soft, x, y + 114, 116, "Food", world.Foods.Count.ToString());
        DrawStatRow(g, bodyFont, soft, x, y + 133, 116, "Signals", world.Pulses.Count.ToString());
        y += 162;

        var culture = new RectangleF(x, y, hud.Width - 32, 126);
        DrawPanel(g, culture, 14, Color.FromArgb(105, 14, 22, 30), Color.FromArgb(70, 255, 255, 255));
        g.DrawString("Shared learning", bodyFont, white, culture.X + 10, culture.Y + 8);
        DrawStatRow(g, bodyFont, soft, culture.X + 10, culture.Y + 34, 74, "Prey food", world.PreyCulture.FoodStrength.ToString("F2"));
        DrawStatRow(g, bodyFont, soft, culture.X + culture.Width / 2, culture.Y + 34, 74, "Danger", world.PreyCulture.DangerStrength.ToString("F2"));
        DrawStatRow(g, bodyFont, soft, culture.X + 10, culture.Y + 56, 74, "Water", world.PreyCulture.WaterStrength.ToString("F2"));
        DrawStatRow(g, bodyFont, soft, culture.X + culture.Width / 2, culture.Y + 56, 74, "Coop", world.PreyCulture.Cooperation.ToString("F2"));
        DrawStatRow(g, bodyFont, soft, culture.X + 10, culture.Y + 82, 74, "Pred hunt", world.PredatorCulture.HuntStrength.ToString("F2"));
        DrawStatRow(g, bodyFont, soft, culture.X + culture.Width / 2, culture.Y + 82, 74, "Water", world.PredatorCulture.WaterStrength.ToString("F2"));
        DrawStatRow(g, bodyFont, soft, culture.X + 10, culture.Y + 104, 74, "Aggro", world.PredatorCulture.Aggression.ToString("F2"));
        DrawStatRow(g, bodyFont, soft, culture.X + culture.Width / 2, culture.Y + 104, 74, "Casts", world.PredatorCulture.Broadcasts.ToString("N0"));
        y += 140;

        var chart = new RectangleF(x, y, hud.Width - 32, 146);
        DrawPanel(g, chart, 14, Color.FromArgb(105, 14, 22, 30), Color.FromArgb(70, 255, 255, 255));
        DrawHistoryChart(g, chart, world, smallFont, faint);
        y += 158;

        g.DrawString("Selected", titleFont, white, x, y);
        y += 30;
        var selected = new RectangleF(x, y, hud.Width - 32, hud.Bottom - y - 16);
        using var wrap = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
        DrawSelection(g, world, selected, bodyFont, smallFont, soft, wrap);

        if (showHelp)
        {
            var help = new RectangleF(MathF.Max(18f, width * 0.12f), MathF.Max(110f, height * 0.10f), MathF.Min(width * 0.76f, 980f), MathF.Min(height * 0.74f, 640f));
            DrawPanel(g, help, 22, Color.FromArgb(235, 8, 12, 20), Color.FromArgb(120, 255, 255, 255));
            g.DrawString("Controls, neural learning, and inheritance", titleFont, white, help.X + 18, help.Y + 18);
            var helpText = string.Join(Environment.NewLine,
                "• Fullscreen button + F11 toggles fullscreen. Esc leaves fullscreen.",
                "• Space pauses. R resets. H shows or hides this panel.",
                "• Speed - / Speed + change simulation speed. Threads - / Threads + change worker count.",
                "• Every creature still has its own neural-network brain.",
                "• Each brain learns during life through reinforcement of its outputs.",
                "• Offspring inherit mutated physical and behavioral traits.",
                "• Offspring also inherit a partial carryover of learned output bias and remembered directions.",
                "• Species culture memory keeps shared food, danger, water, and hunt knowledge alive across the population.",
                "• This single window uses one GPU for rendering; the life logic still uses your CPU threads.");
            var textRect = new RectangleF(help.X + 20, help.Y + 56, help.Width - 40, help.Height - 76);
            g.DrawString(helpText, bodyFont, soft, textRect, wrap);
        }

        return bitmap;
    }

    public bool TryHandleClick(PointF point, out OverlayAction action)
    {
        foreach (var pair in _buttonRects)
        {
            if (pair.Value.Contains(point))
            {
                action = pair.Key;
                return true;
            }
        }

        action = OverlayAction.None;
        return false;
    }

    private void DrawButtonFlow(Graphics g, IReadOnlyList<ButtonInfo> buttons, Font font, float startX, float startY, float maxWidth, float height, float gap)
    {
        var x = startX;
        var y = startY;
        foreach (var button in buttons)
        {
            var width = MeasureButtonWidth(g, font, button.Text);
            if ((x - startX) + width > maxWidth && x > startX)
            {
                x = startX;
                y += height + gap;
            }

            var rect = new RectangleF(x, y, width, height);
            _buttonRects[button.Action] = rect;
            DrawPanel(g, rect, 12, Color.FromArgb(190, 26, 38, 54), Color.FromArgb(96, 255, 255, 255));
            using var brush = new SolidBrush(Color.WhiteSmoke);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(button.Text, font, brush, rect, format);
            x += width + gap;
        }
    }

    private static (float Width, float Height) MeasureButtonFlow(Graphics g, IReadOnlyList<ButtonInfo> buttons, Font font, float maxWidth, float height, float gap)
    {
        var rowWidth = 0f;
        var maxRowWidth = 0f;
        var rows = 1;

        foreach (var button in buttons)
        {
            var width = MeasureButtonWidth(g, font, button.Text);
            var proposed = rowWidth <= 0f ? width : rowWidth + gap + width;
            if (proposed > maxWidth && rowWidth > 0f)
            {
                maxRowWidth = MathF.Max(maxRowWidth, rowWidth);
                rows++;
                rowWidth = width;
            }
            else
            {
                rowWidth = proposed;
            }
        }

        maxRowWidth = MathF.Max(maxRowWidth, rowWidth);
        return (maxRowWidth, rows * height + ((rows - 1) * gap));
    }

    private static float MeasureButtonWidth(Graphics g, Font font, string text)
    {
        var size = g.MeasureString(text, font);
        return MathF.Max(54f, MathF.Ceiling(size.Width) + 26f);
    }

    private static string[] BuildMetaLines(Graphics g, Font font, float maxWidth, string firstLine, string secondLine)
    {
        var firstFits = g.MeasureString(firstLine, font).Width <= maxWidth;
        var secondFits = g.MeasureString(secondLine, font).Width <= maxWidth;
        if (firstFits && secondFits)
        {
            return new[] { firstLine, secondLine };
        }

        return new[]
        {
            $"Speed {firstLine.Split("   ")[0].Replace("Speed ", string.Empty)}   Threads {firstLine.Split("Threads ")[1].Split("   ")[0]}",
            $"FPS {firstLine.Split("FPS ")[1]}",
            "Neural nets ON   Learning ON   Heredity ON"
        };
    }

    private static void DrawPanel(Graphics g, RectangleF rect, float radius, Color fillColor, Color outlineColor)
    {
        using var path = RoundedRect(rect, radius);
        using var fill = new SolidBrush(fillColor);
        using var outline = new Pen(outlineColor, 1.0f);
        g.FillPath(fill, path);
        g.DrawPath(outline, path);
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var diameter = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void DrawStatRow(Graphics g, Font font, Brush brush, float x, float y, float labelWidth, string label, string value)
    {
        g.DrawString(label, font, brush, x, y);
        g.DrawString(value, font, brush, x + labelWidth, y);
    }

    private static string FormatTime(float seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }

    private static void DrawHistoryChart(Graphics g, RectangleF rect, World world, Font font, Brush brush)
    {
        g.DrawString("Population history", font, brush, rect.X + 10, rect.Y + 8);
        var plot = RectangleF.Inflate(rect, -12, -28);
        plot.Y += 10;
        plot.Height -= 10;
        if (world.History.Count < 2)
        {
            g.DrawString("Collecting samples...", font, brush, plot.X + 10, plot.Y + 20);
            return;
        }

        var maxValue = 1;
        foreach (var item in world.History)
        {
            maxValue = Math.Max(maxValue, Math.Max(Math.Max(item.Prey, item.Predator), Math.Max(item.Plants, item.Food)));
        }

        using var preyPen = new Pen(Color.FromArgb(96, 214, 255), 2f);
        using var predPen = new Pen(Color.FromArgb(255, 128, 102), 2f);
        using var plantPen = new Pen(Color.FromArgb(90, 205, 96), 1.6f);
        using var foodPen = new Pen(Color.FromArgb(220, 214, 110), 1.4f);
        using var grid = new Pen(Color.FromArgb(28, 255, 255, 255), 1f);

        for (var i = 0; i <= 4; i++)
        {
            var gy = plot.Top + ((plot.Height / 4f) * i);
            g.DrawLine(grid, plot.Left, gy, plot.Right, gy);
        }

        DrawSeries(g, plot, world.History, maxValue, item => item.Prey, preyPen);
        DrawSeries(g, plot, world.History, maxValue, item => item.Predator, predPen);
        DrawSeries(g, plot, world.History, maxValue, item => item.Plants, plantPen);
        DrawSeries(g, plot, world.History, maxValue, item => item.Food, foodPen);
    }

    private static void DrawSeries(Graphics g, RectangleF plot, List<(float Time, int Prey, int Predator, int Plants, int Food)> history, int maxValue, Func<(float Time, int Prey, int Predator, int Plants, int Food), int> selector, Pen pen)
    {
        if (history.Count < 2) return;
        var points = new PointF[history.Count];
        for (var i = 0; i < history.Count; i++)
        {
            var x = plot.Left + (plot.Width * (i / (float)(history.Count - 1)));
            var y = plot.Bottom - (plot.Height * (selector(history[i]) / (float)maxValue));
            points[i] = new PointF(x, y);
        }
        g.DrawLines(pen, points);
    }

    private static void DrawSelection(Graphics g, World world, RectangleF rect, Font bodyFont, Font smallFont, Brush brush, StringFormat wrap)
    {
        switch (world.Selection.Kind)
        {
            case SelectionKind.Agent when world.Selection.Agent is not null:
                var lines = string.Join(Environment.NewLine, world.Selection.Agent.DescribeSelection());
                g.DrawString(lines, bodyFont, brush, rect, wrap);
                break;
            case SelectionKind.Plant:
                g.DrawString("Plant\nDrops food and slowly spreads where ground and water are favorable.", bodyFont, brush, rect, wrap);
                break;
            case SelectionKind.Food when world.Selection.Food is not null:
                g.DrawString(world.Selection.Food.IsCarrion
                    ? "Carrion\nTemporary meat source left after a death. Predators and desperate prey can use it."
                    : "Food pellet\nFresh nutrition grown by plants. It restores energy and a little water.", bodyFont, brush, rect, wrap);
                break;
            case SelectionKind.Water:
                g.DrawString("Water pool\nCreatures drink here and often cluster around it.", bodyFont, brush, rect, wrap);
                break;
            default:
                g.DrawString("Click a creature or world object to inspect it.", smallFont, brush, rect, wrap);
                break;
        }
    }
}
