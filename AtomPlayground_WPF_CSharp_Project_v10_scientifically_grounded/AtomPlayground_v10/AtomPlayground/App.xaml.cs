using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace AtomPlayground;

public partial class App : Application
{
    private static string LogPath => Path.Combine(AppContext.BaseDirectory, "startup-error.log");

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        try
        {
            var window = new MainWindow();
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
        }
        catch (Exception ex)
        {
            WriteException("Startup failure", ex);
            MessageBox.Show(
                $"Atom Playground failed during startup.\n\n{ex.Message}\n\nA log was written to:\n{LogPath}",
                "Atom Playground",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteException("Unhandled UI exception", e.Exception);
        MessageBox.Show(
            $"Atom Playground hit an unexpected error.\n\n{e.Exception.Message}\n\nA log was written to:\n{LogPath}",
            "Atom Playground",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(-1);
    }

    private static void WriteException(string caption, Exception ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {caption}");
            sb.AppendLine(ex.ToString());
            sb.AppendLine(new string('-', 80));
            File.AppendAllText(LogPath, sb.ToString());
        }
        catch
        {
        }
    }
}
