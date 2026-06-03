using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EdaSimulator.Engines.Library
{
    public class ComponentDatabase
    {
        public DatabaseMetadata Metadata { get; set; } = new DatabaseMetadata();
        public List<LibraryComponent> Components { get; set; } = new List<LibraryComponent>();
    }

    public class DatabaseMetadata
    {
        public string Version { get; set; } = string.Empty;
        public int TotalComponents { get; set; }
        public string GeneratedAt { get; set; } = string.Empty;
    }

    public class LibraryComponent
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Pins { get; set; }
        public string SpiceModel { get; set; } = string.Empty;
        public bool IsCustomIoT { get; set; }
        public double CadWidth { get; set; } = 5.0; // in mm
        public double CadHeight { get; set; } = 5.0; // in mm
        public double CadDepth { get; set; } = 3.0; // in mm
        public string CadColor { get; set; } = "#1E3A5A"; // hex color
        public string CadShape { get; set; } = "Box"; // "Box", "Cylinder", "DIP", "TO220"
        public string PinMappings { get; set; } = string.Empty; // e.g. "1,2,3" or "IN-,IN+,V+,V-"
    }

    /// <summary>
    /// Thread-safe singleton service for loading and querying the master component database.
    /// </summary>
    public class ComponentLibraryService
    {
        private static ComponentLibraryService? _instance;
        private static readonly object _instanceLock = new object();
        
        public static ComponentLibraryService Instance
        {
            get
            {
                if (_instance == null)
                    lock (_instanceLock)
                        _instance ??= new ComponentLibraryService();
                return _instance;
            }
        }

        private readonly List<LibraryComponent> _components = new();
        private readonly object _loadLock = new object();
        private volatile bool _isLoaded;

        private ComponentLibraryService() { }

        public void LoadDatabase()
        {
            if (_isLoaded) return;

            lock (_loadLock)
            {
                if (_isLoaded) return; // Double-checked locking pattern

                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MasterComponentDatabase.json");

                // Fallback: walk up to the source tree for development environments
                if (!File.Exists(dbPath))
                {
                    string candidate = Path.GetFullPath(Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "..", "..", "..", "..", "Engines",
                        "EdaSimulator.Engines", "Library", "MasterComponentDatabase.json"));
                    if (File.Exists(candidate)) dbPath = candidate;
                }

                if (!File.Exists(dbPath))
                {
                    // Graceful degradation: empty library rather than crashing the app
                    _isLoaded = true;
                    return;
                }

                string json = File.ReadAllText(dbPath);
                var db = JsonSerializer.Deserialize<ComponentDatabase>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (db?.Components != null)
                {
                    _components.Clear();
                    _components.AddRange(db.Components);
                }

                _isLoaded = true;
            }
        }

        public IReadOnlyList<LibraryComponent> GetAllComponents()
        {
            if (!_isLoaded) LoadDatabase();
            return _components;
        }

        public IEnumerable<LibraryComponent> GetCustomIoTDevices()
            => GetAllComponents().Where(c => c.IsCustomIoT);

        public IEnumerable<LibraryComponent> SearchComponents(string query)
        {
            var all = GetAllComponents();
            if (string.IsNullOrWhiteSpace(query)) return all;

            query = query.ToLowerInvariant();
            return all.Where(c =>
                c.Name.ToLowerInvariant().Contains(query) ||
                c.Category.ToLowerInvariant().Contains(query) ||
                c.Description.ToLowerInvariant().Contains(query) ||
                c.Manufacturer.ToLowerInvariant().Contains(query));
        }

        public LibraryComponent? GetComponentById(string id)
            => GetAllComponents().FirstOrDefault(c => c.Id == id);

        public void AddComponent(LibraryComponent component)
        {
            if (!_isLoaded) LoadDatabase();

            lock (_loadLock)
            {
                var existing = _components.FirstOrDefault(c => string.Equals(c.Id, component.Id, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    _components.Remove(existing);
                }
                _components.Add(component);

                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MasterComponentDatabase.json");
                if (!File.Exists(dbPath))
                {
                    string candidate = Path.GetFullPath(Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "..", "..", "..", "..", "Engines",
                        "EdaSimulator.Engines", "Library", "MasterComponentDatabase.json"));
                    if (File.Exists(candidate)) dbPath = candidate;
                }

                try
                {
                    var db = new ComponentDatabase
                    {
                        Metadata = new DatabaseMetadata
                        {
                            Version = "1.1",
                            TotalComponents = _components.Count,
                            GeneratedAt = DateTime.UtcNow.ToString("o")
                        },
                        Components = _components
                    };

                    string json = JsonSerializer.Serialize(db, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dbPath, json);
                }
                catch (Exception)
                {
                    // Degrade gracefully if path is write-protected
                }
            }
        }

        /// <summary>Resets the singleton for testing purposes.</summary>
        internal static void ResetForTesting()
        {
            lock (_instanceLock ?? new object())
            {
                _instance = null;
            }
        }
    }
}
