using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using CatiaAIAssistant.Core.Errors;

namespace CatiaAIAssistant
{
    internal static class Program
    {
        private static AppServices? _services;

        [STAThread]
        static void Main()
        {
            // Spec §05.1: no unhandled exception may silently kill the app or lose diagnostics.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ReportFatal(e.Exception, terminating: false);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportFatal(e.ExceptionObject as Exception ?? new Exception(Convert.ToString(e.ExceptionObject)), e.IsTerminating);
            TaskScheduler.UnobservedTaskException += (_, e) => { ReportFatal(e.Exception, terminating: false); e.SetObserved(); };

            ApplicationConfiguration.Initialize();
            _services = AppComposition.Build();
            Application.Run(new Form1(_services));
        }

        private static void ReportFatal(Exception ex, bool terminating)
        {
            var err = ErrorTranslator.Translate(ex);
            try
            {
                if (_services != null)
                    _services.Logger.Failed("UnhandledException", err.UserMessage, err.TechnicalDetails, "System");
                else
                    File.AppendAllText(Path.Combine(Path.GetTempPath(), "CatiaAIAssistant_crash.log"), $"[{DateTime.Now:O}] {err.TechnicalDetails}{Environment.NewLine}");
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine("Failed to log unhandled exception: " + logEx);
            }

            try
            {
                MessageBox.Show(err.UserMessage + (terminating ? "\r\n\r\nبرنامه بسته خواهد شد." : "\r\n\r\nجزئیات فنی در گزارش نشست ثبت شد."),
                    "خطای پیش‌بینی‌نشده", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception uiEx)
            {
                System.Diagnostics.Debug.WriteLine("Failed to show error dialog: " + uiEx);
            }
        }
    }
}
