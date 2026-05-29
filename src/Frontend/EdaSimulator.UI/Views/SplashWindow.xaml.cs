using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using EdaSimulator.Engines.Licensing;

namespace EdaSimulator.UI.Views
{
    public partial class SplashWindow : Window
    {
        private DispatcherTimer _timer;
        private int _progressValue = 0;

        public SplashWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize Licensing
            EdaSimulator.Engines.Licensing.LicenseManager.Initialize();
            
            EditionText.Text = $"{EdaSimulator.Engines.Licensing.LicenseManager.CurrentLicense.Tier} Edition";
            
            // Start loading sequence
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(20);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _progressValue += 2;
            LoadProgress.Value = _progressValue;

            if (_progressValue == 20) LoadingStatus.Text = "Loading SPICE models...";
            else if (_progressValue == 40) LoadingStatus.Text = "Initializing Python Scripting Engine...";
            else if (_progressValue == 60) LoadingStatus.Text = "Configuring Math & Matrix Solvers...";
            else if (_progressValue == 80) LoadingStatus.Text = "Starting UI Framework...";

            if (_progressValue >= 100)
            {
                _timer.Stop();
                OpenMainWindow();
            }
        }

        private void OpenMainWindow()
        {
            var mainWindow = new MainWindow();

            // Apply license tier to the window title
            if (mainWindow.DataContext is ViewModels.MainViewModel vm)
            {
                string tier = EdaSimulator.Engines.Licensing.LicenseManager.CurrentLicense.Tier.ToString();
                vm.WindowTitle = $"EDA Simulator — {tier} Edition  [New Project]";
            }

            App.Current.MainWindow = mainWindow;
            mainWindow.Show();
            this.Close();
        }
    }
}
