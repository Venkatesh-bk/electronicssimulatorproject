using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdaSimulator.Engines.IO;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.Models.Components;
using EdaSimulator.Engines.Simulation;
using EdaSimulator.Engines.PCB;
using EdaSimulator.UI.ViewModels.Canvas;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Collections.ObjectModel;
using System.Linq;
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
        private string _serialMonitorOutput = "Serial Monitor ready. Load a firmware file and run transient simulation.";

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

        [ObservableProperty]
        private bool _isDrcValid = true;

        [ObservableProperty]
        private string _drcStatusMessage = "DRC: OK";

        [ObservableProperty]
        private bool _showDcBiasOverlay = true;

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

        // DC Sweep parameters
        [ObservableProperty]
        private string _dcSweepComponent = "V1";

        [ObservableProperty]
        private string _dcSweepStart = "0";

        [ObservableProperty]
        private string _dcSweepStop = "5";

        [ObservableProperty]
        private string _dcSweepStep = "0.1";

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
        private string _pythonScriptOutput = string.Empty;

        private string _lastExecutedNetlistHash = string.Empty;
        private System.Windows.Threading.DispatcherTimer _liveTunerTimer;
        private System.Windows.Threading.DispatcherTimer _autoSaveTimer;

        private System.Threading.CancellationTokenSource? _simCancellationTokenSource;

        // ── Phase 6: Physics Engine ViewModels ─────────────────────────────────────
        public PhysicsViewModel PhysicsVM { get; } = new PhysicsViewModel();

        // ── Phase 7: PCB Layout ViewModel ──────────────────────────────────────────
        public PcbLayoutViewModel PcbVM { get; } = new PcbLayoutViewModel();

        // ── Phase 2: Multi-sheet Schematics ──────────────────────────────────────
        public ObservableCollection<SchematicViewModel> SchematicSheets { get; } = new();

        [ObservableProperty]
        private SchematicViewModel? _selectedSchematicSheet;

        [RelayCommand]
        private void AddSchematicSheet()
        {
            int sheetNum = SchematicSheets.Count + 1;
            var newSchematic = new Schematic($"Sheet_{sheetNum}");
            var newVm = new SchematicViewModel(newSchematic);
            
            // Add a default GND reference component to the new sheet to make it simulation-ready
            var gnd = new GroundSymbol($"GND_{sheetNum}");
            var gndNode = new ComponentNodeViewModel(gnd) { X = 100, Y = 100 };
            newVm.Items.Add(gndNode);
            newSchematic.AddComponent(gnd);

            SchematicSheets.Add(newVm);
            SelectedSchematicSheet = newVm;
            StatusText = $"Added new schematic sheet: Sheet_{sheetNum}";
        }

        [RelayCommand]
        private void RemoveSchematicSheet(SchematicViewModel sheet)
        {
            if (SchematicSheets.Count <= 1)
            {
                MessageBox.Show("Cannot remove the last schematic sheet.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SchematicSheets.Remove(sheet);
            if (SelectedSchematicSheet == sheet)
            {
                SelectedSchematicSheet = SchematicSheets.FirstOrDefault();
            }
            StatusText = $"Removed sheet: {sheet.Title}";
        }

        partial void OnSelectedSchematicSheetChanged(SchematicViewModel? oldValue, SchematicViewModel? newValue)
        {
            if (newValue != null)
            {
                ActiveSchematicViewModel = newValue;
            }
        }

        // ── Phase 7: BOM & Supply Chain ───────────────────────────────────────────
        public ObservableCollection<BomLineItem> BomItems { get; } = new();

        [ObservableProperty]
        private double _bomTotalCost;

        [ObservableProperty]
        private string _bomSearchQuery = "";

        partial void OnBomSearchQueryChanged(string value)
        {
            UpdateBom();
        }

        [RelayCommand]
        public void UpdateBom()
        {
            BomItems.Clear();
            if (ActiveSchematicViewModel == null)
            {
                BomTotalCost = 0;
                return;
            }

            var fullBom = BomGenerator.GenerateBom(ActiveSchematicViewModel.CoreSchematic);
            
            var query = BomSearchQuery?.Trim().ToLower();
            var filtered = string.IsNullOrEmpty(query)
                ? fullBom
                : fullBom.Where(item => 
                    item.ComponentType.ToLower().Contains(query) ||
                    item.Value.ToLower().Contains(query) ||
                    item.PartNumber.ToLower().Contains(query) ||
                    item.Manufacturer.ToLower().Contains(query) ||
                    string.Join(" ", item.Designators).ToLower().Contains(query)
                ).ToList();

            foreach (var item in filtered)
            {
                BomItems.Add(item);
            }

            BomTotalCost = filtered.Sum(item => item.TotalPrice);
        }

        [RelayCommand]
        public void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Phase 3: Multimeter & Spectrum Analyzer ──────────────────────────────
        [ObservableProperty]
        private SpiceSimulationData? _lastSimulationData;

        public ObservableCollection<MultimeterReading> MultimeterReadings { get; } = new();

        [ObservableProperty]
        private PlotModel _spectraPlotModel = new()
        {
            Title = "FFT Spectrum Analyzer",
            Background = OxyColors.Transparent,
            TextColor = OxyColors.LightGray,
            PlotAreaBorderColor = OxyColor.FromArgb(80, 80, 80, 120)
        };

        public ObservableCollection<string> AvailableSpectraNodes { get; } = new();

        [ObservableProperty]
        private string? _selectedSpectraNode;

        [ObservableProperty]
        private string _selectedFftWindow = "Hanning";

        [ObservableProperty]
        private string _spectraStatus = "No simulation data. Run a transient simulation first.";

        // ── Function Generator (Virtual Instrument) ───────────────────────────────
        [ObservableProperty] private string _fgenWaveform     = "Sine";
        [ObservableProperty] private double _fgenAmplitude    = 1.0;   // V
        [ObservableProperty] private double _fgenFrequency    = 1000;  // Hz
        [ObservableProperty] private double _fgenDcOffset     = 0.0;   // V
        [ObservableProperty] private double _fgenDutyCycle    = 50.0;  // %
        [ObservableProperty] private string _fgenTargetSource = "V1";
        [ObservableProperty] private string _fgenStatus       = "Configure parameters and click Apply to inject a signal.";

        public System.Collections.Generic.List<string> FgenWaveforms { get; } = new()
            { "Sine", "Square", "Triangle", "Sawtooth", "DC" };

        [RelayCommand]
        private void ApplyFunctionGenerator()
        {
            // Build SPICE source value string
            string sourceValue;
            switch (FgenWaveform)
            {
                case "Sine":
                    sourceValue = $"SIN({FgenDcOffset} {FgenAmplitude} {FgenFrequency})";
                    break;
                case "Square":
                    double period = 1.0 / FgenFrequency;
                    double highTime = period * FgenDutyCycle / 100.0;
                    double riseTime = period * 0.001;
                    sourceValue = $"PULSE({FgenDcOffset} {FgenAmplitude + FgenDcOffset} 0 {riseTime:G3} {riseTime:G3} {highTime:G3} {period:G3})";
                    break;
                case "Triangle":
                case "Sawtooth":
                    double tPeriod = 1.0 / FgenFrequency;
                    sourceValue = $"SIN({FgenDcOffset} {FgenAmplitude} {FgenFrequency})"; // approximation
                    break;
                case "DC":
                    sourceValue = $"DC {FgenDcOffset + FgenAmplitude}";
                    break;
                default:
                    sourceValue = $"SIN(0 {FgenAmplitude} {FgenFrequency})";
                    break;
            }

            // Find the target voltage source on the active schematic
            var schematic = ActiveSchematicViewModel?.CoreSchematic;
            if (schematic == null) { FgenStatus = "Error: No active schematic."; return; }

            var targetComp = schematic.Components.Values
                .FirstOrDefault(c => string.Equals(c.Designator, FgenTargetSource, StringComparison.OrdinalIgnoreCase));

            if (targetComp == null)
            {
                FgenStatus = $"Error: Component '{FgenTargetSource}' not found. Add a voltage source with this designator.";
                return;
            }

            // Update the visual VM which updates the core component value and triggers notification
            var compVm = ActiveSchematicViewModel!.Items
                .OfType<ComponentNodeViewModel>()
                .FirstOrDefault(c => c.CoreComponent == targetComp);
            if (compVm != null)
            {
                compVm.Value = sourceValue;
            }
            else
            {
                targetComp.Value = sourceValue;
            }

            FgenStatus = $"✅ Applied {FgenWaveform} {FgenAmplitude}V @ {FgenFrequency}Hz to {FgenTargetSource}. Run simulation to see waveform.";
        }

        // ── Bode Plot (Gain + Phase vs. Frequency) ────────────────────────────────
        [ObservableProperty]
        private PlotModel _bodePlotModel = new()
        {
            Title = "Bode Plot — Gain & Phase",
            Background = OxyColors.Transparent,
            TextColor = OxyColors.LightGray,
            PlotAreaBorderColor = OxyColor.FromArgb(80, 80, 80, 120)
        };

        [ObservableProperty] private string _bodeInputNode  = "v(vin)";
        [ObservableProperty] private string _bodeOutputNode = "v(vout)";
        [ObservableProperty] private string _bodeStatus     = "Run an AC sweep simulation then click 'Plot Bode'.";

        [RelayCommand]
        private async System.Threading.Tasks.Task PlotBodeAsync()
        {
            if (ActiveSchematicViewModel == null) return;

            if (string.IsNullOrWhiteSpace(BodeInputNode) || string.IsNullOrWhiteSpace(BodeOutputNode))
            {
                BodeStatus = "Error: Input and Output nodes must be specified.";
                return;
            }

            // Remove node functions like "v(" and ")" from input/output node names to format them properly
            string inputNodeClean = BodeInputNode.Replace("v(", "").Replace(")", "").Trim();
            string outputNodeClean = BodeOutputNode.Replace("v(", "").Replace(")", "").Trim();

            // If we don't have AC simulation data, run it now!
            bool needsAcSim = LastSimulationData == null || !LastSimulationData.DataPoints.ContainsKey("frequency");
            if (needsAcSim)
            {
                BodeStatus = "Running AC simulation...";
                string prevSimType = SimulationType;
                SimulationType = "AC Sweep";
                
                // Find a voltage source connected to the input node and temporarily add "AC 1"
                VoltageSource? inputSource = null;
                string originalValue = "";
                
                var inputNet = ActiveSchematicViewModel.CoreSchematic.Nets.Values
                    .FirstOrDefault(n => n.Name.Equals(inputNodeClean, System.StringComparison.OrdinalIgnoreCase));
                    
                if (inputNet != null)
                {
                    foreach (var comp in ActiveSchematicViewModel.CoreSchematic.Components.Values.OfType<VoltageSource>())
                    {
                        if (comp.Pins.Any(p => p.ConnectedNetId == inputNet.Id))
                        {
                            inputSource = comp;
                            originalValue = comp.Value;
                            if (!comp.Value.Contains("AC", System.StringComparison.OrdinalIgnoreCase))
                            {
                                comp.Value += " AC 1";
                            }
                            break;
                        }
                    }
                }

                try
                {
                    await SimulateAsync();
                }
                catch (System.Exception ex)
                {
                    BodeStatus = $"Simulation failed: {ex.Message}";
                    return;
                }
                finally
                {
                    SimulationType = prevSimType;
                    if (inputSource != null)
                    {
                        inputSource.Value = originalValue;
                    }
                }
            }

            // Process simulation data and plot Bode
            if (LastSimulationData == null || !LastSimulationData.DataPoints.ContainsKey("frequency"))
            {
                BodeStatus = "Error: No AC Sweep data available.";
                return;
            }

            var data = LastSimulationData;
            var freqList = data.DataPoints["frequency"];
            
            string inKey = $"v({inputNodeClean.ToLowerInvariant()})";
            string outKey = $"v({outputNodeClean.ToLowerInvariant()})";

            // If input node is "0" or "gnd", it's 0V, but we can't divide by 0. 
            // In AC sweep, if input node is gnd, let's treat input magnitude as 1.0 and phase as 0.0
            bool isInputGnd = inputNodeClean.Equals("0", System.StringComparison.OrdinalIgnoreCase) || 
                              inputNodeClean.Equals("gnd", System.StringComparison.OrdinalIgnoreCase);

            if (!isInputGnd && !data.DataPoints.ContainsKey(inKey))
            {
                BodeStatus = $"Error: Input node '{inputNodeClean}' not found in simulation data.";
                return;
            }

            if (!data.DataPoints.ContainsKey(outKey))
            {
                BodeStatus = $"Error: Output node '{outputNodeClean}' not found in simulation data.";
                return;
            }

            var inMag = isInputGnd ? null : data.DataPoints[inKey];
            var inPhase = isInputGnd ? null : (data.DataPoints.TryGetValue(inKey + "_phase", out var ip) ? ip : null);
            
            var outMag = data.DataPoints[outKey];
            var outPhase = data.DataPoints.TryGetValue(outKey + "_phase", out var op) ? op : null;

            int count = freqList.Count;
            if (count == 0)
            {
                BodeStatus = "Error: Empty simulation dataset.";
                return;
            }

            BodePlotModel.Series.Clear();
            BodePlotModel.Axes.Clear();

            // Create plot model
            BodePlotModel.Title = $"Bode Plot: {BodeOutputNode} / {BodeInputNode}";

            // Logarithmic X-Axis (Frequency)
            var freqAxis = new LogarithmicAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Frequency (Hz)",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.FromArgb(40, 128, 128, 128),
                MinorGridlineColor = OxyColor.FromArgb(20, 128, 128, 128),
                TextColor = OxyColors.LightGray
            };
            BodePlotModel.Axes.Add(freqAxis);

            // Left Y-Axis (Gain in dB)
            var gainAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Gain (dB)",
                Key = "GainAxis",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.FromArgb(40, 128, 128, 128),
                MinorGridlineColor = OxyColor.FromArgb(20, 128, 128, 128),
                TextColor = OxyColors.LightGreen,
                TitleColor = OxyColors.LightGreen
            };
            BodePlotModel.Axes.Add(gainAxis);

            // Right Y-Axis (Phase in Degrees)
            var phaseAxis = new LinearAxis
            {
                Position = AxisPosition.Right,
                Title = "Phase (Degrees)",
                Key = "PhaseAxis",
                MajorGridlineStyle = LineStyle.None,
                MinorGridlineStyle = LineStyle.None,
                TextColor = OxyColors.Orange,
                TitleColor = OxyColors.Orange
            };
            BodePlotModel.Axes.Add(phaseAxis);

            var gainSeries = new LineSeries
            {
                Title = "Gain (dB)",
                Color = OxyColors.LightGreen,
                StrokeThickness = 2.5,
                YAxisKey = "GainAxis"
            };

            var phaseSeries = new LineSeries
            {
                Title = "Phase (Degrees)",
                Color = OxyColors.Orange,
                StrokeThickness = 2.0,
                LineStyle = LineStyle.Dash,
                YAxisKey = "PhaseAxis"
            };

            for (int i = 0; i < count; i++)
            {
                double freq = freqList[i];
                if (freq <= 0) continue; // Logarithmic axis requires positive values

                // Gain calculation
                double v1 = isInputGnd ? 1.0 : inMag![i];
                double v2 = outMag[i];
                
                // Avoid division by zero or log of zero
                if (v1 <= 1e-20) v1 = 1e-20;
                if (v2 <= 1e-20) v2 = 1e-20;

                double gainDb = 20.0 * Math.Log10(v2 / v1);
                gainSeries.Points.Add(new DataPoint(freq, gainDb));

                // Phase calculation
                double p1 = (isInputGnd || inPhase == null) ? 0.0 : inPhase[i];
                double p2 = (outPhase == null) ? 0.0 : outPhase[i];

                double diffPhase = p2 - p1;
                phaseSeries.Points.Add(new DataPoint(freq, diffPhase));
            }

            BodePlotModel.Series.Add(gainSeries);
            BodePlotModel.Series.Add(phaseSeries);

            // Add legend
            BodePlotModel.Legends.Clear();
            BodePlotModel.Legends.Add(new OxyPlot.Legends.Legend
            {
                LegendPosition = OxyPlot.Legends.LegendPosition.TopRight,
                LegendBackground = OxyColor.FromArgb(150, 30, 30, 30),
                LegendBorderThickness = 1,
                LegendTextColor = OxyColors.White
            });

            BodePlotModel.InvalidatePlot(true);
            BodeStatus = $"Bode plot updated successfully with {gainSeries.Points.Count} points.";
        }



        public void UpdateMultimeterReadings()
        {
            MultimeterReadings.Clear();

            if (ActiveSchematicViewModel == null) return;

            // Gather all voltage probes
            var voltageProbes = ActiveSchematicViewModel.Items.OfType<VoltageProbeItemViewModel>().ToList();
            foreach (var probe in voltageProbes)
            {
                var reading = new MultimeterReading
                {
                    Target = $"V({probe.TargetNetName})",
                    Type = "Voltage"
                };

                if (LastSimulationData != null)
                {
                    string key = probe.TargetNetName.ToLower();
                    string spiceKey = $"v({key})";
                    if (LastSimulationData.DataPoints.TryGetValue(spiceKey, out var points) && points.Count > 0)
                    {
                        double lastVal = points[^1];
                        double dc = points.Average();
                        double sumSq = points.Sum(v => v * v);
                        double rms = System.Math.Sqrt(sumSq / points.Count);

                        reading.ValueText = $"{lastVal:F3} V";
                        reading.RmsText = $"{rms:F3} V RMS (DC={dc:F3}V)";
                    }
                    else
                    {
                        reading.ValueText = "N/A";
                        reading.RmsText = "Run simulation to read";
                    }
                }
                MultimeterReadings.Add(reading);
            }

            // Gather all current probes
            var currentProbes = ActiveSchematicViewModel.Items.OfType<CurrentProbeItemViewModel>().ToList();
            foreach (var probe in currentProbes)
            {
                var reading = new MultimeterReading
                {
                    Target = $"I({probe.TargetDeviceDesignator})",
                    Type = "Current"
                };

                if (LastSimulationData != null)
                {
                    string key = probe.TargetDeviceDesignator.ToLower();
                    string? foundKey = LastSimulationData.DataPoints.Keys.FirstOrDefault(k => k.Contains(key) && k.StartsWith("i("));
                    
                    if (foundKey != null && LastSimulationData.DataPoints.TryGetValue(foundKey, out var points) && points.Count > 0)
                    {
                        double lastVal = points[^1];
                        double dc = points.Average();
                        double sumSq = points.Sum(v => v * v);
                        double rms = System.Math.Sqrt(sumSq / points.Count);

                        reading.ValueText = $"{lastVal * 1000.0:F3} mA";
                        reading.RmsText = $"{rms * 1000.0:F3} mA RMS (DC={dc * 1000.0:F3}mA)";
                    }
                    else
                    {
                        reading.ValueText = "N/A";
                        reading.RmsText = "Run simulation to read";
                    }
                }
                MultimeterReadings.Add(reading);
            }
        }

        private void ApplyWindow(double[] data, string windowType)
        {
            int n = data.Length;
            if (windowType == "Hanning")
            {
                for (int i = 0; i < n; i++)
                {
                    data[i] *= 0.5 * (1.0 - System.Math.Cos(2.0 * System.Math.PI * i / (n - 1)));
                }
            }
            else if (windowType == "Hamming")
            {
                for (int i = 0; i < n; i++)
                {
                    data[i] *= 0.54 - 0.46 * System.Math.Cos(2.0 * System.Math.PI * i / (n - 1));
                }
            }
            else if (windowType == "Blackman")
            {
                for (int i = 0; i < n; i++)
                {
                    data[i] *= 0.42 - 0.5 * System.Math.Cos(2.0 * System.Math.PI * i / (n - 1)) + 0.08 * System.Math.Cos(4.0 * System.Math.PI * i / (n - 1));
                }
            }
        }

        [RelayCommand]
        private void RunSpectraAnalysis()
        {
            if (LastSimulationData == null)
            {
                SpectraStatus = "Error: No simulation data. Run a simulation first.";
                return;
            }

            if (string.IsNullOrEmpty(SelectedSpectraNode))
            {
                SpectraStatus = "Error: Select a node first.";
                return;
            }

            if (!LastSimulationData.DataPoints.ContainsKey("time"))
            {
                SpectraStatus = "Error: FFT analysis requires transient (time-domain) simulation.";
                return;
            }

            var timeList = LastSimulationData.DataPoints["time"];
            if (!LastSimulationData.DataPoints.TryGetValue(SelectedSpectraNode, out var yList) || yList.Count < 4)
            {
                SpectraStatus = "Error: Selected node data is invalid or too short.";
                return;
            }

            int n = yList.Count;
            int fftSize = 1;
            while (fftSize * 2 <= n) fftSize *= 2;

            if (fftSize < 4)
            {
                SpectraStatus = "Error: Not enough data points.";
                return;
            }

            double[] yArray = yList.Take(fftSize).ToArray();
            ApplyWindow(yArray, SelectedFftWindow);

            var complexData = new System.Numerics.Complex[fftSize];
            for (int i = 0; i < fftSize; i++)
            {
                complexData[i] = new System.Numerics.Complex(yArray[i], 0);
            }

            FftHelper.Fft(complexData);

            double totalTime = timeList[fftSize - 1] - timeList[0];
            double dt = totalTime / (fftSize - 1);
            if (dt <= 0)
            {
                SpectraStatus = "Error: Invalid time step dt.";
                return;
            }
            double samplingFreq = 1.0 / dt;

            SpectraPlotModel.Series.Clear();
            SpectraPlotModel.Axes.Clear();

            SpectraPlotModel.Title = $"FFT Spectrum of {SelectedSpectraNode.ToUpper()} ({SelectedFftWindow} Window)";
            
            SpectraPlotModel.Axes.Add(new LogarithmicAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Frequency (Hz)",
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.FromArgb(60, 80, 80, 140),
                TicklineColor = OxyColors.Gray,
                AxislineColor = OxyColors.Gray,
                TextColor = OxyColors.LightGray
            });

            SpectraPlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Magnitude (dB)",
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.FromArgb(60, 80, 80, 140),
                TicklineColor = OxyColors.Gray,
                AxislineColor = OxyColors.Gray,
                TextColor = OxyColors.LightGray
            });

            var series = new LineSeries
            {
                Title = "Magnitude",
                Color = OxyColors.Orange,
                StrokeThickness = 2,
                TrackerFormatString = "{0}\nFreq: {2:0.00} Hz\nMagnitude: {4:0.00} dB"
            };

            int halfSize = fftSize / 2;
            for (int k = 1; k < halfSize; k++)
            {
                double freq = k * samplingFreq / fftSize;
                double magnitude = complexData[k].Magnitude / fftSize;
                if (k > 0) magnitude *= 2.0;

                double db = 20.0 * System.Math.Log10(magnitude + 1e-15);
                series.Points.Add(new DataPoint(freq, db));
            }

            SpectraPlotModel.Series.Add(series);
            SpectraPlotModel.InvalidatePlot(true);

            SpectraStatus = $"FFT Complete. Size: {fftSize} pts | Fs: {samplingFreq:F1} Hz | Res: {samplingFreq / fftSize:F2} Hz";
        }

        // ── Oscilloscope: persistent singleton window ────────────────────────────────
        private Views.OscilloscopeWindow? _scopeWindow;
        private Views.OscilloscopeWindow GetScopeWindow()
        {
            if (_scopeWindow == null || !_scopeWindow.IsLoaded)
                _scopeWindow = new Views.OscilloscopeWindow();
            return _scopeWindow;
        }

        public MainViewModel()
        {
            // Initialize with an empty schematic
            ActiveSchematicViewModel = new SchematicViewModel(new Schematic("New Project"));
            SchematicSheets.Add(ActiveSchematicViewModel);
            SelectedSchematicSheet = ActiveSchematicViewModel;
            NetlistOutput = "No netlist generated yet. Click 'Generate .cir' to export.";

            _liveTunerTimer = new System.Windows.Threading.DispatcherTimer();
            _liveTunerTimer.Interval = System.TimeSpan.FromSeconds(1);
            _liveTunerTimer.Tick += HandleLiveTuningTick;

            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
            _autoSaveTimer.Interval = System.TimeSpan.FromMinutes(2); // Auto-save every 2 minutes
            _autoSaveTimer.Tick += HandleAutoSaveTick;
            _autoSaveTimer.Start();
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

        private void HandleAutoSaveTick(object? sender, System.EventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentProjectPath)) return;
            if (IsSimulating) return;

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

                var netLabels = ActiveSchematicViewModel.Items
                    .OfType<NetLabelItemViewModel>()
                    .Select(l => new NetLabelRecord
                    {
                        NetName = l.NetName,
                        NetId   = l.AssociatedNetId,
                        X       = l.X,
                        Y       = l.Y
                    });

                var doc = ProjectFileService.ToDocument(
                    ActiveSchematicViewModel.CoreSchematic,
                    placements,
                    ActiveSchematicViewModel.CoreSchematic.Title,
                    netLabels);

                ProjectFileService.Save(doc, CurrentProjectPath);
                StatusText = $"Project auto-saved  |  {System.IO.Path.GetFileName(CurrentProjectPath)}  |  {System.DateTime.Now:HH:mm:ss}";
            }
            catch
            {
                // Quiet fail for background auto-saves
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
        private void SyncPcb()
        {
            var schematic = ActiveSchematicViewModel.CoreSchematic;
            if (schematic.Components.Count == 0)
            {
                StatusText = "Sync to PCB: No components in schematic.";
                return;
            }

            PcbVM.ImportFromSchematicCommand.Execute(schematic);
            StatusText = $"📰 PCB synced — {schematic.Components.Count} component(s) placed as footprints. Switch to the PCB Layout tab to edit.";
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
            var directive = BuildSimDirective();
            var netlist = exporter.GenerateNetlist(ActiveSchematicViewModel.CoreSchematic, directive);
            NetlistOutput = drcResult.logOutput + netlist;

            _lastExecutedNetlistHash = netlist; // Anchor the graph hash for Live Tuning

            ClearOperatingPointAnnotations();

            IsSimulating = true;
            _simCancellationTokenSource = new System.Threading.CancellationTokenSource();

            var executionSvc = new SpiceExecutionService(
                EdaSimulator.Engines.Settings.SettingsManager.Instance.Current.NgSpicePath);
            EdaSimulator.Engines.Simulation.SpiceExecutionResult result;
            
            var progress = new Progress<string>(line =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    StatusText = $"Simulation: {line.Trim()}";
                }
            });

            try
            {
                // Push the UI cancellation token completely down into the native OS Process Wrapper
                result = await executionSvc.RunSimulationAsync(netlist, _simCancellationTokenSource.Token, progress);
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

                // Reset selections first
                foreach (var item in ActiveSchematicViewModel.Items)
                {
                    item.IsSelected = false;
                }

                if (!string.IsNullOrEmpty(result.AffectedDesignator))
                {
                    var comp = ActiveSchematicViewModel.Items
                        .OfType<ComponentNodeViewModel>()
                        .FirstOrDefault(c => string.Equals(c.Designator, result.AffectedDesignator, StringComparison.OrdinalIgnoreCase));
                    if (comp != null)
                    {
                        comp.IsSelected = true;
                        NetlistOutput += $"\n>>> Highlighted offending component on canvas: {result.AffectedDesignator} <<<";
                    }
                }
                else if (!string.IsNullOrEmpty(result.AffectedNetName))
                {
                    var netName = result.AffectedNetName;
                    var wires = ActiveSchematicViewModel.Items
                        .OfType<WireViewModel>()
                        .Where(w => w.NetLabel != null && string.Equals(w.NetLabel, netName, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var w in wires)
                    {
                        w.IsSelected = true;
                    }

                    if (wires.Count > 0)
                    {
                        NetlistOutput += $"\n>>> Highlighted offending net on canvas: {netName} <<<";
                    }
                }
                return;
            }
            
            NetlistOutput += $"\n\n--- SIMULATION SUCCESS ---\n{result.OutputLog}";

            if (System.IO.File.Exists(result.RawFilePath))
            {
                var data = RawFileParser.Parse(result.RawFilePath);
                LastSimulationData = data;
                UpdateMultimeterReadings();
                UpdateOperatingPointAnnotations();

                AvailableSpectraNodes.Clear();
                foreach (var kv in data.DataPoints)
                {
                    if (kv.Key == "time") continue;
                    AvailableSpectraNodes.Add(kv.Key);
                }
                if (AvailableSpectraNodes.Count > 0)
                {
                    SelectedSpectraNode = AvailableSpectraNodes.FirstOrDefault(n => n.StartsWith("v(")) ?? AvailableSpectraNodes[0];
                }

                var scopeWindow = GetScopeWindow();
                scopeWindow.ViewModel.ClearTraces();

                // Require a time axis — transient analysis
                if (data.DataPoints.ContainsKey("time"))
                {
                    var timeAxis = data.DataPoints["time"];

                    // Co-simulate firmware execution if MCU is present
                    double stopTime = ParseSpiceTime(TransientStopTime);
                    var mcu = ActiveSchematicViewModel?.CoreSchematic?.Components.Values
                        .OfType<McuComponent>()
                        .FirstOrDefault();
                    if (mcu != null)
                    {
                        SerialMonitorOutput = VirtualMcuSimulationEngine.RunCoSimulation(mcu.FirmwarePath, stopTime);
                    }
                    else
                    {
                        SerialMonitorOutput = "[Serial Monitor] No MCU found on schematic to execute firmware.";
                    }

                    // Auto-display ALL signal vectors (voltages, currents) — not just named probes
                    // Skip "time" itself and internal branch currents that start with "i("
                    var waveformColors = new[]
                    {
                        OxyPlot.OxyColors.Cyan, OxyPlot.OxyColors.Yellow, OxyPlot.OxyColors.LimeGreen,
                        OxyPlot.OxyColors.OrangeRed, OxyPlot.OxyColors.Violet, OxyPlot.OxyColors.DeepSkyBlue
                    };
                    int colorIdx = 0;

                    foreach (var kv in data.DataPoints)
                    {
                        if (kv.Key == "time") continue; // skip X axis itself
                        string label = kv.Key.ToUpper();
                        scopeWindow.ViewModel.RenderTraceColored(label, timeAxis, kv.Value,
                            waveformColors[colorIdx % waveformColors.Length]);
                        colorIdx++;
                    }

                    scopeWindow.Show();
                }
                else if (data.DataPoints.ContainsKey("frequency"))
                {
                    var freqAxis = data.DataPoints["frequency"];
                    bool isNoise = data.DataPoints.Keys.Any(k => k.Contains("onoise") || k.Contains("inoise"));

                    if (isNoise)
                    {
                        foreach (var kv in data.DataPoints)
                        {
                            if (kv.Key == "frequency") continue;
                            scopeWindow.ViewModel.RenderNoisePlot(kv.Key.ToUpper(), freqAxis, kv.Value);
                        }
                    }
                    else
                    {
                        // AC analysis — frequency domain (Bode plot)
                        foreach (var kv in data.DataPoints)
                        {
                            if (kv.Key == "frequency") continue;
                            scopeWindow.ViewModel.RenderBodePlot(kv.Key.ToUpper(), freqAxis, kv.Value);
                        }
                    }
                    scopeWindow.Show();
                }
                else if (data.Variables.Count > 0)
                {
                    // DC Sweep or other sweep analysis
                    string sweepVar = data.Variables[0];
                    if (data.DataPoints.ContainsKey(sweepVar))
                    {
                        var sweepAxis = data.DataPoints[sweepVar];
                        scopeWindow.ViewModel.SetupSweepPlot(sweepVar.ToUpper(), $"DC Sweep Analysis — Sweeping {sweepVar.ToUpper()}");

                        var waveformColors = new[]
                        {
                            OxyPlot.OxyColors.Cyan, OxyPlot.OxyColors.Yellow, OxyPlot.OxyColors.LimeGreen,
                            OxyPlot.OxyColors.OrangeRed, OxyPlot.OxyColors.Violet, OxyPlot.OxyColors.DeepSkyBlue
                        };
                        int colorIdx = 0;

                        foreach (var kv in data.DataPoints)
                        {
                            if (kv.Key == sweepVar) continue; // skip the independent variable
                            string label = kv.Key.ToUpper();
                            scopeWindow.ViewModel.RenderTraceColored(
                                label, 
                                sweepAxis, 
                                kv.Value,
                                waveformColors[colorIdx % waveformColors.Length],
                                "{0}\n" + sweepVar.ToUpper() + ": {2:0.000}\nValue: {4:0.000}");
                            colorIdx++;
                        }
                        scopeWindow.Show();
                    }
                    else
                    {
                        NetlistOutput += "\n[INFO] Simulation complete. Sweep variable data not found in .raw output.";
                    }
                }
                else
                {
                    // Operating point — show first non-sweep variable
                    NetlistOutput += "\n[INFO] Simulation complete. No variable data found in .raw output.";
                }

                int traceCount = scopeWindow.ViewModel.TraceInfos.Count;
                StatusText = $"Simulation complete — {traceCount} trace(s) plotted  |  Mode: {SimulationType}";
            }
            else
            {
                StatusText = $"Simulation complete — no .raw data output (check netlist for errors)";
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

                // Reconstruct visual wires for the mock circuit
                schVM.ReconstructWiresFromNets();

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

                var netLabels = ActiveSchematicViewModel.Items
                    .OfType<NetLabelItemViewModel>()
                    .Select(l => new NetLabelRecord
                    {
                        NetName = l.NetName,
                        NetId   = l.AssociatedNetId,
                        X       = l.X,
                        Y       = l.Y
                    });

                var doc = ProjectFileService.ToDocument(
                    ActiveSchematicViewModel.CoreSchematic,
                    placements,
                    ActiveSchematicViewModel.CoreSchematic.Title,
                    netLabels);

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

                // Reconstruct visual wire connections from the loaded net structures
                ActiveSchematicViewModel.ReconstructWiresFromNets();

                // Restore net labels
                if (doc.NetLabels != null)
                {
                    foreach (var nlRec in doc.NetLabels)
                    {
                        var labelVm = new NetLabelItemViewModel(nlRec.NetName, nlRec.X, nlRec.Y, nlRec.NetId);
                        ActiveSchematicViewModel.Items.Add(labelVm);
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
                "DC Sweep"  => $".dc {DcSweepComponent} {DcSweepStart} {DcSweepStop} {DcSweepStep}",
                "Noise Analysis" => GetNoiseDirective(),
                "Pole-Zero" => GetPoleZeroDirective(),
                _           => $".tran {TransientStepTime} {TransientStopTime}"
            };
        }

        private string GetPoleZeroDirective()
        {
            string inNode = "1";
            if (ActiveSchematicViewModel?.CoreSchematic?.Components != null)
            {
                var firstV = ActiveSchematicViewModel.CoreSchematic.Components.Values
                    .FirstOrDefault(c => c.Designator.StartsWith("V", StringComparison.OrdinalIgnoreCase));
                if (firstV != null && firstV.Pins.Count > 0)
                {
                    inNode = ActiveSchematicViewModel.CoreSchematic.GetNetNameForPin(firstV.Pins[0]);
                }
            }

            string outNode = "out";
            if (ActiveSchematicViewModel != null)
            {
                var namedWire = ActiveSchematicViewModel.Items.OfType<WireViewModel>()
                    .FirstOrDefault(w => !string.IsNullOrEmpty(w.NetLabel) && 
                                         (w.NetLabel.Contains("out", StringComparison.OrdinalIgnoreCase) || 
                                          w.NetLabel.Contains("output", StringComparison.OrdinalIgnoreCase)));
                if (namedWire != null && !string.IsNullOrEmpty(namedWire.NetLabel))
                {
                    outNode = namedWire.NetLabel;
                }
                else
                {
                    var generalWire = ActiveSchematicViewModel.Items.OfType<WireViewModel>()
                        .FirstOrDefault(w => !string.IsNullOrEmpty(w.NetLabel) && 
                                             !w.NetLabel.Equals("GND", StringComparison.OrdinalIgnoreCase) && 
                                             !w.NetLabel.Equals("0", StringComparison.OrdinalIgnoreCase) && 
                                             !w.NetLabel.Equals("VCC", StringComparison.OrdinalIgnoreCase) && 
                                             !w.NetLabel.Equals("VDD", StringComparison.OrdinalIgnoreCase));
                    if (generalWire != null && !string.IsNullOrEmpty(generalWire.NetLabel))
                    {
                        outNode = generalWire.NetLabel;
                    }
                }
            }

            return $".pz {inNode} 0 {outNode} 0 vol pz";
        }

        private string GetNoiseDirective()
        {
            string srcName = "V1";
            if (ActiveSchematicViewModel?.CoreSchematic?.Components != null)
            {
                var firstV = ActiveSchematicViewModel.CoreSchematic.Components.Values
                    .FirstOrDefault(c => c.Designator.StartsWith("V", StringComparison.OrdinalIgnoreCase));
                if (firstV != null)
                {
                    srcName = firstV.Designator;
                }
            }

            string outNode = "out";
            if (ActiveSchematicViewModel != null)
            {
                var namedWire = ActiveSchematicViewModel.Items.OfType<WireViewModel>()
                    .FirstOrDefault(w => !string.IsNullOrEmpty(w.NetLabel) && 
                                         (w.NetLabel.Contains("out", StringComparison.OrdinalIgnoreCase) || 
                                          w.NetLabel.Contains("output", StringComparison.OrdinalIgnoreCase)));
                if (namedWire != null && !string.IsNullOrEmpty(namedWire.NetLabel))
                {
                    outNode = namedWire.NetLabel;
                }
                else
                {
                    var generalWire = ActiveSchematicViewModel.Items.OfType<WireViewModel>()
                        .FirstOrDefault(w => !string.IsNullOrEmpty(w.NetLabel) && 
                                             !w.NetLabel.Equals("GND", StringComparison.OrdinalIgnoreCase) && 
                                             !w.NetLabel.Equals("0", StringComparison.OrdinalIgnoreCase) && 
                                             !w.NetLabel.Equals("VCC", StringComparison.OrdinalIgnoreCase) && 
                                             !w.NetLabel.Equals("VDD", StringComparison.OrdinalIgnoreCase));
                    if (generalWire != null && !string.IsNullOrEmpty(generalWire.NetLabel))
                    {
                        outNode = generalWire.NetLabel;
                    }
                }
            }

            return $".noise v({outNode}) {srcName} dec {AcPointsPerDecade} {AcStartFreq} {AcStopFreq}";
        }

        private double ParseSpiceTime(string timeStr)
        {
            if (string.IsNullOrEmpty(timeStr)) return 0.01;
            string clean = timeStr.Trim().ToLower();
            if (clean.EndsWith("s"))
            {
                clean = clean.Substring(0, clean.Length - 1).Trim();
            }
            double scale = 1.0;
            if (clean.EndsWith("m")) { scale = 1e-3; clean = clean.Substring(0, clean.Length - 1); }
            else if (clean.EndsWith("u")) { scale = 1e-6; clean = clean.Substring(0, clean.Length - 1); }
            else if (clean.EndsWith("n")) { scale = 1e-9; clean = clean.Substring(0, clean.Length - 1); }
            else if (clean.EndsWith("p")) { scale = 1e-12; clean = clean.Substring(0, clean.Length - 1); }

            if (double.TryParse(clean, out double val))
            {
                return val * scale;
            }
            return 0.01;
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

        [RelayCommand]
        private void ClearSerialMonitor()
        {
            SerialMonitorOutput = "[Console Cleared]";
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Phase 7: Update Status when selection changes
        // ──────────────────────────────────────────────────────────────────────────────

        public void UpdateSelectionState()
        {
            if (ActiveSchematicViewModel == null)
            {
                ComponentProperties.Clear();
                return;
            }

            if (ActiveSchematicViewModel.SelectedComponent != null)
            {
                var node = ActiveSchematicViewModel.SelectedComponent;
                ComponentProperties.Populate(node);
                StatusText = $"Selected: {node.CoreComponent.Designator} ({node.CoreComponent.GetType().Name})  |  Value: {node.CoreComponent.Value}";
            }
            else if (ActiveSchematicViewModel.SelectedWire != null)
            {
                var wire = ActiveSchematicViewModel.SelectedWire;
                ComponentProperties.PopulateWire(wire, ActiveSchematicViewModel);
                StatusText = $"Selected Net/Wire: {wire.NetLabel}  |  GUID: {wire.TargetNetId}";
            }
            else
            {
                ComponentProperties.Clear();
                StatusText = $"Ready  |  {ActiveSchematicViewModel.CoreSchematic.Components.Count} components  |  {ActiveSchematicViewModel.CoreSchematic.Nets.Count} nets";
            }
        }

        partial void OnActiveSchematicViewModelChanged(SchematicViewModel? oldValue, SchematicViewModel newValue)
        {
            if (oldValue != null)
            {
                oldValue.Items.CollectionChanged -= HandleSchematicItemsChanged;
                oldValue.PropertyChanged -= HandleSchematicPropertyChanged;
                oldValue.NetProbed -= HandleNetProbed;
            }
            newValue.Items.CollectionChanged += HandleSchematicItemsChanged;
            newValue.PropertyChanged += HandleSchematicPropertyChanged;
            newValue.NetProbed += HandleNetProbed;
            RunLiveDRC();
            UpdateBom();
        }

        private void HandleNetProbed(string netName)
        {
            var scope = GetScopeWindow();
            if (!scope.IsVisible)
            {
                scope.Show();
            }
            scope.ViewModel.HighlightTrace(netName);
        }

        private void HandleSchematicPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SchematicViewModel.SelectedComponent) ||
                e.PropertyName == nameof(SchematicViewModel.SelectedWire))
            {
                UpdateSelectionState();
            }
        }

        private void HandleSchematicItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RunLiveDRC();
            UpdateMultimeterReadings();
            UpdateBom();
        }

        private void RunLiveDRC()
        {
            if (ActiveSchematicViewModel == null) return;
            var (isValid, logOutput) = ActiveSchematicViewModel.RunDRC();
            IsDrcValid = isValid;
            DrcStatusMessage = isValid ? "DRC: PASS" : "DRC: FAIL/WARN";
        }

        private void ClearOperatingPointAnnotations()
        {
            foreach (var sheet in SchematicSheets)
            {
                foreach (var wire in sheet.Items.OfType<WireViewModel>())
                {
                    wire.ShowOpVoltage = false;
                    wire.OpVoltageText = string.Empty;
                }
            }
        }

        partial void OnShowDcBiasOverlayChanged(bool value)
        {
            if (value)
            {
                UpdateOperatingPointAnnotations();
            }
            else
            {
                ClearOperatingPointAnnotations();
            }
        }

        private void UpdateOperatingPointAnnotations()
        {
            if (LastSimulationData == null || !ShowDcBiasOverlay)
            {
                ClearOperatingPointAnnotations();
                return;
            }

            foreach (var sheet in SchematicSheets)
            {
                foreach (var wire in sheet.Items.OfType<WireViewModel>())
                {
                    // Resolve net name
                    string netName = wire.NetLabel;
                    if (string.IsNullOrEmpty(netName))
                    {
                        var net = sheet.CoreSchematic.GetNetById(wire.TargetNetId);
                        if (net != null)
                        {
                            netName = net.Name;
                        }
                    }

                    if (string.IsNullOrEmpty(netName))
                    {
                        wire.ShowOpVoltage = false;
                        continue;
                    }

                    string pinLower = netName.ToLowerInvariant();
                    string key = $"v({pinLower})";

                    // Also check for ground net
                    if (pinLower == "0" || pinLower == "gnd" || pinLower == "ground")
                    {
                        wire.OpVoltageText = "0.000 V";
                        wire.ShowOpVoltage = true;
                    }
                    else if (LastSimulationData.DataPoints.TryGetValue(key, out var list) && list.Count > 0)
                    {
                        double val = list[list.Count - 1];
                        wire.OpVoltageText = FormatVoltage(val);
                        wire.ShowOpVoltage = true;
                    }
                    else
                    {
                        wire.ShowOpVoltage = false;
                    }
                }
            }
        }

        private static string FormatVoltage(double val)
        {
            double abs = Math.Abs(val);
            if (abs >= 1.0)
                return $"{val:F3} V";
            if (abs >= 1e-3)
                return $"{val * 1e3:F2} mV";
            if (abs >= 1e-6)
                return $"{val * 1e6:F2} µV";
            return $"{val:F3} V";
        }
    }

    public class MultimeterReading
    {
        public string Target { get; set; } = "";
        public string Type { get; set; } = ""; // "Voltage" or "Current"
        public string ValueText { get; set; } = "---";
        public string RmsText { get; set; } = "---";
    }
}

