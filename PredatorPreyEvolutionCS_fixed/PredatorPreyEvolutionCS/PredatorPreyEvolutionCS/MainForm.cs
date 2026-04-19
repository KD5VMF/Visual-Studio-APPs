namespace PredatorPreyEvolutionCS;

public sealed class MainForm : Form
{
    private readonly DoubleBufferedPanel _view = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly World _world;

    private readonly TrackBar _speedBar = new();
    private readonly Label _speedValueLabel = new();
    private readonly NumericUpDown _preyCount = new();
    private readonly NumericUpDown _predatorCount = new();
    private readonly NumericUpDown _plantCount = new();
    private readonly Button _pauseButton = new();
    private readonly Button _resetButton = new();
    private readonly Button _helpButton = new();
    private readonly Label _statusLabel = new();

    private bool _paused;

    public MainForm()
    {
        Text = "Predator / Prey Evolution in C#";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1200, 760);
        ClientSize = new Size(1560, 920);
        BackColor = Color.FromArgb(18, 22, 32);
        Font = new Font("Segoe UI", 9f, FontStyle.Regular);

        _world = new World(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));
        BuildUi();

        _timer.Interval = 16;
        _timer.Tick += TimerTick;
        _timer.Start();
        UpdateStatusText();
        Shown += (_, _) => ResetWorld();
    }

    private void BuildUi()
    {
        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(24, 29, 41),
            Padding = new Padding(12, 10, 12, 10)
        };


        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight
        };
        topBar.Controls.Add(flow);

        flow.Controls.Add(CreateButton(_pauseButton, "Pause / Resume", (_, _) =>
        {
            _paused = !_paused;
            UpdateStatusText();
        }));

        flow.Controls.Add(CreateButton(_resetButton, "New World", (_, _) => ResetWorld()));

        flow.Controls.Add(CreateButton(_helpButton, "What this does", (_, _) => ShowHelp()));

        flow.Controls.Add(CreateDivider());

        flow.Controls.Add(CreateLabeledNumeric("Prey", _preyCount, 1, 500, 90));
        flow.Controls.Add(CreateLabeledNumeric("Predators", _predatorCount, 1, 200, 14));
        flow.Controls.Add(CreateLabeledNumeric("Plants", _plantCount, 5, 500, 90));

        flow.Controls.Add(CreateDivider());

        var speedPanel = new Panel
        {
            Width = 300,
            Height = 50,
            Margin = new Padding(8, 2, 8, 2)
        };

        var speedLabel = new Label
        {
            Text = "Simulation speed",
            ForeColor = Color.WhiteSmoke,
            AutoSize = true,
            Location = new Point(0, 2)
        };
        speedPanel.Controls.Add(speedLabel);

        _speedBar.Minimum = 1;
        _speedBar.Maximum = 25;
        _speedBar.Value = 4;
        _speedBar.TickStyle = TickStyle.None;
        _speedBar.Width = 210;
        _speedBar.Location = new Point(0, 20);
        _speedBar.Scroll += (_, _) => UpdateStatusText();
        speedPanel.Controls.Add(_speedBar);

        _speedValueLabel.AutoSize = true;
        _speedValueLabel.ForeColor = Color.Gainsboro;
        _speedValueLabel.Location = new Point(220, 22);
        speedPanel.Controls.Add(_speedValueLabel);

        flow.Controls.Add(speedPanel);

        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.Gainsboro;
        _statusLabel.Margin = new Padding(18, 14, 0, 0);
        flow.Controls.Add(_statusLabel);

        _view.Dock = DockStyle.Fill;
        _view.Paint += ViewPaint;
        _view.Resize += (_, _) =>
        {
            _world.Resize(_view.ClientSize.Width, _view.ClientSize.Height);
            _view.Invalidate();
        };
        _view.MouseDown += ViewMouseDown;
        Controls.Add(_view);
        Controls.Add(topBar);
    }

    private Control CreateDivider()
    {
        return new Panel
        {
            Width = 12,
            Height = 40,
            Margin = new Padding(4, 8, 4, 8),
            BackColor = Color.FromArgb(55, 255, 255, 255)
        };
    }

    private Control CreateLabeledNumeric(string label, NumericUpDown nud, int min, int max, int value)
    {
        var panel = new Panel
        {
            Width = 108,
            Height = 50,
            Margin = new Padding(8, 2, 8, 2)
        };

        var title = new Label
        {
            Text = label,
            ForeColor = Color.WhiteSmoke,
            AutoSize = true,
            Location = new Point(0, 2)
        };
        panel.Controls.Add(title);

        nud.Minimum = min;
        nud.Maximum = max;
        nud.Value = value;
        nud.Width = 86;
        nud.Location = new Point(0, 22);
        nud.BackColor = Color.FromArgb(245, 245, 245);
        panel.Controls.Add(nud);

        return panel;
    }

    private Button CreateButton(Button button, string text, EventHandler onClick)
    {
        button.Text = text;
        button.Width = 130;
        button.Height = 38;
        button.Margin = new Padding(4, 6, 4, 6);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(80, 255, 255, 255);
        button.FlatAppearance.BorderSize = 1;
        button.BackColor = Color.FromArgb(36, 43, 60);
        button.ForeColor = Color.WhiteSmoke;
        button.Click += onClick;
        return button;
    }

    private void ResetWorld()
    {
        _world.Resize(_view.ClientSize.Width, _view.ClientSize.Height);
        _world.Reset((int)_preyCount.Value, (int)_predatorCount.Value, (int)_plantCount.Value);
        _paused = false;
        UpdateStatusText();
        _view.Invalidate();
    }

    private void TimerTick(object? sender, EventArgs e)
    {
        if (!_paused)
        {
            var steps = _speedBar.Value;
            const double dt = 1.0 / 60.0;

            for (var i = 0; i < steps; i++)
            {
                _world.Update(dt);
            }
        }

        UpdateStatusText();
        _view.Invalidate();
    }

    private void UpdateStatusText()
    {
        _speedValueLabel.Text = $"{_speedBar.Value}x";
        _statusLabel.Text = _paused
            ? "Paused"
            : $"Running • {_world.Preys.Count} prey • {_world.Predators.Count} predators • {_world.Plants.Count} plants • {_world.Foods.Count} food";
    }

    private void ViewPaint(object? sender, PaintEventArgs e)
    {
        _world.Draw(e.Graphics, _view.ClientRectangle);
    }

    private void ViewMouseDown(object? sender, MouseEventArgs e)
    {
        _world.SelectNearest(e.Location, 20);
        _view.Invalidate();
    }

    private void ShowHelp()
    {
        var message =
            "This C# project recreates the core idea from the video as a real-time 2D ecosystem.\n\n" +
            "• Plants slowly spawn food pellets and can spread.\n" +
            "• Prey agents use a tiny neural network to search for food, avoid predators, and reproduce.\n" +
            "• Predator agents use a tiny neural network to hunt prey, survive, and reproduce.\n" +
            "• Children inherit their parent's brain and traits with mutations, so the population gradually changes over time.\n" +
            "• Click an object in the world to inspect its current stats.\n\n" +
            "This is a desktop simulation, not a giant ML training stack, so it stays easy to build and run in Visual Studio.";

        var wasPaused = _paused;
        _paused = true;
        UpdateStatusText();

        MessageBox.Show(this, message, "About this simulation", MessageBoxButtons.OK, MessageBoxIcon.Information);

        _paused = wasPaused;
        UpdateStatusText();
    }
}
