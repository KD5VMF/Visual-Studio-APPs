using System;
using System.IO;
using System.Windows.Forms;

namespace AdGuardLiveWatch
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                ApplicationConfiguration.Initialize();
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

                Application.ThreadException += (_, e) => WriteFatal(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                {
                    if (e.ExceptionObject is Exception ex)
                        WriteFatal(ex);
                };

                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                WriteFatal(ex);
                MessageBox.Show(ex.ToString(),
                    "AGWatch REV12 fatal error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void WriteFatal(Exception ex)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AGWatch");

                Directory.CreateDirectory(dir);
                File.WriteAllText(
                    Path.Combine(dir, "fatal_error_REV12.txt"),
                    DateTime.Now + Environment.NewLine + ex);
            }
            catch
            {
                // Do not crash while trying to write crash report.
            }
        }
    }
}
