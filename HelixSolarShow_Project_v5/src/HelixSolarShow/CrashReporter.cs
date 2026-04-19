using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

namespace HelixSolarShow;

internal static class CrashReporter
{
    private static readonly object Sync = new();
    private static string _logPath = Path.Combine(AppContext.BaseDirectory, "helix-show-log.txt");

    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(AppContext.BaseDirectory);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    ReportFatal("AppDomain.UnhandledException", ex, string.Empty);
            };
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                ReportFatal("TaskScheduler.UnobservedTaskException", e.Exception, string.Empty);
                e.SetObserved();
            };
        }
        catch
        {
        }
    }

    public static void LogInfo(string message)
    {
        lock (Sync)
        {
            File.AppendAllText(_logPath, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] INFO  {message}{Environment.NewLine}");
        }
    }

    public static void ReportFatal(string stage, Exception ex, string context)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("==============================================================================");
            sb.AppendLine($"Timestamp : {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}");
            sb.AppendLine($"Stage     : {stage}");
            sb.AppendLine($"Process   : {Process.GetCurrentProcess().MainModule?.FileName}");
            sb.AppendLine($"OS        : {Environment.OSVersion}");
            sb.AppendLine($"Framework : {Environment.Version}");
            sb.AppendLine($"64-bit    : {Environment.Is64BitProcess}");
            if (!string.IsNullOrWhiteSpace(context))
            {
                sb.AppendLine("Context   :");
                sb.AppendLine(context);
            }
            sb.AppendLine("Exception :");
            sb.AppendLine(ex.ToString());
            sb.AppendLine();
            lock (Sync)
            {
                File.AppendAllText(_logPath, sb.ToString());
            }
            MessageBox.Show(
                $"Helix Solar Show hit an error during {stage}.\n\nSee:\n{_logPath}",
                "Helix Solar Show Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
        }
    }
}
