using System;
using System.IO;
using System.Text.Json;

namespace EdaSimulator.Engines.Settings
{
    public enum ApplicationTheme { DarkPro, DarkBlue, LightPro }

    public class AppSettings
    {
        // === Appearance ===
        public ApplicationTheme Theme { get; set; } = ApplicationTheme.DarkPro;
        public double CanvasGridSize { get; set; } = 20.0;
        public bool ShowGrid { get; set; } = true;
        public bool SnapToGrid { get; set; } = true;

        // === Simulation ===
        public string NgSpicePath { get; set; } = @"D:\electronicssimulatorproject\resources\engines\ngspice\Spice64\bin\ngspice_con.exe";
        public int SimulationTimeoutSeconds { get; set; } = 60;
        public bool AutoRunDrcBeforeSimulation { get; set; } = true;
        public string DefaultSimulationType { get; set; } = "Transient";

        // === PCB Autorouter ===
        /// <summary>
        /// Full path to the FreeRouting executable JAR.
        /// Download from: https://github.com/freerouting/freerouting/releases
        /// </summary>
        public string FreeRoutingJarPath { get; set; } = string.Empty;

        // === File & Project ===
        public string LastProjectDirectory { get; set; } = string.Empty;
        public bool AutoSaveEnabled { get; set; } = true;
        public int AutoSaveIntervalMinutes { get; set; } = 5;

        // === Display ===
        public double WireThickness { get; set; } = 1.5;
        public string WireColor { get; set; } = "#FF00CC00";
        public string ComponentColor { get; set; } = "#FF00AAFF";
        public bool ShowComponentValues { get; set; } = true;
        public bool ShowDesignators { get; set; } = true;
    }

    /// <summary>
    /// Thread-safe singleton for persistent application settings stored in AppData.
    /// </summary>
    public class SettingsManager
    {
        private static SettingsManager? _instance;
        private static readonly object _lock = new();
        public static SettingsManager Instance
        {
            get
            {
                lock (_lock) return _instance ??= new SettingsManager();
            }
        }

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EdaSimulator", "settings.json");

        public AppSettings Current { get; private set; } = new AppSettings();

        private SettingsManager() => Load();

        public void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    Current = JsonSerializer.Deserialize<AppSettings>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new AppSettings();
                }
            }
            catch { Current = new AppSettings(); }
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(SettingsPath,
                    JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* Ignore — settings are best-effort */ }
        }

        public void Reset() { Current = new AppSettings(); Save(); }
    }
}
