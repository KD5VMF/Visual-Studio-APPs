using System.Windows;
using System.Windows.Input;
using AtomPlayground.Models;
using AtomPlayground.Utils;

namespace AtomPlayground;

public partial class MainWindow : Window
{
    private bool _wasPausedBeforeHelp;
    private bool _uiReady;

    public MainWindow()
    {
        InitializeComponent();

        ModeComboBox.ItemsSource = Enum.GetValues(typeof(SandboxMode));
        ElementComboBox.ItemsSource = ElementCatalog.All;
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        ModeComboBox.SelectedItem = SandboxMode.Chemistry;
        ElementComboBox.SelectedItem = ElementCatalog.GetBySymbol("C");

        SandboxControl.InitializeIfNeeded();
        SandboxControl.World.Mode = SandboxMode.Chemistry;
        SandboxControl.World.TimeScale = TimeScaleSlider.Value;
        SandboxControl.World.SmashEnergy = CollisionEnergySlider.Value;

        UpdateTimeScaleText();
        UpdateCollisionEnergyText();
        ApplyDisplayOptions();
        RefreshInfoPanel();
        UpdateFullscreenButtonText();
        _uiReady = true;
    }

    private void SpawnAtomButton_OnClick(object sender, RoutedEventArgs e)
    {
        var template = GetSelectedTemplate();
        var bounds = SandboxControl.World.Bounds;
        var center = new Point(bounds.Width * 0.5, bounds.Height * 0.5);
        var spawn = new Point(center.X + Random.Shared.Next(-220, 220), center.Y + Random.Shared.Next(-180, 180));
        var velocity = new Vector((Random.Shared.NextDouble() - 0.5) * 30, (Random.Shared.NextDouble() - 0.5) * 30);
        SandboxControl.World.AddAtom(template, spawn, velocity);
        AddEventLine($"Spawned {template.Name} atom.");
    }

    private void SpawnClusterButton_OnClick(object sender, RoutedEventArgs e)
    {
        var template = GetSelectedTemplate();
        var bounds = SandboxControl.World.Bounds;
        var center = new Point(bounds.Width * 0.5 + Random.Shared.Next(-140, 140), bounds.Height * 0.5 + Random.Shared.Next(-120, 120));
        SandboxControl.World.AddCluster(template, center);
    }

    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        SandboxControl.ResetView();
        SandboxControl.World.Reset();
        RefreshInfoPanel();
        EventListBox.Items.Clear();
        foreach (var evt in SandboxControl.World.RecentEvents)
        {
            EventListBox.Items.Add(evt.ToString());
        }
    }

    private void ClearAllButton_OnClick(object sender, RoutedEventArgs e)
    {
        SandboxControl.ResetView();
        SandboxControl.World.ClearAll();
        RefreshInfoPanel();
        EventListBox.Items.Clear();
        foreach (var evt in SandboxControl.World.RecentEvents)
        {
            EventListBox.Items.Add(evt.ToString());
        }
    }

    private void ModeComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ModeComboBox.SelectedItem is SandboxMode mode)
        {
            SandboxControl.World.Mode = mode;
            AddEventLine($"Observation mode set to {mode}.");
            RefreshInfoPanel();
        }
    }

    private void TimeScaleSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
        {
            return;
        }

        SandboxControl.World.TimeScale = TimeScaleSlider.Value;
        UpdateTimeScaleText();
    }

    private void CollisionEnergySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
        {
            return;
        }

        SandboxControl.World.SmashEnergy = CollisionEnergySlider.Value;
        UpdateCollisionEnergyText();
    }

    private void PauseButton_OnClick(object sender, RoutedEventArgs e)
    {
        SandboxControl.World.Paused = !SandboxControl.World.Paused;
        AddEventLine(SandboxControl.World.Paused ? "Simulation paused." : "Simulation resumed.");
    }

    private void SmashSelectedButton_OnClick(object sender, RoutedEventArgs e)
    {
        SandboxControl.World.SmashSelected(CollisionEnergySlider.Value);
        RefreshInfoPanel();
    }

    private void CoolVelocitiesButton_OnClick(object sender, RoutedEventArgs e)
    {
        SandboxControl.World.CoolVelocities();
        RefreshInfoPanel();
    }

    private void AddElectronButton_OnClick(object sender, RoutedEventArgs e)
    {
        SandboxControl.World.NudgeSelectedParticleCounts(0, 0, +1);
        RefreshInfoPanel();
    }

    private void RemoveElectronButton_OnClick(object sender, RoutedEventArgs e)
    {
        SandboxControl.World.NudgeSelectedParticleCounts(0, 0, -1);
        RefreshInfoPanel();
    }

    private void AddNeutronButton_OnClick(object sender, RoutedEventArgs e)
    {
        SandboxControl.World.NudgeSelectedParticleCounts(0, +1, 0);
        RefreshInfoPanel();
    }

    private void RemoveNeutronButton_OnClick(object sender, RoutedEventArgs e)
    {
        SandboxControl.World.NudgeSelectedParticleCounts(0, -1, 0);
        RefreshInfoPanel();
    }

    private void AddProtonButton_OnClick(object sender, RoutedEventArgs e)
    {
        SandboxControl.World.NudgeSelectedParticleCounts(+1, 0, 0);
        RefreshInfoPanel();
    }

    private void RemoveProtonButton_OnClick(object sender, RoutedEventArgs e)
    {
        SandboxControl.World.NudgeSelectedParticleCounts(-1, 0, 0);
        RefreshInfoPanel();
    }

    private void DisplayCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_uiReady)
        {
            return;
        }

        ApplyDisplayOptions();
    }

    private void SandboxControl_OnSelectionChanged(object? sender, EventArgs e)
    {
        RefreshInfoPanel();
    }

    private void SandboxControl_OnEventLogged(object? sender, WorldEvent e)
    {
        AddEventLine(e.ToString());
        RefreshInfoPanel();
    }

    private void ShowHelpButton_OnClick(object sender, RoutedEventArgs e)
    {
        _wasPausedBeforeHelp = SandboxControl.World.Paused;
        SandboxControl.World.Paused = true;
        HelpOverlay.Visibility = Visibility.Visible;
    }

    private void HelpCloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        HelpOverlay.Visibility = Visibility.Collapsed;
        SandboxControl.World.Paused = _wasPausedBeforeHelp;
    }

    private void CenterFitButton_OnClick(object sender, RoutedEventArgs e)
    {
        SandboxControl.ResetView();
        AddEventLine("View reset to fit the full atom world.");
    }

    private void ToggleFullscreenButton_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void Window_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && WindowState == WindowState.Maximized && WindowStyle == WindowStyle.None)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
    }

    private void ToggleFullscreen()
    {
        if (WindowStyle == WindowStyle.None)
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.CanResize;
        }
        else
        {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            ResizeMode = ResizeMode.CanResize;
        }

        UpdateFullscreenButtonText();
        SandboxControl.ResetView();
        AddEventLine(WindowStyle == WindowStyle.None ? "Entered fullscreen mode." : "Returned to windowed mode.");
    }

    private void UpdateFullscreenButtonText()
    {
        if (FullscreenButton is null)
        {
            return;
        }

        FullscreenButton.Content = WindowStyle == WindowStyle.None ? "Windowed" : "Fullscreen";
    }

    private void RefreshInfoPanel()
    {
        var atom = SandboxControl.PrimarySelection;
        if (atom is null)
        {
            InfoNameText.Text = "None";
            InfoAtomicNumberText.Text = "-";
            InfoMassNumberText.Text = "-";
            InfoElectronsText.Text = "-";
            InfoChargeText.Text = "-";
            InfoChemistryText.Text = "-";
            InfoStabilityText.Text = "-";
            InfoVelocityText.Text = "-";
            return;
        }

        InfoNameText.Text = $"{atom.Name} ({atom.Symbol})";
        InfoAtomicNumberText.Text = atom.Protons.ToString();
        InfoMassNumberText.Text = atom.MassNumber.ToString();
        InfoElectronsText.Text = atom.Electrons.ToString();
        InfoChargeText.Text = atom.Charge switch
        {
            > 0 => $"+{atom.Charge}",
            < 0 => atom.Charge.ToString(),
            _ => "0"
        };
        InfoChemistryText.Text = SandboxControl.World.DescribeChemistry(atom);
        InfoStabilityText.Text = SandboxControl.World.EstimateStability(atom);
        InfoVelocityText.Text = $"{atom.Velocity.Length:0.0}";
    }

    private void UpdateTimeScaleText()
    {
        TimeScaleText.Text = $"{TimeScaleSlider.Value:0.0}x";
    }

    private void UpdateCollisionEnergyText()
    {
        CollisionEnergyText.Text = $"{CollisionEnergySlider.Value:0}";
    }

    private void ApplyDisplayOptions()
    {
        if (!IsLoaded || SandboxControl is null || SandboxControl.World is null ||
            ShowHudCheckBox is null || ShowShellsCheckBox is null || ShowBondsCheckBox is null ||
            ShowGridCheckBox is null || ShowTrailsCheckBox is null)
        {
            return;
        }

        SandboxControl.World.ShowHud = ShowHudCheckBox.IsChecked == true;
        SandboxControl.World.ShowShells = ShowShellsCheckBox.IsChecked == true;
        SandboxControl.World.ShowBonds = ShowBondsCheckBox.IsChecked == true;
        SandboxControl.World.ShowGrid = ShowGridCheckBox.IsChecked == true;
        SandboxControl.World.ShowTrails = ShowTrailsCheckBox.IsChecked == true;
    }

    private ElementTemplate GetSelectedTemplate()
    {
        return ElementComboBox.SelectedItem as ElementTemplate ?? ElementCatalog.All[0];
    }

    private void AddEventLine(string text)
    {
        EventListBox.Items.Insert(0, text);
        while (EventListBox.Items.Count > 20)
        {
            EventListBox.Items.RemoveAt(EventListBox.Items.Count - 1);
        }
    }
}
