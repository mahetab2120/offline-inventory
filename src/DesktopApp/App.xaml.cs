using System;
using System.IO;
using System.Windows;

namespace DesktopApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (s, args) =>
        {
            string err = args.Exception.InnerException != null 
                ? $"{args.Exception.Message}\n\nDetails: {args.Exception.InnerException.Message}" 
                : args.Exception.Message;

            try
            {
                File.AppendAllText("desktop_error.log", $"[{DateTime.UtcNow:u}] {args.Exception}\n\n");
            }
            catch { }

            MessageBox.Show($"Application Notice:\n{err}", "AFS Desktop Application", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                try
                {
                    File.AppendAllText("desktop_error.log", $"[{DateTime.UtcNow:u}] CRITICAL: {ex}\n\n");
                }
                catch { }

                MessageBox.Show($"System Notice:\n{ex.Message}", "AFS Desktop Application", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }
}
