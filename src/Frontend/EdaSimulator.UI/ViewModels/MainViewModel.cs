using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.Models.Components;
using EdaSimulator.Engines.Simulation;
using EdaSimulator.UI.ViewModels.Canvas;
using System.Windows;

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

        private string _lastExecutedNetlistHash = string.Empty;
        private System.Windows.Threading.DispatcherTimer _liveTunerTimer;

        private System.Threading.CancellationTokenSource? _simCancellationTokenSource;

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
        private void AddMockComponents()
        {
            // Reset the schematic each time so this command is safe to call multiple times
            ActiveSchematicViewModel = new SchematicViewModel(new Schematic("Mock Circuit"));
            var schVM = ActiveSchematicViewModel;
            var sch = schVM.CoreSchematic;

            try
            {
                var r1 = new Resistor("R1", "10k");
                var v1 = new VoltageSource("V1", "DC 5");  

                // Create Visual Wrappers and assign locations
                var r1Node = new ComponentNodeViewModel(r1) { X = 300, Y = 200 };
                var v1Node = new ComponentNodeViewModel(v1) { X = 100, Y = 200 };
                
                schVM.AddComponentNode(r1Node);
                schVM.AddComponentNode(v1Node);

                // Connect V1(+) → N001 ← R1(pin1) and V1(-) → GND ← R1(pin2)
                var net1 = sch.CreateNet("N001");
                sch.ConnectPinToNet(v1.GetPinByName("+"), net1.Id);
                sch.ConnectPinToNet(r1.GetPinByName("1"), net1.Id);

                sch.ConnectPinToNet(v1.GetPinByName("-"), sch.MasterGroundNet.Id);
                sch.ConnectPinToNet(r1.GetPinByName("2"), sch.MasterGroundNet.Id);

                MessageBox.Show("Successfully added mock V1 and R1 components to schematic.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
