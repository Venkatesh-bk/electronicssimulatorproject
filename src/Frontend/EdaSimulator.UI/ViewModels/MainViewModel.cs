using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdaSimulator.Engines.IO;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.Models.Components;
using EdaSimulator.Engines.Simulation;
using EdaSimulator.UI.ViewModels.Canvas;
using System.Windows;
using Microsoft.Win32;

namespace EdaSimulator.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private SchematicViewModel _activeSchematicViewModel;

        [ObservableProperty]
        private string _netlistOutput;

        [ObservableProperty]
        private bool _isSimulating;

        [ObservableProperty]
        private bool _isLiveTuningEnabled;

        // ── Phase 7: Status Bar ─────────────────────────────────────────────────────
        [ObservableProperty]
        private string _statusText = "Ready  |  Press 'W' to wire  |  ESC to select";

        [ObservableProperty]
        private double _mouseCanvasX;

        [ObservableProperty]
        private double _mouseCanvasY;

        [ObservableProperty]
        private string _activeToolName = "Selection";

        // ── Phase 7: Save/Load ──────────────────────────────────────────────────────
        [ObservableProperty]
        private string _currentProjectPath = "";

        [ObservableProperty]
        private string _windowTitle = "EDA Simulator Platform - Professional  [New Project]";

        // ── Phase 7: Properties Panel ───────────────────────────────────────────────
        [ObservableProperty]
        private ComponentPropertiesViewModel _componentProperties = new();

        // ── Phase 7: Simulation Mode ────────────────────────────────────────────────
        [ObservableProperty]
        private string _simulationType = "Transient";

        [ObservableProperty]
        private string _acStartFreq = "1";

        [ObservableProperty]
        private string _acStopFreq = "10Meg";

        [ObservableProperty]
        private string _acPointsPerDecade = "100";

        [ObservableProperty]
        private string _transientStopTime = "10m";

        [ObservableProperty]
        private string _transientStepTime = "1u";


        [ObservableProperty]
        private string _pythonScriptCode = @"import cupy as cp
import time
import math

print('--- GPU-Accelerated Monte Carlo Yield Analysis ---')
print('Simulating 10,000,000 variations of an Active RC Filter...')
print('Nominal Values: R1=10k, R2=10k, C1=1nF, C2=1nF')
print('Tolerance: Resistors 5%, Capacitors 10%')

start = time.time()

# Number of Monte Carlo iterations
N = 10000000

# Generate normal distributions directly on the GPU
print('Allocating random variables on GPU...')
R1 = cp.random.normal(10000, 10000 * 0.05 / 3, N, dtype=cp.float32)
R2 = cp.random.normal(10000, 10000 * 0.05 / 3, N, dtype=cp.float32)
C1 = cp.random.normal(1e-9, 1e-9 * 0.10 / 3, N, dtype=cp.float32)
C2 = cp.random.normal(1e-9, 1e-9 * 0.10 / 3, N, dtype=cp.float32)

# Calculate Cutoff Frequency for all 10M circuits in parallel
# f_c = 1 / (2 * pi * sqrt(R1 * R2 * C1 * C2))
print('Calculating cutoff frequencies in parallel...')
fc = 1.0 / (2.0 * math.pi * cp.sqrt(R1 * R2 * C1 * C2))

# Target Cutoff: 15.9 kHz. Let's find yield % within +/- 5% of target
target_fc = 15915.5
lower_bound = target_fc * 0.95
upper_bound = target_fc * 1.05

# GPU boolean array evaluation
valid_circuits = (fc >= lower_bound) & (fc <= upper_bound)
yield_count = cp.sum(valid_circuits).item()
yield_percentage = (yield_count / N) * 100.0

cp.cuda.Stream.null.synchronize()
end = time.time()

print(f'\n--- Results ---')
print(f'Total Iterations: {N:,}')
print(f'Yield Rate: {yield_percentage:.2f}% ({yield_count:,} passed)')
print(f'Execution Time: {end - start:.4f} seconds')
print('SUCCESS: Massive parallel EDA computation executed on NVIDIA GPU.')
";

        [ObservableProperty]
        private string _pythonScriptOutput;

        private string _lastExecutedNetlistHash = string.Empty;
        private System.Windows.Threading.DispatcherTimer _liveTunerTimer;

        private System.Threading.CancellationTokenSource? _simCancellationTokenSource;

        // ── Phase 6: Physics Engine ViewModels ─────────────────────────────────────
        public PhysicsViewModel PhysicsVM { get; } = new PhysicsViewModel();

        // ── Phase 7: PCB Layout ViewModel ──────────────────────────────────────────
        public PcbLayoutViewModel PcbVM { get; } = new PcbLayoutViewModel();

        public MainViewModel()
        {
            // Initialize with an empty schematic
            ActiveSchematicViewModel = new SchematicViewModel(new Schematic("New Project"));
            NetlistOutput = "No netlist generated yet. Click 'Generate .cir' to export.";

            _liveTunerTimer = new System.Windows.Threading.DispatcherTimer();
            _liveTunerTimer.Interval = System.TimeSpan.FromSeconds(1);
            _liveTunerTimer.Tick += HandleLiveTuningTick;
        }

        partial void OnIsLiveTuningEnabledChanged(bool value)
        {
            if (value)
            {
                _liveTunerTimer.Start();
                // Ensure initial tracking logic catches up
                var exporter = new SpiceNetlistExporter();
                _lastExecutedNetlistHash = exporter.GenerateNetlist(ActiveSchematicViewModel.CoreSchematic);
            }
            else
            {
                _liveTunerTimer.Stop();
            }
        }

        private void HandleLiveTuningTick(object? sender, System.EventArgs e)
        {
            if (IsSimulating) return;

            var exporter = new SpiceNetlistExporter();
            var currentNetlist = exporter.GenerateNetlist(ActiveSchematicViewModel.CoreSchematic);
            
            if (currentNetlist != _lastExecutedNetlistHash)
            {
                _lastExecutedNetlistHash = currentNetlist;
                SimulateCommand.Execute(null);
            }
        }

        [RelayCommand]
        private void GenerateNetlist()
        {
            var drcResult = ActiveSchematicViewModel.RunDRC();
            
            if (!drcResult.isValid)
            {
                NetlistOutput = drcResult.logOutput + "Netlist generation aborted due to critical simulation physics errors.\nPlease connect the required pins and ground referenced nodes.";
                return;
            }

            var exporter = new SpiceNetlistExporter();
            var netlist = exporter.GenerateNetlist(ActiveSchematicViewModel.CoreSchematic);
            
            NetlistOutput = drcResult.logOutput + netlist;
        }
        [RelayCommand]
        private async System.Threading.Tasks.Task SimulateAsync()
        {
            if (IsSimulating) return; // Prevent concurrent spin-ups
            
            var drcResult = ActiveSchematicViewModel.RunDRC();
            if (!drcResult.isValid)
            {
                NetlistOutput = drcResult.logOutput + "Simulation aborted due to critical graph physics errors.";
                return;
            }

            var exporter = new SpiceNetlistExporter();
            var netlist = exporter.GenerateNetlist(ActiveSchematicViewModel.CoreSchematic);
            NetlistOutput = drcResult.logOutput + netlist;

            _lastExecutedNetlistHash = netlist; // Anchor the graph hash for Live Tuning

            IsSimulating = true;
            _simCancellationTokenSource = new System.Threading.CancellationTokenSource();

            var executionSvc = new SpiceExecutionService();
            EdaSimulator.Engines.Simulation.SpiceExecutionResult result;
            
            try
            {
                // Push the UI cancellation token completely down into the native OS Process Wrapper
                result = await executionSvc.RunSimulationAsync(netlist, _simCancellationTokenSource.Token);
            }
            finally
            {
                IsSimulating = false;
                _simCancellationTokenSource?.Dispose();
                _simCancellationTokenSource = null;
            }

            if (!result.Success)
            {
                NetlistOutput += $"\n\n--- SIMULATION FAILED ---\n{result.ErrorMessage}";
                return;
            }
            
            NetlistOutput += $"\n\n--- SIMULATION SUCCESS ---\n{result.OutputLog}";

            if (System.IO.File.Exists(result.RawFilePath))
            {
                var data = RawFileParser.Parse(result.RawFilePath);

                var scopeWindow = new Views.OscilloscopeWindow();
                scopeWindow.ViewModel.ClearTraces();

                // By default, SPICE time vector is usually var index 0 explicitly lowercase "time"
                if (!data.DataPoints.ContainsKey("time"))
                {
                    // Fallback to DC sweep or other axes later
                    return;
                }
                
                var timeAxis = data.DataPoints["time"];

                // Intersect targeted Probes
                var probes = System.Linq.Enumerable.OfType<VoltageProbeItemViewModel>(ActiveSchematicViewModel.Items);
                foreach (var probe in probes)
                {
                    // Ngspice labels variables compactly without spaces, e.g., v(net_id)
                    string traceKey = $"v({probe.TargetNetName})".ToLower();
                    
                    if (data.DataPoints.ContainsKey(traceKey))
                    {
                        scopeWindow.ViewModel.RenderTrace($"V(Net {probe.TargetNetName})", timeAxis, data.DataPoints[traceKey]);
                    }
                }
                
                scopeWindow.Show();
            }
        }

        [RelayCommand]
        private void StopSimulation()
        {
            if (IsSimulating && _simCancellationTokenSource != null)
            {
                NetlistOutput += "\n\n[USER ACTION] Triggering hardware simulation abort...";
                _simCancellationTokenSource.Cancel();
            }
        }

        [RelayCommand]
        private void RunPythonScript()
        {
            if (string.IsNullOrWhiteSpace(PythonScriptCode))
                return;

            try
            {
                PythonScriptOutput = EdaSimulator.Engines.Scripting.PythonEngineService.ExecuteScript(PythonScriptCode, ActiveSchematicViewModel.CoreSchematic);
            }
            catch (System.Exception ex)
            {
                PythonScriptOutput = $"[Engine Error] {ex.Message}";
            }
        }

        [RelayCommand]
        private void AddMockComponents()
        {
            // Reset the schematic each time so this command is safe to call multiple times
            ActiveSchematicViewModel = new SchematicViewModel(new Schematic("Sallen-Key Active Low-Pass Filter"));
            var schVM = ActiveSchematicViewModel;
            var sch = schVM.CoreSchematic;

            try
            {
                // Component Instantiation
                var u1 = new OpAmp("X1", "LM358");
                var r1 = new Resistor("R1", "10k");
                var r2 = new Resistor("R2", "10k");
                var c1 = new Capacitor("C1", "1n");
                var c2 = new Capacitor("C2", "1n");
                
                var vIn = new VoltageSource("V_IN", "SINE(0 5 15.9k)");
                var vcc = new VoltageSource("V_CC", "DC 15");
                var vee = new VoltageSource("V_EE", "DC -15");

                schVM.AddComponentNode(new ComponentNodeViewModel(vIn) { X = 100, Y = 250 });
                schVM.AddComponentNode(new ComponentNodeViewModel(r1) { X = 200, Y = 250 });
                schVM.AddComponentNode(new ComponentNodeViewModel(r2) { X = 300, Y = 250 });
                schVM.AddComponentNode(new ComponentNodeViewModel(c1) { X = 400, Y = 150 });
                schVM.AddComponentNode(new ComponentNodeViewModel(c2) { X = 400, Y = 350 });
                schVM.AddComponentNode(new ComponentNodeViewModel(u1) { X = 500, Y = 250 });
                schVM.AddComponentNode(new ComponentNodeViewModel(vcc) { X = 500, Y = 150 });
                schVM.AddComponentNode(new ComponentNodeViewModel(vee) { X = 500, Y = 350 });

                var netVin = sch.CreateNet("VIN_NODE");
                var netMid = sch.CreateNet("MID_NODE");
                var netP   = sch.CreateNet("POS_NODE");
                var netOut = sch.CreateNet("OUT_NODE");
                var netVcc = sch.CreateNet("VCC_NET");
                var netVee = sch.CreateNet("VEE_NET");

                sch.ConnectPinToNet(vIn.GetPinByName("+"), netVin.Id);
                sch.ConnectPinToNet(r1.GetPinByName("1"), netVin.Id);
                sch.ConnectPinToNet(r1.GetPinByName("2"), netMid.Id);
                sch.ConnectPinToNet(r2.GetPinByName("1"), netMid.Id);
                sch.ConnectPinToNet(c1.GetPinByName("1"), netMid.Id);
                sch.ConnectPinToNet(r2.GetPinByName("2"), netP.Id);
                sch.ConnectPinToNet(c2.GetPinByName("1"), netP.Id);
                sch.ConnectPinToNet(u1.GetPinByName("IN+"), netP.Id);
                sch.ConnectPinToNet(u1.GetPinByName("OUT"), netOut.Id);
                sch.ConnectPinToNet(c1.GetPinByName("2"), netOut.Id);
                sch.ConnectPinToNet(u1.GetPinByName("IN-"), netOut.Id);
                sch.ConnectPinToNet(vcc.GetPinByName("+"), netVcc.Id);
                sch.ConnectPinToNet(u1.GetPinByName("V+"), netVcc.Id);
                sch.ConnectPinToNet(vee.GetPinByName("-"), netVee.Id);
                sch.ConnectPinToNet(u1.GetPinByName("V-"), netVee.Id);
                sch.ConnectPinToNet(vIn.GetPinByName("-"), sch.MasterGroundNet.Id);
                sch.ConnectPinToNet(c2.GetPinByName("2"), sch.MasterGroundNet.Id);
                sch.ConnectPinToNet(vcc.GetPinByName("-"), sch.MasterGroundNet.Id);
                sch.ConnectPinToNet(vee.GetPinByName("+"), sch.MasterGroundNet.Id);

                StatusText = $"Sallen-Key LPF built  |  {sch.Components.Count} components  |  {sch.Nets.Count} nets";
                MessageBox.Show("Successfully generated International Standard Sallen-Key Active Low-Pass Filter.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Phase 7: Save / Load / New
        // ──────────────────────────────────────────────────────────────────────────────

        [RelayCommand]
        private void NewProject()
        {
            var result = MessageBox.Show("Create a new project? Unsaved changes will be lost.", "New Project",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            ActiveSchematicViewModel = new SchematicViewModel(new Schematic("New Project"));
            CurrentProjectPath = "";
            WindowTitle = "EDA Simulator Platform - Professional  [New Project]";
            StatusText = "New project created  |  0 components  |  0 nets";
            NetlistOutput = "No netlist generated yet.";
        }

        [RelayCommand]
        private void SaveProject()
        {
            if (string.IsNullOrEmpty(CurrentProjectPath))
            {
                SaveProjectAs();
                return;
            }
            PerformSave(CurrentProjectPath);
        }

        [RelayCommand]
        private void SaveProjectAs()
        {
            var dlg = new SaveFileDialog
            {
                Title            = "Save EDA Project",
                Filter           = "EDA Project (*.edaproj)|*.edaproj|All Files (*.*)|*.*",
                DefaultExt       = ".edaproj",
                FileName         = ActiveSchematicViewModel.CoreSchematic.Title
            };
            if (dlg.ShowDialog() == true)
            {
                PerformSave(dlg.FileName);
            }
        }

        private void PerformSave(string filePath)
        {
            try
            {
                var placements = ActiveSchematicViewModel.Items
                    .OfType<ComponentNodeViewModel>()
                    .Select(n => new ComponentPlacementRecord
                    {
                        Designator = n.CoreComponent.Designator,
                        X          = n.X,
                        Y          = n.Y
                    });

                var doc = ProjectFileService.ToDocument(
                    ActiveSchematicViewModel.CoreSchematic,
                    placements,
                    ActiveSchematicViewModel.CoreSchematic.Title);

                ProjectFileService.Save(doc, filePath);
                CurrentProjectPath = filePath;
                WindowTitle = $"EDA Simulator Platform - Professional  [{System.IO.Path.GetFileName(filePath)}]";
                StatusText = $"Project saved  |  {System.IO.Path.GetFileName(filePath)}";
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Failed to save project:\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void LoadProject()
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Open EDA Project",
                Filter = "EDA Project (*.edaproj)|*.edaproj|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var doc       = ProjectFileService.Load(dlg.FileName);
                var schematic = ProjectFileService.FromDocument(doc);
                ActiveSchematicViewModel = new SchematicViewModel(schematic);

                // Restore canvas positions from the placement records
                foreach (var placement in doc.Placements)
                {
                    var node = ActiveSchematicViewModel.Items
                        .OfType<ComponentNodeViewModel>()
                        .FirstOrDefault(n => n.CoreComponent.Designator == placement.Designator);
                    if (node != null)
                    {
                        node.X = placement.X;
                        node.Y = placement.Y;
                    }
                }

                CurrentProjectPath = dlg.FileName;
                WindowTitle = $"EDA Simulator Platform - Professional  [{System.IO.Path.GetFileName(dlg.FileName)}]";
                StatusText = $"Project loaded  |  {schematic.Components.Count} components  |  {schematic.Nets.Count} nets";
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Failed to load project:\n{ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ExportNetlistToFile()
        {
            var dlg = new SaveFileDialog
            {
                Title  = "Export SPICE Netlist",
                Filter = "SPICE Netlist (*.cir)|*.cir|Circuit (*.net)|*.net|All Files (*.*)|*.*",
                DefaultExt = ".cir"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var exporter  = new SpiceNetlistExporter();
                var directive = BuildSimDirective();
                var netlist   = exporter.GenerateNetlist(ActiveSchematicViewModel.CoreSchematic, directive);
                System.IO.File.WriteAllText(dlg.FileName, netlist);
                StatusText = $"Netlist exported  |  {System.IO.Path.GetFileName(dlg.FileName)}";
                MessageBox.Show($"Netlist exported to:\n{dlg.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Phase 7: Simulation Type Support (Transient / AC / DC)
        // ──────────────────────────────────────────────────────────────────────────────

        private string BuildSimDirective()
        {
            return SimulationType switch
            {
                "AC Sweep"  => $".ac dec {AcPointsPerDecade} {AcStartFreq} {AcStopFreq}",
                "DC Sweep"  => ".dc V1 0 5 0.1",
                _           => $".tran {TransientStepTime} {TransientStopTime}"
            };
        }

        // Override GenerateNetlist so it uses the current sim type
        [RelayCommand]
        private void GenerateNetlistWithMode()
        {
            var drcResult = ActiveSchematicViewModel.RunDRC();
            var exporter  = new SpiceNetlistExporter();
            var directive = BuildSimDirective();
            NetlistOutput = drcResult.logOutput + exporter.GenerateNetlist(ActiveSchematicViewModel.CoreSchematic, directive);
            StatusText    = $"Netlist generated  |  Mode: {SimulationType}";
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Phase 7: Update Status when selection changes
        // ──────────────────────────────────────────────────────────────────────────────

        public void OnComponentSelected(ComponentNodeViewModel? node)
        {
            if (node == null)
            {
                ComponentProperties.Clear();
                StatusText = $"Ready  |  {ActiveSchematicViewModel.CoreSchematic.Components.Count} components  |  {ActiveSchematicViewModel.CoreSchematic.Nets.Count} nets";
            }
            else
            {
                ComponentProperties.Populate(node.CoreComponent);
                StatusText = $"Selected: {node.CoreComponent.Designator} ({node.CoreComponent.GetType().Name})  |  Value: {node.CoreComponent.Value}";
            }
        }
    }
}

