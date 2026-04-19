using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NewtonsCradleStudio;

public partial class MainWindow : Window
{
    private WindowStyle _savedWindowStyle;
    private ResizeMode _savedResizeMode;
    private WindowState _savedWindowState;
    private bool _isFullscreen;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => SyncUiToSimulation();
    }

    private void SyncUiToSimulation()
    {
        Cradle.SetIdealTransfer(IdealModeCheckBox.IsChecked == true);
        Cradle.SetLoss(LossSlider.Value);
        Cradle.SetCount((int)CountSlider.Value);
        Cradle.SetTimeScale(TimeScaleSlider.Value);
    }

    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        Cradle.Reset();
    }

    private void PauseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Cradle.TogglePause();
        PauseButton.Content = Cradle.IsPaused ? "Resume" : "Pause";
    }

    private void FullscreenButton_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void IdealModeCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        Cradle.SetIdealTransfer(IdealModeCheckBox.IsChecked == true);
    }

    private void LossSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        Cradle.SetLoss(LossSlider.Value);
    }

    private void CountSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        Cradle.SetCount((int)CountSlider.Value);
    }

    private void TimeScaleSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        Cradle.SetTimeScale(TimeScaleSlider.Value);
    }

    private void Window_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _isFullscreen)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _savedWindowStyle = WindowStyle;
            _savedResizeMode = ResizeMode;
            _savedWindowState = WindowState;

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Topmost = true;
            _isFullscreen = true;
            FullscreenButton.Content = "Windowed";
            return;
        }

        Topmost = false;
        WindowStyle = _savedWindowStyle;
        ResizeMode = _savedResizeMode;
        WindowState = _savedWindowState;
        _isFullscreen = false;
        FullscreenButton.Content = "Fullscreen";
    }
}
