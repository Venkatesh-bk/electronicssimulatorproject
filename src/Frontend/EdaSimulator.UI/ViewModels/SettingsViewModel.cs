using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdaSimulator.Engines.Settings;
using Microsoft.Win32;

namespace EdaSimulator.UI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        // ── Appearance ──────────────────────────────────────────────────────────────
        [ObservableProperty] private bool _showGrid;
        [ObservableProperty] private bool _snapToGrid;
        [ObservableProperty] private double _canvasGridSize;
        [ObservableProperty] private string _selectedTheme = "DarkPro";

        // ── Simulation ───────────────────────────────────────────────────────────────
        [ObservableProperty] private string _ngSpicePath = string.Empty;
        [ObservableProperty] private int _simulationTimeoutSeconds;
        [ObservableProperty] private bool _autoRunDrc;
        [ObservableProperty] private string _defaultSimType = string.Empty;

        // ── File ─────────────────────────────────────────────────────────────────────
        [ObservableProperty] private bool _autoSaveEnabled;
        [ObservableProperty] private int _autoSaveIntervalMinutes;

        // ── PCB Autorouter ───────────────────────────────────────────────────────────
        [ObservableProperty] private string _freeRoutingJarPath = string.Empty;
        [ObservableProperty] private bool   _freeRoutingFound   = false;

        // ── Display ──────────────────────────────────────────────────────────────────
        [ObservableProperty] private double _wireThickness;
        [ObservableProperty] private string _wireColor = string.Empty;
        [ObservableProperty] private bool _showComponentValues;
        [ObservableProperty] private bool _showDesignators;

        public SettingsViewModel()
        {
            LoadFromSettings();
        }

        private void LoadFromSettings()
        {
            var s = SettingsManager.Instance.Current;
            ShowGrid = s.ShowGrid;
            SnapToGrid = s.SnapToGrid;
            CanvasGridSize = s.CanvasGridSize;
            SelectedTheme = s.Theme.ToString();
            NgSpicePath = s.NgSpicePath;
            SimulationTimeoutSeconds = s.SimulationTimeoutSeconds;
            AutoRunDrc = s.AutoRunDrcBeforeSimulation;
            DefaultSimType = s.DefaultSimulationType;
            AutoSaveEnabled = s.AutoSaveEnabled;
            AutoSaveIntervalMinutes = s.AutoSaveIntervalMinutes;
            WireThickness = s.WireThickness;
            WireColor = s.WireColor;
            ShowComponentValues = s.ShowComponentValues;
            ShowDesignators = s.ShowDesignators;
            FreeRoutingJarPath = s.FreeRoutingJarPath;
            FreeRoutingFound = EdaSimulator.Engines.PCB.FreeRoutingService.IsFreeRoutingAvailable();
        }

        [RelayCommand]
        private void Save()
        {
            var s = SettingsManager.Instance.Current;
            s.ShowGrid = ShowGrid;
            s.SnapToGrid = SnapToGrid;
            s.CanvasGridSize = CanvasGridSize;
            s.NgSpicePath = NgSpicePath;
            s.SimulationTimeoutSeconds = SimulationTimeoutSeconds;
            s.AutoRunDrcBeforeSimulation = AutoRunDrc;
            s.DefaultSimulationType = DefaultSimType;
            s.AutoSaveEnabled = AutoSaveEnabled;
            s.AutoSaveIntervalMinutes = AutoSaveIntervalMinutes;
            s.WireThickness = WireThickness;
            s.WireColor = WireColor;
            s.ShowComponentValues = ShowComponentValues;
            s.ShowDesignators = ShowDesignators;
            s.FreeRoutingJarPath = FreeRoutingJarPath;

            if (System.Enum.TryParse<ApplicationTheme>(SelectedTheme, out var theme))
                s.Theme = theme;

            SettingsManager.Instance.Save();

            // Refresh FreeRouting availability indicator after saving JAR path
            FreeRoutingFound = EdaSimulator.Engines.PCB.FreeRoutingService.IsFreeRoutingAvailable();
        }

        [RelayCommand]
        private void BrowseFreeRoutingJar()
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Locate FreeRouting Executable JAR",
                Filter = "Java Archive (*.jar)|*.jar|All Files (*.*)|*.*",
                InitialDirectory = System.IO.Path.GetDirectoryName(FreeRoutingJarPath)
                                   .NullIfEmpty() ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)
            };
            if (dlg.ShowDialog() == true)
            {
                FreeRoutingJarPath = dlg.FileName;
                FreeRoutingFound   = System.IO.File.Exists(dlg.FileName);
            }
        }

        [RelayCommand]
        private void BrowseNgSpice()
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Locate ngspice Executable",
                Filter = "Executable (*.exe)|*.exe|All Files (*.*)|*.*",
                InitialDirectory = System.IO.Path.GetDirectoryName(NgSpicePath).NullIfEmpty()
                                   ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles)
            };
            if (dlg.ShowDialog() == true)
                NgSpicePath = dlg.FileName;
        }

        [RelayCommand]
        private void Reset()
        {
            if (MessageBox.Show("Reset all settings to defaults?", "Confirm Reset",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            SettingsManager.Instance.Reset();
            LoadFromSettings();
        }
    }

    internal static class StringExtensions
    {
        public static string? NullIfEmpty(this string? s)
            => string.IsNullOrEmpty(s) ? null : s;
    }
}
