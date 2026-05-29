using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EdaSimulator.Engines.Simulation
{
    /// <summary>
    /// Singleton that manages the bundled SPICE model library.
    /// Provides:
    ///   1. The full path to eda_components.lib (for netlist .include directives)
    ///   2. A parsed catalog of all available models for the UI library browser
    ///   3. Model lookup by name (for component property validation)
    /// </summary>
    public sealed class ModelLibraryService
    {
        private static readonly Lazy<ModelLibraryService> _instance =
            new(() => new ModelLibraryService());

        public static ModelLibraryService Instance => _instance.Value;

        private readonly List<SpiceLibraryModel> _models = new();
        private string? _libraryFilePath;

        /// <summary>All parsed models from the bundled library.</summary>
        public IReadOnlyList<SpiceLibraryModel> Models => _models;

        /// <summary>Full path to the .lib file, or null if not found.</summary>
        public string? LibraryFilePath => _libraryFilePath;

        /// <summary>True when the library was located and successfully loaded.</summary>
        public bool IsLoaded => _libraryFilePath != null && _models.Count > 0;

        private ModelLibraryService()
        {
            _libraryFilePath = FindLibraryFile();
            if (_libraryFilePath != null)
            {
                try
                {
                    var parser = new SpiceLibParser();
                    _models.AddRange(parser.ParseLibrary(_libraryFilePath));
                }
                catch
                {
                    // Library load is optional — simulation still works without it
                }
            }
        }

        /// <summary>
        /// Returns the .model or .subckt definition for the given model name.
        /// Returns null if not found.
        /// </summary>
        public SpiceLibraryModel? FindModel(string modelName)
        {
            return _models.FirstOrDefault(m =>
                string.Equals(m.Name, modelName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns all model names grouped by type for the UI library browser.
        /// Key = "Diodes" | "BJT NPN" | "BJT PNP" | "MOSFET N" | "MOSFET P" | "Op-Amps" | "Regulators" | "Other"
        /// </summary>
        public Dictionary<string, List<string>> GetCatalog()
        {
            var catalog = new Dictionary<string, List<string>>
            {
                ["Diodes"]      = new(),
                ["BJT NPN"]     = new(),
                ["BJT PNP"]     = new(),
                ["MOSFET-N"]    = new(),
                ["MOSFET-P"]    = new(),
                ["Op-Amps"]     = new(),
                ["Regulators"]  = new(),
                ["Other"]       = new(),
            };

            foreach (var m in _models)
            {
                string raw = m.RawDefinition.ToUpperInvariant();
                string name = m.Name;

                if (raw.Contains(".MODEL") && raw.Contains(" D("))
                    catalog["Diodes"].Add(name);
                else if (raw.Contains(".MODEL") && raw.Contains(" NPN("))
                    catalog["BJT NPN"].Add(name);
                else if (raw.Contains(".MODEL") && raw.Contains(" PNP("))
                    catalog["BJT PNP"].Add(name);
                else if (raw.Contains(".MODEL") && raw.Contains(" NMOS("))
                    catalog["MOSFET-N"].Add(name);
                else if (raw.Contains(".MODEL") && raw.Contains(" PMOS("))
                    catalog["MOSFET-P"].Add(name);
                else if (name.StartsWith("LM") || name.StartsWith("TL") || name.StartsWith("NE") ||
                         name.StartsWith("OP") || name.StartsWith("UA"))
                    catalog["Op-Amps"].Add(name);
                else if (name.StartsWith("LM78") || name.StartsWith("LM79") || name.StartsWith("LM317") ||
                         name.Contains("REG"))
                    catalog["Regulators"].Add(name);
                else
                    catalog["Other"].Add(name);
            }

            return catalog;
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Discovery — walks from the executable back to the project root
        // ────────────────────────────────────────────────────────────────────────────

        private static string? FindLibraryFile()
        {
            // Priority 1: Next to executable (deployed/published mode)
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            string candidate = Path.Combine(exeDir, "eda_components.lib");
            if (File.Exists(candidate)) return candidate;

            // Priority 2: Walk up from exe to find resources/spice_models/
            string? dir = exeDir;
            for (int i = 0; i < 8; i++)
            {
                if (dir == null) break;
                string probe = Path.Combine(dir, "resources", "spice_models", "eda_components.lib");
                if (File.Exists(probe)) return probe;
                dir = Path.GetDirectoryName(dir);
            }

            return null;
        }
    }
}
