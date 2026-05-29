using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdaSimulator.Engines.Settings;

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

            if (System.Enum.TryParse<ApplicationTheme>(SelectedTheme, out var theme))
                s.Theme = theme;

            SettingsManager.Instance.Save();
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
}
