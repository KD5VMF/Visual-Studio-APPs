using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace HelixSolarShow;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        CrashReporter.Initialize();
        CrashReporter.LogInfo("HelixSolarShow starting.");

        var gameSettings = GameWindowSettings.Default;
        gameSettings.UpdateFrequency = 120.0;

        var nativeSettings = new NativeWindowSettings
        {
            Title = "Helix Solar Show HD",
            ClientSize = new Vector2i(1920, 1080),
            StartVisible = true,
            WindowState = WindowState.Fullscreen,
            NumberOfSamples = 8,
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible
        };

        try
        {
            using var window = new HelixShowWindow(gameSettings, nativeSettings);
            window.Run();
            CrashReporter.LogInfo("HelixSolarShow exited normally.");
        }
        catch (Exception ex)
        {
            CrashReporter.ReportFatal("Program.Main", ex, string.Empty);
        }
    }
}
