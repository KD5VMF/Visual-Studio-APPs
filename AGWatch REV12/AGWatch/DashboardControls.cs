using System.Drawing.Drawing2D;
using System.Reflection;

namespace AdGuardLiveWatch;

internal static class Theme
{
    public static readonly Color Back = Color.FromArgb(8, 12, 20);
    public static readonly Color Card = Color.FromArgb(14, 21, 34);
    public static readonly Color Card2 = Color.FromArgb(18, 28, 46);
    public static readonly Color Border = Color.FromArgb(47, 62, 88);
    public static readonly Color Text = Color.FromArgb(235, 243, 255);
    public static readonly Color Muted = Color.FromArgb(154, 172, 201);
    public static readonly Color Blue = Color.FromArgb(66, 184, 255);
    public static readonly Color Green = Color.FromArgb(76, 230, 139);
    public static readonly Color Yellow = Color.FromArgb(255, 220, 60);
    public static readonly Color Orange = Color.FromArgb(255, 158, 73);
    public static readonly Color Red = Color.FromArgb(255, 111, 120);
    public static readonly Color Purple = Color.FromArgb(194, 132, 255);

    public static Font SmallBold => new("Segoe UI", 9, FontStyle.Bold);
    public static Font Tiny => new("Segoe UI", 8, FontStyle.Regular);
    public static Font Normal => new("Segoe UI", 9, FontStyle.Regular);
    public static Font Big => new("Segoe UI", 18, FontStyle.Bold);
}

public static class WinFormsTuning
{
    public static void EnableDoubleBuffering(Control c)
    {
        try
        {
            var prop = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            prop?.SetValue(c, true, null);
        }
        catch { }

        foreach (Control child in c.Controls)
            EnableDoubleBuffering(child);
    }
}

public sealed class StatCard : UserControl
{
    private readonly Label _title = new();
    private readonly Label _value = new();
    private readonly Label _sub = new();
    private readonly Panel _accent = new();

    public string Title { get => _title.Text; set => _title.Text = value; }
    public string ValueText { get => _value.Text; set => _value.Text = value; }
    public string SubText { get => _sub.Text; set => _sub.Text = value; }

    public Color AccentColor
    {
        get => _accent.BackColor;
        set
        {
            _accent.BackColor = value;
            _value.ForeColor = value;
            Invalidate();
        }
    }

    public StatCard()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        Margin = new Padding(5);
        BackColor = Theme.Card;

        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0)
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 9));
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _accent.Dock = DockStyle.Fill;
        _accent.Margin = new Padding(0, 8, 0, 8);
        outer.Controls.Add(_accent, 0, 0);

        var inner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(10, 8, 8, 7),
            BackColor = Color.Transparent
        };
        inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 19));
        inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 21));

        _title.Dock = DockStyle.Fill;
        _title.ForeColor = Theme.Muted;
        _title.Font = Theme.SmallBold;
        _title.TextAlign = ContentAlignment.MiddleLeft;
        _title.AutoEllipsis = true;

        _value.Dock = DockStyle.Fill;
        _value.ForeColor = Theme.Blue;
        _value.Font = Theme.Big;
        _value.TextAlign = ContentAlignment.MiddleLeft;
        _value.AutoEllipsis = true;

        _sub.Dock = DockStyle.Fill;
        _sub.ForeColor = Theme.Text;
        _sub.Font = Theme.Tiny;
        _sub.TextAlign = ContentAlignment.MiddleLeft;
        _sub.AutoEllipsis = true;

        inner.Controls.Add(_title, 0, 0);
        inner.Controls.Add(_value, 0, 1);
        inner.Controls.Add(_sub, 0, 2);

        outer.Controls.Add(inner, 1, 0);
        Controls.Add(outer);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}

public sealed class SmoothBarGraph : UserControl
{
    private List<double> _values = new();
    public string Title { get; set; } = "GRAPH";
    public string LeftText { get; set; } = "";
    public string RightText { get; set; } = "";
    public Color AccentColor { get; set; } = Theme.Blue;
    public double FixedMax { get; set; } = 0;

    // REV12: lets low block-rate numbers breathe without lying about them.
    // The graph automatically widens or tightens around recent values.
    public bool UseDynamicRange { get; set; } = false;
    public double DynamicMinRange { get; set; } = 5.0;
    public double DynamicPaddingFraction { get; set; } = 0.20;
    public double ClampMin { get; set; } = double.NaN;
    public double ClampMax { get; set; } = double.NaN;
    public string RangeSuffix { get; set; } = "";

    public IReadOnlyList<double> Values
    {
        get => _values;
        set
        {
            var next = value?.ToList() ?? new List<double>();
            if (_values.Count == next.Count && !_values.Where((t, i) => Math.Abs(t - next[i]) > 0.001).Any())
                return;
            _values = next;
            Invalidate();
        }
    }

    public SmoothBarGraph()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        Margin = new Padding(5);
        BackColor = Theme.Card;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.None;
        e.Graphics.Clear(BackColor);

        using var borderPen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        TextRenderer.DrawText(e.Graphics, Title, Theme.SmallBold,
            new Rectangle(10, 8, Width - 20, 22), Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        var plot = new Rectangle(14, 40, Width - 28, Height - 72);
        if (plot.Width <= 4 || plot.Height <= 4) return;

        using var bg = new SolidBrush(Color.FromArgb(22, 32, 50));
        e.Graphics.FillRectangle(bg, plot);

        using var gridPen = new Pen(Color.FromArgb(36, 49, 70));
        for (int i = 0; i <= 4; i++)
        {
            int y = plot.Top + (plot.Height * i / 4);
            e.Graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
        }

        var (min, max) = GetScale();
        double span = Math.Max(0.0001, max - min);

        if (UseDynamicRange)
        {
            string top = max.ToString("0.0") + RangeSuffix;
            string bottom = min.ToString("0.0") + RangeSuffix;
            TextRenderer.DrawText(e.Graphics, top, Theme.Tiny,
                new Rectangle(plot.Right - 72, plot.Top + 2, 68, 16), Theme.Muted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, bottom, Theme.Tiny,
                new Rectangle(plot.Right - 72, plot.Bottom - 18, 68, 16), Theme.Muted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        int count = Math.Max(1, _values.Count);
        int gap = 2;
        int barW = Math.Max(2, (plot.Width - ((count - 1) * gap)) / count);
        int x = plot.Left;

        using var brush = new SolidBrush(AccentColor);
        foreach (double v in _values)
        {
            double scaled = UseDynamicRange ? (v - min) / span : v / max;
            int h = (int)Math.Round(plot.Height * Math.Clamp(scaled, 0, 1));
            h = Math.Max(1, h);
            var r = new Rectangle(x, plot.Bottom - h, barW, h);
            e.Graphics.FillRectangle(brush, r);
            x += barW + gap;
            if (x > plot.Right) break;
        }

        TextRenderer.DrawText(e.Graphics, LeftText, Theme.Normal,
            new Rectangle(12, Height - 28, Width - 24, 18), Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, RightText, Theme.Normal,
            new Rectangle(12, Height - 28, Width - 24, 18), Theme.Muted,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private (double Min, double Max) GetScale()
    {
        if (_values.Count == 0)
            return UseDynamicRange ? (0, Math.Max(1, DynamicMinRange)) : (0, 1);

        if (!UseDynamicRange)
        {
            double fixedMax = FixedMax > 0 ? FixedMax : Math.Max(1, _values.Max() * 1.2);
            return (0, fixedMax);
        }

        double min = _values.Min();
        double max = _values.Max();
        double span = max - min;
        double minimumSpan = Math.Max(0.1, DynamicMinRange);

        if (span < minimumSpan)
        {
            double mid = (min + max) / 2.0;
            min = mid - (minimumSpan / 2.0);
            max = mid + (minimumSpan / 2.0);
        }
        else
        {
            double pad = span * Math.Clamp(DynamicPaddingFraction, 0, 1);
            min -= pad;
            max += pad;
        }

        if (!double.IsNaN(ClampMin)) min = Math.Max(ClampMin, min);
        if (!double.IsNaN(ClampMax)) max = Math.Min(ClampMax, max);
        if (max <= min) max = min + minimumSpan;
        return (min, max);
    }
}

public sealed class LedMeter : UserControl
{
    private double _value;
    private double _maximum = 100;

    // REV8: Title is no longer drawn in this custom control.
    // The title is a normal WinForms Label above the meter, so bars can never cover it.
    public string Title { get; set; } = "";
    public string Unit { get; set; } = "";
    public string BottomText { get; set; } = "";
    public Color AccentColor { get; set; } = Theme.Green;
    public int Segments { get; set; } = 10;

    public double Value
    {
        get => _value;
        set
        {
            value = Math.Max(0, value);
            if (Math.Abs(_value - value) < 0.001) return;
            _value = value;
            Invalidate();
        }
    }

    public double Maximum
    {
        get => _maximum;
        set
        {
            value = Math.Max(1, value);
            if (Math.Abs(_maximum - value) < 0.001) return;
            _maximum = value;
            Invalidate();
        }
    }

    public LedMeter()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        Margin = new Padding(0);
        BackColor = Theme.Card;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(BackColor);

        int segs = Math.Clamp(Segments, 4, 10);
        int topPad = 12;
        int bottomPad = 58;
        int sidePad = 20;

        var area = new Rectangle(sidePad, topPad, Math.Max(10, Width - sidePad * 2), Math.Max(10, Height - topPad - bottomPad));
        int gap = 8;
        int segH = Math.Max(9, (area.Height - ((segs - 1) * gap)) / segs);
        int lit = (int)Math.Round(Math.Clamp(Value / Maximum, 0, 1) * segs);
        int y = area.Bottom - segH;

        for (int i = 0; i < segs; i++)
        {
            bool on = i < lit;
            Color c = AccentColor;
            if (i >= segs * 0.65) c = Theme.Yellow;
            if (i >= segs * 0.85) c = Theme.Red;

            using var br = new SolidBrush(on ? c : Color.FromArgb(35, 48, 70));
            e.Graphics.FillRectangle(br, area.Left, y, area.Width, segH);
            y -= segH + gap;
        }

        TextRenderer.DrawText(e.Graphics, $"{Value:0.0} {Unit}".Trim(), new Font("Segoe UI", 14, FontStyle.Bold),
            new Rectangle(8, Height - 46, Width - 16, 22), Theme.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        TextRenderer.DrawText(e.Graphics, BottomText, Theme.Tiny,
            new Rectangle(8, Height - 24, Width - 16, 16), Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

public sealed class HorizontalBarList : UserControl
{
    private List<BarItem> _items = new();

    public string Title { get; set; } = "BARS";
    public Color AccentColor { get; set; } = Theme.Purple;
    public int MaxItems { get; set; } = 8;

    public IReadOnlyList<BarItem> Items
    {
        get => _items;
        set
        {
            int maxItems = Math.Clamp(MaxItems, 4, 12);
            var next = value?.Take(maxItems).ToList() ?? new List<BarItem>();
            bool same = _items.Count == next.Count &&
                        !_items.Where((t, i) => t.Label != next[i].Label || t.Detail != next[i].Detail || Math.Abs(t.Value - next[i].Value) > 0.001).Any();
            if (same) return;
            _items = next;
            Invalidate();
        }
    }

    public HorizontalBarList()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        Margin = new Padding(5);
        BackColor = Theme.Card;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(BackColor);

        using var borderPen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        TextRenderer.DrawText(e.Graphics, Title, Theme.SmallBold,
            new Rectangle(10, 8, Width - 20, 22), Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        var area = new Rectangle(12, 38, Width - 24, Height - 48);
        double max = Math.Max(1, _items.Count == 0 ? 1 : _items.Max(x => x.Value));
        int maxItems = Math.Clamp(MaxItems, 4, 12);
        int rowH = Math.Max(17, area.Height / maxItems);
        int labelW = Math.Min(360, Math.Max(130, area.Width / 3));
        int valueW = 118;
        int barX = area.Left + labelW + 12;
        int barW = Math.Max(20, area.Width - labelW - valueW - 22);

        for (int i = 0; i < maxItems; i++)
        {
            int y = area.Top + i * rowH;
            if (i >= _items.Count)
            {
                using var emptyBr = new SolidBrush(Color.FromArgb(30, 41, 60));
                e.Graphics.FillRectangle(emptyBr, barX, y + 6, barW, Math.Max(4, rowH - 12));
                continue;
            }

            var item = _items[i];
            string label = string.IsNullOrWhiteSpace(item.Label) ? "unknown" : item.Label;
            TextRenderer.DrawText(e.Graphics, label, Theme.Normal,
                new Rectangle(area.Left, y, labelW, rowH), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            using var bg = new SolidBrush(Color.FromArgb(33, 45, 65));
            e.Graphics.FillRectangle(bg, barX, y + 6, barW, Math.Max(4, rowH - 12));

            int fill = (int)Math.Round((item.Value / max) * barW);
            using var fg = new SolidBrush(AccentColor);
            e.Graphics.FillRectangle(fg, barX, y + 6, fill, Math.Max(4, rowH - 12));

            string valueText = string.IsNullOrWhiteSpace(item.Detail) ? item.Value.ToString("0") : item.Detail;
            TextRenderer.DrawText(e.Graphics, valueText,
                Theme.SmallBold, new Rectangle(area.Right - valueW, y, valueW, rowH), Theme.Muted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
