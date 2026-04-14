using System.Windows;

namespace EdaSimulator.UI
{
    /// <summary>
    /// Interaction logic for App.xaml.
    /// Hosts global exception handling and application-level startup logic.
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global unhandled exception handler — logs instead of silently crashing
            DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nThe application will attempt to continue.",
                    "EdaSimulator — Unhandled Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                args.Handled = true; // Prevent crash; production builds should log to file instead
            };
        }
    }
}
