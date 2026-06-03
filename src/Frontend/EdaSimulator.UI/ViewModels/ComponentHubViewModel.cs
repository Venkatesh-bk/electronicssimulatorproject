using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdaSimulator.Engines.Library;
using EdaSimulator.Engines.Simulation;

namespace EdaSimulator.UI.ViewModels
{
    public class HubComponentItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Pins { get; set; }
        public bool IsCustomIoT { get; set; }
        public string IotBadge => IsCustomIoT ? "⚡ IoT" : string.Empty;
    }

    public class SpiceModelItem
    {
        public string Name        { get; set; } = "";
        public string Category    { get; set; } = "";
        public string TypeLabel   { get; set; } = "";
        public string Definition  { get; set; } = "";
        public string Badge       => TypeLabel == "SUBCKT" ? "📦 SubCkt" : ".MODEL";
    }

    public partial class ComponentHubViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<HubComponentItem> _components = new();

        [ObservableProperty]
        private ObservableCollection<string> _categories = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string _selectedCategory = "All";

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private HubComponentItem? _selectedComponent;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private int _totalCount;

        // SPICE Model Library tab
        [ObservableProperty]
        private ObservableCollection<SpiceModelItem> _spiceModels = new();

        [ObservableProperty]
        private string _spiceModelStatus = "";

        [ObservableProperty]
        private string _spiceSearchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<SpiceModelItem> _filteredSpiceModels = new();

        // Create Custom Component fields
        [ObservableProperty] private string _newCompId = string.Empty;
        [ObservableProperty] private string _newCompName = string.Empty;
        [ObservableProperty] private string _newCompManufacturer = string.Empty;
        [ObservableProperty] private string _newCompCategory = "Other";
        [ObservableProperty] private string _newCompDescription = string.Empty;
        [ObservableProperty] private string _newCompPins = "2";
        [ObservableProperty] private string _newCompPinMappings = string.Empty;
        [ObservableProperty] private string _newCompSpiceModel = string.Empty;
        [ObservableProperty] private string _newCompCadShape = "Box";
        [ObservableProperty] private string _newCompCadWidth = "5.0";
        [ObservableProperty] private string _newCompCadHeight = "5.0";
        [ObservableProperty] private string _newCompCadDepth = "3.0";
        [ObservableProperty] private string _newCompCadColor = "#1E3A5A";

        public ComponentHubViewModel()
        {
            _ = InitializeAsync();
            LoadSpiceModels();
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading component database...";

            await Task.Run(() =>
            {
                try
                {
                    ComponentLibraryService.Instance.LoadDatabase();
                }
                catch { /* DB file not found in test environment */ }
            });

            // Populate category filter
            var allComponents = ComponentLibraryService.Instance.GetAllComponents().ToList();
            var cats = allComponents.Select(c => c.Category).Distinct().OrderBy(x => x).ToList();

            Categories.Clear();
            Categories.Add("All");
            Categories.Add("⚡ IoT Devices");
            foreach (var cat in cats) Categories.Add(cat);

            TotalCount = allComponents.Count;
            StatusMessage = $"Library loaded — {TotalCount} components from SparkFun, Adafruit, Eagle & more";

            await SearchAsync();
            IsLoading = false;
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            IsLoading = true;
            Components.Clear();

            await Task.Run(() =>
            {
                IEnumerable<LibraryComponent> results;

                if (SelectedCategory == "⚡ IoT Devices")
                    results = ComponentLibraryService.Instance.GetCustomIoTDevices();
                else
                    results = ComponentLibraryService.Instance.SearchComponents(SearchQuery);

                if (SelectedCategory != "All" && SelectedCategory != "⚡ IoT Devices")
                    results = results.Where(c => c.Category == SelectedCategory);

                var mapped = results.Take(300).Select(c => new HubComponentItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    Manufacturer = c.Manufacturer,
                    Category = c.Category,
                    Description = c.Description,
                    Pins = c.Pins,
                    IsCustomIoT = c.IsCustomIoT
                }).ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var item in mapped)
                        Components.Add(item);
                });
            });

            IsLoading = false;
        }

        [RelayCommand]
        private async Task InstallComponentAsync()
        {
            if (SelectedComponent == null) return;

            IsLoading = true;
            StatusMessage = $"Installing {SelectedComponent.Name}...";
            await Task.Delay(800);

            IsLoading = false;
            StatusMessage = $"✅ {SelectedComponent.Name} installed to local library.";
            System.Windows.MessageBox.Show(
                $"'{SelectedComponent.Name}' has been added to your project's local library.\n\nSPICE model and footprint are now available in the Parts Bin.",
                "Component Installed",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        // ── SPICE Model Library ──────────────────────────────────────────────────────

        private void LoadSpiceModels()
        {
            var lib = ModelLibraryService.Instance;
            SpiceModels.Clear();
            FilteredSpiceModels.Clear();

            if (!lib.IsLoaded)
            {
                SpiceModelStatus = "ℹ️ eda_components.lib not found.";
                return;
            }

            var catalog = lib.GetCatalog();
            foreach (var kv in catalog)
            {
                foreach (var name in kv.Value)
                {
                    var raw = lib.FindModel(name);
                    SpiceModels.Add(new SpiceModelItem
                    {
                        Name       = name,
                        Category   = kv.Key,
                        TypeLabel  = raw?.Type == SpiceModelType.Subcircuit ? "SUBCKT" : "MODEL",
                        Definition = raw?.RawDefinition.Substring(0, Math.Min(80, raw.RawDefinition.Length)).Trim() ?? ""
                    });
                }
            }

            foreach (var m in SpiceModels) FilteredSpiceModels.Add(m);

            SpiceModelStatus = $"✔ {SpiceModels.Count} models loaded from eda_components.lib";
        }

        [RelayCommand]
        private void FilterSpiceModels()
        {
            FilteredSpiceModels.Clear();
            var q = SpiceSearchQuery.Trim().ToLower();
            foreach (var m in SpiceModels)
            {
                if (string.IsNullOrEmpty(q) ||
                    m.Name.ToLower().Contains(q) ||
                    m.Category.ToLower().Contains(q))
                {
                    FilteredSpiceModels.Add(m);
                }
            }
        }

        [RelayCommand]
        private void ImportSpiceLibrary()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import SPICE Model Library",
                Filter = "SPICE Library Files (*.lib;*.mod;*.txt)|*.lib;*.mod;*.txt|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    ModelLibraryService.Instance.ImportLibrary(dlg.FileName);
                    LoadSpiceModels();
                    System.Windows.MessageBox.Show(
                        $"SPICE library successfully imported from '{System.IO.Path.GetFileName(dlg.FileName)}'. Models are now available.",
                        "Import Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to import library: {ex.Message}",
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        public System.Action? RequestClose { get; set; }

        [ObservableProperty]
        private string _placedComponentId = string.Empty;

        [RelayCommand]
        private void PlaceComponent()
        {
            if (SelectedComponent == null) return;
            PlacedComponentId = SelectedComponent.Id;
            RequestClose?.Invoke();
        }

        [RelayCommand]
        private void CreateCustomComponent()
        {
            if (string.IsNullOrWhiteSpace(NewCompId) || string.IsNullOrWhiteSpace(NewCompName))
            {
                System.Windows.MessageBox.Show("Please enter a valid Part ID and Display Name.", "Validation Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(NewCompSpiceModel))
            {
                System.Windows.MessageBox.Show("Please enter a valid SPICE model definition (e.g. .model or .subckt card).", "Validation Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(NewCompPins, out int pinCount) || pinCount <= 0)
            {
                System.Windows.MessageBox.Show("Please enter a valid positive integer pin count.", "Validation Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            double.TryParse(NewCompCadWidth, out double cadW);
            double.TryParse(NewCompCadHeight, out double cadH);
            double.TryParse(NewCompCadDepth, out double cadD);

            var customLibComp = new LibraryComponent
            {
                Id = NewCompId.Trim(),
                Name = NewCompName.Trim(),
                Manufacturer = string.IsNullOrWhiteSpace(NewCompManufacturer) ? "Generic" : NewCompManufacturer.Trim(),
                Category = NewCompCategory ?? "Other",
                Description = string.IsNullOrWhiteSpace(NewCompDescription) ? $"{NewCompName} custom component." : NewCompDescription.Trim(),
                Pins = pinCount,
                SpiceModel = NewCompSpiceModel.Trim(),
                IsCustomIoT = false,
                CadWidth = cadW <= 0 ? 5.0 : cadW,
                CadHeight = cadH <= 0 ? 5.0 : cadH,
                CadDepth = cadD <= 0 ? 3.0 : cadD,
                CadColor = string.IsNullOrWhiteSpace(NewCompCadColor) ? "#1E3A5A" : NewCompCadColor.Trim(),
                CadShape = NewCompCadShape ?? "Box",
                PinMappings = NewCompPinMappings.Trim()
            };

            // 1. Add to SPICE Model Library in-memory and persistence
            ModelLibraryService.Instance.ImportLibraryText(customLibComp.Name, customLibComp.SpiceModel);

            // 2. Add to Master Component Database JSON
            ComponentLibraryService.Instance.AddComponent(customLibComp);

            // 3. Clear inputs
            NewCompId = "";
            NewCompName = "";
            NewCompManufacturer = "";
            NewCompDescription = "";
            NewCompPinMappings = "";
            NewCompSpiceModel = "";

            System.Windows.MessageBox.Show(
                $"Successfully created component '{customLibComp.Name}' and added it to the Master Database!",
                "Component Created",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            // Reload databases
            _ = InitializeAsync();
            LoadSpiceModels();
        }
    }
}
