using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Windows.Forms;
using FormsTimer = System.Windows.Forms.Timer;
using AICreatureLab.Core;

namespace AICreatureLab.UI;

internal sealed class MainForm : Form
{
    private readonly SimulationWorld _world;
    private readonly BufferedPanel _viewport;
    private readonly BufferedPanel _graphPanel;
    private readonly FormsTimer _timer;

    private readonly Label _lblPopulation;
    private readonly Label _lblGenerations;
    private readonly Label _lblChampion;
    private readonly Label _lblEnergy;
    private readonly Label _lblTime;
    private readonly Label _lblResources;
    private readonly Label _lblLoaded;
    private readonly Label _lblSpeed;
    private readonly TextBox _logBox;
    private readonly TrackBar _speedTrack;
    private readonly NumericUpDown _mutationStrength;
    private readonly NumericUpDown _mutationChance;
    private readonly NumericUpDown _foodCount;
    private readonly NumericUpDown _hazardCount;
    private readonly CheckBox _chkDrawSensors;

    private bool _paused;
    private bool _runtimeErrorShown;
    private float _lastLoggedChampionScore;
    private int _lastLoggedGeneration;

    public MainForm()
    {
        _world = new SimulationWorld(new SimulationConfig());

        Text = AICreatureLab.AppInfo.DisplayName;
        MinimumSize = new Size(1320, 820);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        KeyPreview = true;
        BackColor = Color.FromArgb(18, 18, 26);
        ForeColor = Color.White;

        var rightPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 390,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(24, 24, 34)
        };

        _viewport = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(10, 13, 19)
        };
        _viewport.Paint += Viewport_Paint;

        _graphPanel = new BufferedPanel
        {
            Dock = DockStyle.Bottom,
            Height = 180,
            BackColor = Color.FromArgb(16, 18, 25)
        };
        _graphPanel.Paint += GraphPanel_Paint;

        var infoLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(0, 0, 0, 8)
        };

        _lblPopulation = CreateInfoLabel();
        _lblGenerations = CreateInfoLabel();
        _lblChampion = CreateInfoLabel();
        _lblEnergy = CreateInfoLabel();
        _lblTime = CreateInfoLabel();
        _lblResources = CreateInfoLabel();
        _lblLoaded = CreateInfoLabel();
        _lblSpeed = CreateInfoLabel();

        infoLayout.Controls.Add(_lblPopulation);
        infoLayout.Controls.Add(_lblGenerations);
        infoLayout.Controls.Add(_lblChampion);
        infoLayout.Controls.Add(_lblEnergy);
        infoLayout.Controls.Add(_lblTime);
        infoLayout.Controls.Add(_lblResources);
        infoLayout.Controls.Add(_lblLoaded);
        infoLayout.Controls.Add(_lblSpeed);

        var buttonFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 78,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 8)
        };

        var btnPause = CreateButton("Pause / Resume", (_, _) => TogglePause());
        var btnReset = CreateButton("Reset Random", (_, _) => ResetRandom());
        var btnSave = CreateButton("Save Champion", (_, _) => SaveChampion());
        var btnLoad = CreateButton("Load Champion", (_, _) => LoadChampion());
        var btnSeedChampion = CreateButton("Restart From Loaded", (_, _) => RestartFromLoadedGenome());

        buttonFlow.Controls.Add(btnPause);
        buttonFlow.Controls.Add(btnReset);
        buttonFlow.Controls.Add(btnSave);
        buttonFlow.Controls.Add(btnLoad);
        buttonFlow.Controls.Add(btnSeedChampion);

        var runtimeGroup = new GroupBox
        {
            Text = "Runtime Controls",
            Dock = DockStyle.Top,
            Height = 250,
            ForeColor = Color.White
        };

        var runtimeLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(8)
        };
        runtimeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        runtimeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _speedTrack = new TrackBar
        {
            Minimum = 1,
            Maximum = 200,
            TickFrequency = 20,
            Value = 10,
            Dock = DockStyle.Fill
        };
        _speedTrack.ValueChanged += (_, _) => ApplyRuntimeSettings();

        _mutationStrength = new NumericUpDown
        {
            DecimalPlaces = 2,
            Increment = 0.01M,
            Minimum = 0.01M,
            Maximum = 1.50M,
            Value = (decimal)_world.Config.MutationStrength,
            Dock = DockStyle.Fill
        };
        _mutationStrength.ValueChanged += (_, _) => ApplyRuntimeSettings();

        _mutationChance = new NumericUpDown
        {
            DecimalPlaces = 2,
            Increment = 0.01M,
            Minimum = 0.01M,
            Maximum = 1.00M,
            Value = (decimal)_world.Config.MutationChance,
            Dock = DockStyle.Fill
        };
        _mutationChance.ValueChanged += (_, _) => ApplyRuntimeSettings();

        _foodCount = new NumericUpDown
        {
            Minimum = 25,
            Maximum = 800,
            Value = _world.Config.TargetFoodCount,
            Dock = DockStyle.Fill
        };
        _foodCount.ValueChanged += (_, _) => ApplyRuntimeSettings();

        _hazardCount = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 300,
            Value = _world.Config.TargetHazardCount,
            Dock = DockStyle.Fill
        };
        _hazardCount.ValueChanged += (_, _) => ApplyRuntimeSettings();

        _chkDrawSensors = new CheckBox
        {
            Text = "Draw champion sensors",
            Checked = _world.DrawSensors,
            Dock = DockStyle.Left,
            AutoSize = true,
            ForeColor = Color.White
        };
        _chkDrawSensors.CheckedChanged += (_, _) => ApplyRuntimeSettings();

        runtimeLayout.Controls.Add(CreateRuntimeLabel("Simulation speed"), 0, 0);
        runtimeLayout.Controls.Add(_speedTrack, 1, 0);
        runtimeLayout.Controls.Add(CreateRuntimeLabel("Mutation strength"), 0, 1);
        runtimeLayout.Controls.Add(_mutationStrength, 1, 1);
        runtimeLayout.Controls.Add(CreateRuntimeLabel("Mutation chance"), 0, 2);
        runtimeLayout.Controls.Add(_mutationChance, 1, 2);
        runtimeLayout.Controls.Add(CreateRuntimeLabel("Food target"), 0, 3);
        runtimeLayout.Controls.Add(_foodCount, 1, 3);
        runtimeLayout.Controls.Add(CreateRuntimeLabel("Hazard target"), 0, 4);
        runtimeLayout.Controls.Add(_hazardCount, 1, 4);
        runtimeLayout.Controls.Add(CreateRuntimeLabel("Visuals"), 0, 5);
        runtimeLayout.Controls.Add(_chkDrawSensors, 1, 5);

        runtimeGroup.Controls.Add(runtimeLayout);

        var helpLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 70,
            ForeColor = Color.Gainsboro,
            Text = "Keys: Space pause | R reset | Ctrl+S save champion | Ctrl+O load champion | C restart from loaded genome",
            Padding = new Padding(0, 8, 0, 8)
        };

        _logBox = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(14, 16, 21),
            ForeColor = Color.Gainsboro,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 10f)
        };

        rightPanel.Controls.Add(_logBox);
        rightPanel.Controls.Add(helpLabel);
        rightPanel.Controls.Add(runtimeGroup);
        rightPanel.Controls.Add(buttonFlow);
        rightPanel.Controls.Add(infoLayout);

        Controls.Add(_viewport);
        Controls.Add(_graphPanel);
        Controls.Add(rightPanel);

        _timer = new FormsTimer { Interval = 16 };
        _timer.Tick += Timer_Tick;
        _timer.Start();

        KeyDown += MainForm_KeyDown;

        ApplyRuntimeSettings();
        UpdateUi();
        Log($"{AICreatureLab.AppInfo.DisplayName} started.");
        Log("Original C# implementation ready. Creatures are learning how not to die.");
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        try
        {
            if (!_paused)
            {
                _world.Update(1f / 60f);
                MaybeLogChampionMilestones();
            }
        }
        catch (Exception ex)
        {
            HandleRuntimeException("simulation tick", ex);
        }

        UpdateUi();
        _viewport.Invalidate();
        _graphPanel.Invalidate();
    }

    private void TogglePause()
    {
        _paused = !_paused;
        Log(_paused ? "Simulation paused." : "Simulation resumed.");
    }

    private void ResetRandom()
    {
        _world.ResetWithRandomPopulation();
        _lastLoggedChampionScore = 0f;
        _lastLoggedGeneration = 0;
        Log("Reset to a brand new random population.");
    }

    private void RestartFromLoadedGenome()
    {
        if (_world.LoadedGenome is null)
        {
            Log("No saved genome is loaded yet.");
            return;
        }

        _world.ResetFromLoadedChampion(_world.LoadedGenome);
        _lastLoggedChampionScore = 0f;
        _lastLoggedGeneration = _world.LoadedGenome.Generation;
        Log($"Restarted world from loaded champion genome (generation {_world.LoadedGenome.Generation}).");
    }

    private void SaveChampion()
    {
        var genome = _world.SaveChampionGenome();
        if (genome is null)
        {
            Log("There is no champion yet to save.");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "AI Creature Genome (*.json)|*.json",
            FileName = $"AICreatureLab_champion_v{AICreatureLab.AppInfo.Version}_gen{genome.Generation}.json",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var json = JsonSerializer.Serialize(genome, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(dialog.FileName, json);
        Log($"Champion genome saved to: {dialog.FileName}");
    }

    private void LoadChampion()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "AI Creature Genome (*.json)|*.json",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var json = File.ReadAllText(dialog.FileName);
        var genome = JsonSerializer.Deserialize<SavedGenome>(json);

        if (genome is null)
        {
            Log("Failed to load genome file.");
            return;
        }

        _world.LoadGenome(genome);
        _lastLoggedChampionScore = 0f;
        _lastLoggedGeneration = genome.Generation;
        Log($"Loaded genome from: {dialog.FileName}");
    }

    private void ApplyRuntimeSettings()
    {
        _world.TimeScale = _speedTrack.Value / 10f;
        _world.Config.MutationStrength = (double)_mutationStrength.Value;
        _world.Config.MutationChance = (double)_mutationChance.Value;
        _world.Config.TargetFoodCount = (int)_foodCount.Value;
        _world.Config.TargetHazardCount = (int)_hazardCount.Value;
        _world.DrawSensors = _chkDrawSensors.Checked;
    }

    private void UpdateUi()
    {
        _lblPopulation.Text = $"Population: {_world.Creatures.Count} alive | births {_world.TotalBirths} | deaths {_world.TotalDeaths}";
        _lblGenerations.Text = $"Generation: highest {_world.HighestGeneration} | avg age {_world.AverageAge:0.0}s";
        _lblChampion.Text = $"Champion: score {_world.ChampionScore:0.00} | children {_world.Champion?.ChildrenProduced ?? 0}";
        _lblEnergy.Text = $"Average energy: {_world.AverageEnergy:0.0} | best energy {_world.Champion?.MaxEnergySeen ?? 0f:0.0}";
        _lblTime.Text = $"Sim time: {_world.TimeSeconds:0.0}s | mode {(_paused ? "paused" : "running")}";
        _lblResources.Text = $"Food eaten {_world.TotalFoodEaten} | hazard hits {_world.TotalHazardHits} | food {_world.Foods.Count} | hazard {_world.Hazards.Count}";
        _lblLoaded.Text = _world.LoadedGenome is null
            ? "Loaded champion: none"
            : $"Loaded champion: gen {_world.LoadedGenome.Generation} | saved {_world.LoadedGenome.CreatedUtc:u}";
        _lblSpeed.Text = $"{AICreatureLab.AppInfo.DisplayName} | workers up to {_world.WorkerThreadCount} | {(_world.LastUpdateUsedParallel ? "parallel" : "single-core")} tick {_world.LastUpdateMilliseconds:0.00} ms | scale {_world.TimeScale:0.0}x | mutation {_world.Config.MutationStrength:0.00} / {_world.Config.MutationChance:0.00}";
    }

    private void MaybeLogChampionMilestones()
    {
        if (_world.Champion is null)
        {
            return;
        }

        if (_world.ChampionScore >= _lastLoggedChampionScore + 30f)
        {
            _lastLoggedChampionScore = _world.ChampionScore;
            Log($"New champion score {_world.ChampionScore:0.00} at generation {_world.Champion.Generation}.");
        }

        if (_world.HighestGeneration > _lastLoggedGeneration)
        {
            _lastLoggedGeneration = _world.HighestGeneration;
            Log($"Evolution reached generation {_lastLoggedGeneration}.");
        }
    }

    private void Viewport_Paint(object? sender, PaintEventArgs e)
    {
        try
        {
            var foods = _world.GetFoodSnapshot();
            var hazards = _world.GetHazardSnapshot();
            var creatures = _world.GetCreatureSnapshot();

            var g = e.Graphics;
            g.Clear(Color.FromArgb(10, 13, 19));
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var worldRect = GetWorldRectangle(_viewport.ClientRectangle);
            using var worldBrush = new SolidBrush(Color.FromArgb(12, 18, 28));
            using var worldPen = new Pen(Color.FromArgb(50, 110, 160), 2f);
            g.FillRectangle(worldBrush, worldRect);
            g.DrawRectangle(worldPen, Rectangle.Round(worldRect));

            var scaleX = worldRect.Width / _world.Config.WorldWidth;
            var scaleY = worldRect.Height / _world.Config.WorldHeight;
            var scale = Math.Min(scaleX, scaleY);

            PointF ToScreen(Vector2 point) =>
                new(
                    worldRect.Left + (point.X * scale),
                    worldRect.Top + (point.Y * scale));

            foreach (var food in foods)
        {
            var p = ToScreen(food.Position);
            var r = Math.Max(2.5f, _world.Config.FoodRadius * scale);
            using var b = new SolidBrush(Color.FromArgb(90, 230, 120));
            g.FillEllipse(b, p.X - r, p.Y - r, r * 2f, r * 2f);
        }

        foreach (var hazard in hazards)
        {
            var p = ToScreen(hazard.Position);
            var r = Math.Max(4f, _world.Config.HazardRadius * scale);
            using var b = new SolidBrush(Color.FromArgb(225, 85, 85));
            g.FillEllipse(b, p.X - r, p.Y - r, r * 2f, r * 2f);
            using var pen = new Pen(Color.FromArgb(255, 170, 170), 1.5f);
            g.DrawEllipse(pen, p.X - r, p.Y - r, r * 2f, r * 2f);
        }

        foreach (var creature in creatures)
        {
            var p = ToScreen(creature.Position);
            var radius = Math.Max(5.5f, _world.Config.CreatureRadius * scale);
            using var brush = new SolidBrush(creature.BodyColor);
            g.FillEllipse(brush, p.X - radius, p.Y - radius, radius * 2f, radius * 2f);

            var heading = MathUtil.AngleToVector(creature.FacingAngle);
            var nose = ToScreen(creature.Position + (heading * (_world.Config.CreatureRadius * 1.9f)));
            using var linePen = new Pen(Color.FromArgb(245, 245, 245), 1.8f);
            g.DrawLine(linePen, p, nose);

            if (ReferenceEquals(creature, _world.Champion))
            {
                using var championPen = new Pen(Color.Gold, 2.4f);
                g.DrawEllipse(championPen, p.X - radius - 2f, p.Y - radius - 2f, (radius * 2f) + 4f, (radius * 2f) + 4f);
            }
        }

        if (_world.DrawSensors && _world.Champion is not null)
        {
            var champion = _world.Champion;
            var from = ToScreen(champion.Position);
            DrawSensorLine(g, from, champion.Position + _world.FindNearestFood(champion.Position).VectorToTarget, ToScreen, Color.FromArgb(90, 230, 120));
            DrawSensorLine(g, from, champion.Position + _world.FindNearestHazard(champion.Position).VectorToTarget, ToScreen, Color.FromArgb(225, 85, 85));
            DrawSensorLine(g, from, champion.Position + _world.FindNearestCreature(champion.Position, champion).VectorToTarget, ToScreen, Color.FromArgb(110, 180, 255));
        }

        using var hudBack = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
        using var hudFore = new SolidBrush(Color.WhiteSmoke);
        var hudRect = new RectangleF(worldRect.Left + 12f, worldRect.Top + 12f, 360f, 88f);
        g.FillRectangle(hudBack, hudRect);
        g.DrawString(
            $"{AICreatureLab.AppInfo.DisplayName}\nPopulation: {creatures.Length}   Highest Gen: {_world.HighestGeneration}\nChampion Score: {_world.ChampionScore:0.00}   Time Scale: {_world.TimeScale:0.0}x",
            Font,
            hudFore,
            hudRect);
        }
        catch (Exception ex)
        {
            HandleRuntimeException("viewport render", ex);
        }
    }

    private void GraphPanel_Paint(object? sender, PaintEventArgs e)
    {
        try
        {
            var history = _world.GetHistorySnapshot();

            var g = e.Graphics;
        g.Clear(Color.FromArgb(16, 18, 25));
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var rect = new RectangleF(12f, 12f, _graphPanel.ClientSize.Width - 24f, _graphPanel.ClientSize.Height - 30f);
        if (rect.Width <= 10f || rect.Height <= 10f)
        {
            return;
        }

        using var borderPen = new Pen(Color.FromArgb(68, 86, 126), 1.3f);
        g.DrawRectangle(borderPen, Rectangle.Round(rect));

        if (history.Length < 2)
        {
            return;
        }

        var maxPopulation = Math.Max(10, history.Max(h => h.Population));
        var maxScore = Math.Max(10f, history.Max(h => h.BestScore));
        var maxEnergy = Math.Max(10f, history.Max(h => h.AverageEnergy));

        DrawSeries(g, rect, history.Length,
            index => history[index].Population / (float)maxPopulation,
            Color.FromArgb(100, 220, 255));

        DrawSeries(g, rect, history.Length,
            index => history[index].BestScore / maxScore,
            Color.FromArgb(255, 210, 90));

        DrawSeries(g, rect, history.Length,
            index => history[index].AverageEnergy / maxEnergy,
            Color.FromArgb(120, 240, 145));

        using var textBrush = new SolidBrush(Color.Gainsboro);
        g.DrawString("Population", Font, textBrush, 16f, 16f);
        g.DrawString("Champion Score", Font, textBrush, 110f, 16f);
        g.DrawString("Avg Energy", Font, textBrush, 245f, 16f);

        using var popPen = new Pen(Color.FromArgb(100, 220, 255), 3f);
        using var scorePen = new Pen(Color.FromArgb(255, 210, 90), 3f);
        using var energyPen = new Pen(Color.FromArgb(120, 240, 145), 3f);

        g.DrawLine(popPen, 16f, 35f, 42f, 35f);
        g.DrawLine(scorePen, 110f, 35f, 136f, 35f);
        g.DrawLine(energyPen, 245f, 35f, 271f, 35f);
        }
        catch (Exception ex)
        {
            HandleRuntimeException("history render", ex);
        }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
        {
            TogglePause();
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.R)
        {
            ResetRandom();
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.C)
        {
            RestartFromLoadedGenome();
            e.SuppressKeyPress = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.S)
        {
            SaveChampion();
            e.SuppressKeyPress = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.O)
        {
            LoadChampion();
            e.SuppressKeyPress = true;
        }
    }

    private void HandleRuntimeException(string context, Exception ex)
    {
        _paused = true;

        if (_runtimeErrorShown)
        {
            return;
        }

        _runtimeErrorShown = true;
        var crashPath = WriteCrashLog(context, ex);
        Log($"Runtime error during {context}: {ex.Message}");
        Log($"Crash log: {crashPath}");

        MessageBox.Show(
            this,
            $"The simulation hit an error during {context} and was paused.\n\n{ex.Message}\n\nCrash log: {crashPath}",
            AICreatureLab.AppInfo.DisplayName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static string WriteCrashLog(string context, Exception ex)
    {
        try
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AICreatureLab",
                "CrashLogs");

            Directory.CreateDirectory(baseDir);
            var filePath = Path.Combine(baseDir, $"AICreatureLab_{AICreatureLab.AppInfo.Version}_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            var content = $"Context: {context}{Environment.NewLine}{Environment.NewLine}{ex}";
            File.WriteAllText(filePath, content);
            return filePath;
        }
        catch
        {
            return "Unable to write crash log.";
        }
    }

    private void Log(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        _logBox.AppendText(line + Environment.NewLine);
    }

    private static Label CreateInfoLabel() =>
        new()
        {
            Dock = DockStyle.Top,
            ForeColor = Color.Gainsboro,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 4)
        };

    private static Label CreateRuntimeLabel(string text) =>
        new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            ForeColor = Color.Gainsboro,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true
        };

    private Button CreateButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 30,
            BackColor = Color.FromArgb(45, 58, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(4)
        };

        button.FlatAppearance.BorderColor = Color.FromArgb(90, 130, 190);
        button.Click += onClick;
        return button;
    }

    private RectangleF GetWorldRectangle(Rectangle clientRect)
    {
        const float margin = 18f;
        var width = clientRect.Width - (margin * 2f);
        var height = clientRect.Height - (margin * 2f);

        var scale = Math.Min(width / _world.Config.WorldWidth, height / _world.Config.WorldHeight);
        var worldWidth = _world.Config.WorldWidth * scale;
        var worldHeight = _world.Config.WorldHeight * scale;

        return new RectangleF(
            (clientRect.Width - worldWidth) / 2f,
            (clientRect.Height - worldHeight) / 2f,
            worldWidth,
            worldHeight);
    }

    private static void DrawSeries(Graphics g, RectangleF rect, int count, Func<int, float> selector, Color color)
    {
        if (count < 2)
        {
            return;
        }

        using var pen = new Pen(color, 2f);

        PointF GetPoint(int index)
        {
            var t = index / (float)(count - 1);
            var value = Math.Clamp(selector(index), 0f, 1f);
            return new PointF(
                rect.Left + (rect.Width * t),
                rect.Bottom - (rect.Height * value));
        }

        var points = Enumerable.Range(0, count).Select(GetPoint).ToArray();
        g.DrawLines(pen, points);
    }

    private static void DrawSensorLine(Graphics g, PointF fromScreen, Vector2 targetWorld, Func<Vector2, PointF> toScreen, Color color)
    {
        var toScreenPoint = toScreen(targetWorld);
        using var pen = new Pen(color, 1.6f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        g.DrawLine(pen, fromScreen, toScreenPoint);
    }
}
