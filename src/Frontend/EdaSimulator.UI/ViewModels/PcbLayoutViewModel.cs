using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.Models.Components;
using EdaSimulator.Engines.PCB;
using Microsoft.Win32;

namespace EdaSimulator.UI.ViewModels
{
    /// <summary>
    /// ViewModel for the Phase 7 PCB Layout panel.
    /// Manages the PCB document, canvas, DRC results, and manufacturing export.
    /// </summary>
    public partial class PcbLayoutViewModel : ObservableObject
    {
        private readonly PcbDrcEngine _drcEngine = new();
        private PcbDocument _pcbDoc;

        public PcbLayoutViewModel()
        {
            _pcbDoc = new PcbDocument { Title = "Untitled PCB" };
            RefreshFreeRoutingAvailability();
        }

        /// <summary>Re-checks whether FreeRouting JAR can be found (call after settings change).</summary>
        public void RefreshFreeRoutingAvailability()
            => IsFreeRoutingAvailable = FreeRoutingService.IsFreeRoutingAvailable();


        // ── PCB Document Properties ──────────────────────────────────────────────────

        [ObservableProperty] private string _pcbTitle     = "Untitled PCB";
        [ObservableProperty] private double _boardWidth   = 100.0;
        [ObservableProperty] private double _boardHeight  = 80.0;
        [ObservableProperty] private int    _layerCount   = 2;

        // ── DRC ──────────────────────────────────────────────────────────────────────

        [ObservableProperty] private string _drcOutput    = "Run DRC to check PCB design rules.";
        [ObservableProperty] private bool   _drcPassed    = true;
        [ObservableProperty] private int    _drcErrors;
        [ObservableProperty] private int    _drcWarnings;

        // ── BOM ──────────────────────────────────────────────────────────────────────

        [ObservableProperty] private string _bomOutput    = "Import schematic to generate BOM.";

        // ── Layer Selector ───────────────────────────────────────────────────────────

        [ObservableProperty] private string _activeLayer  = "F.Cu";

        // ── FreeRouting Autorouter ───────────────────────────────────────────────────

        [ObservableProperty] private bool   _isFreeRoutingAvailable = false;
        [ObservableProperty] private bool   _isAutoRouting          = false;
        [ObservableProperty] private string _autoRouteStatus        = "Idle";

        private CancellationTokenSource? _routingCts;

        public List<string> AvailableLayers => new()
        {
            "F.Cu", "B.Cu", "F.SilkS", "B.SilkS",
            "F.Mask", "B.Mask", "F.Paste", "B.Paste",
            "Edge.Cuts", "In1.Cu", "In2.Cu"
        };

        // ── Canvas Footprints (observable for UI) ────────────────────────────────────

        public ObservableCollection<PcbFootprintVM> CanvasFootprints { get; } = new();

        // ── Canvas Routing & Connection Elements ──────────────────────────────────────

        public ObservableCollection<PcbRatsnestLineVM> CanvasRatsnestLines { get; } = new();
        public ObservableCollection<PcbTraceVM> CanvasTraces { get; } = new();
        public ObservableCollection<PcbViaVM> CanvasVias { get; } = new();

        // ── Coordinate synchronization & rubber-banding ─────────────────────────────

        public void UpdateRatsnestPositions()
        {
            foreach (var rVM in CanvasRatsnestLines)
            {
                var fromFp = CanvasFootprints.FirstOrDefault(f => f.Designator == rVM.FromDesignator);
                var toFp = CanvasFootprints.FirstOrDefault(f => f.Designator == rVM.ToDesignator);
                if (fromFp != null && toFp != null)
                {
                    var fromPad = fromFp.Model.Pads.FirstOrDefault(p => p.PadNumber == rVM.FromPadNumber);
                    var toPad = toFp.Model.Pads.FirstOrDefault(p => p.PadNumber == rVM.ToPadNumber);

                    double fromPadX = fromPad?.X ?? 0;
                    double fromPadY = fromPad?.Y ?? 0;
                    double toPadX = toPad?.X ?? 0;
                    double toPadY = toPad?.Y ?? 0;

                    // Pad coordinates in mm relative to footprint center
                    double fromRad = fromFp.Model.Rotation * Math.PI / 180.0;
                    double fromCos = Math.Cos(fromRad);
                    double fromSin = Math.Sin(fromRad);
                    double fx = fromPadX * fromCos - fromPadY * fromSin;
                    double fy = fromPadX * fromSin + fromPadY * fromCos;

                    double toRad = toFp.Model.Rotation * Math.PI / 180.0;
                    double toCos = Math.Cos(toRad);
                    double toSin = Math.Sin(toRad);
                    double tx = toPadX * toCos - toPadY * toSin;
                    double ty = toPadX * toSin + toPadY * toCos;

                    // Absolute coordinates on screen (scaled by 5 px/mm)
                    rVM.X1 = (fromFp.Model.X + fx) * 5.0;
                    rVM.Y1 = (fromFp.Model.Y + fy) * 5.0;
                    rVM.X2 = (toFp.Model.X + tx) * 5.0;
                    rVM.Y2 = (toFp.Model.Y + ty) * 5.0;
                }
            }
        }

        private void OnFootprintMoved()
        {
            UpdateRatsnestPositions();
        }

        // ── Commands ────────────────────────────────────────────────────────────────

        [RelayCommand]
        private void ImportFromSchematic(Schematic? schematic)
        {
            if (schematic == null) return;

            _pcbDoc = new PcbDocument
            {
                Title  = schematic.Title + " PCB",
                Outline = new PcbBoardOutline { Width_mm = BoardWidth, Height_mm = BoardHeight }
            };

            // Auto-place footprints in a grid from schematic components
            var components = schematic.Components.Values.ToList();
            int cols    = (int)Math.Ceiling(Math.Sqrt(components.Count));
            int spacing = 15; // 15mm component pitch

            CanvasFootprints.Clear();
            CanvasRatsnestLines.Clear();
            CanvasTraces.Clear();
            CanvasVias.Clear();
            _pcbDoc.Footprints.Clear();
            _pcbDoc.Ratsnest.Clear();
            _pcbDoc.Traces.Clear();
            _pcbDoc.Vias.Clear();

            for (int i = 0; i < components.Count; i++)
            {
                var comp = components[i];
                int col  = i % cols;
                int row  = i / cols;

                var fp = new PcbFootprint
                {
                    Designator  = comp.Designator,
                    Value       = comp.Value,
                    FootprintId = SuggestFootprintId(comp),
                    X           = 15 + col * spacing,
                    Y           = 15 + row * spacing,
                    Rotation    = 0
                };

                // Suggest courtyard sizes
                if (comp is Resistor || comp is Capacitor || comp is Inductor || comp is Diode)
                {
                    fp.CrtYd_Width_mm = 5.0;
                    fp.CrtYd_Height_mm = 4.0;
                }
                else if (comp is BJT || comp is MOSFET)
                {
                    fp.CrtYd_Width_mm = 6.0;
                    fp.CrtYd_Height_mm = 5.0;
                }
                else if (comp is OpAmp)
                {
                    fp.CrtYd_Width_mm = 10.0;
                    fp.CrtYd_Height_mm = 8.0;
                }
                else
                {
                    fp.CrtYd_Width_mm = 8.0;
                    fp.CrtYd_Height_mm = 8.0;
                }

                // Generate pads based on component type
                fp.Pads.AddRange(GeneratePads(comp));
                _pcbDoc.Footprints.Add(fp);

                var fpVM = new PcbFootprintVM(fp, OnFootprintMoved);
                CanvasFootprints.Add(fpVM);
            }

            // Generate Ratsnest from Schematic Nets
            foreach (var net in schematic.Nets.Values)
            {
                var netPins = new List<(Component Component, Pin Pin)>();
                foreach (var pinId in net.ConnectedPinIds)
                {
                    foreach (var comp in schematic.Components.Values)
                    {
                        var pin = comp.Pins.FirstOrDefault(p => p.Id == pinId);
                        if (pin != null)
                        {
                            netPins.Add((comp, pin));
                            break;
                        }
                    }
                }

                if (netPins.Count >= 2)
                {
                    for (int i = 0; i < netPins.Count - 1; i++)
                    {
                        var fromPin = netPins[i];
                        var toPin = netPins[i + 1];

                        var ratsnestLine = new PcbRatsnestLine
                        {
                            NetName = net.Name,
                            FromDesignator = fromPin.Component.Designator,
                            FromPadNumber = fromPin.Pin.SpiceNodeSequence.ToString(),
                            ToDesignator = toPin.Component.Designator,
                            ToPadNumber = toPin.Pin.SpiceNodeSequence.ToString()
                        };
                        _pcbDoc.Ratsnest.Add(ratsnestLine);

                        var rVM = new PcbRatsnestLineVM
                        {
                            NetName = net.Name,
                            FromDesignator = ratsnestLine.FromDesignator,
                            FromPadNumber = ratsnestLine.FromPadNumber,
                            ToDesignator = ratsnestLine.ToDesignator,
                            ToPadNumber = ratsnestLine.ToPadNumber
                        };
                        CanvasRatsnestLines.Add(rVM);
                    }
                }
            }

            UpdateRatsnestPositions();

            BomOutput = GenerateBomText(schematic);
            DrcOutput = $"PCB imported from schematic '{schematic.Title}'.\n" +
                        $"{_pcbDoc.Footprints.Count} footprints placed in auto-grid layout.\n" +
                        $"{CanvasRatsnestLines.Count} ratsnest routing lines generated.\n" +
                        $"Drag components to reposition them and see ratsnest rubber-banding.\n" +
                        $"Run DRC to validate the design.";
            PcbTitle = _pcbDoc.Title;
        }

        [RelayCommand]
        private void AutoRouteBoard()
        {
            if (_pcbDoc == null || _pcbDoc.Footprints.Count == 0)
            {
                System.Windows.MessageBox.Show("Please import a schematic first.", "No Design", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Clear existing traces/vias
            _pcbDoc.Traces.Clear();
            _pcbDoc.Vias.Clear();
            CanvasTraces.Clear();
            CanvasVias.Clear();

            // Populate from ratsnest lines
            foreach (var rLine in _pcbDoc.Ratsnest)
            {
                var fromFp = _pcbDoc.Footprints.FirstOrDefault(f => f.Designator == rLine.FromDesignator);
                var toFp = _pcbDoc.Footprints.FirstOrDefault(f => f.Designator == rLine.ToDesignator);
                if (fromFp == null || toFp == null) continue;

                var fromPad = fromFp.Pads.FirstOrDefault(p => p.PadNumber == rLine.FromPadNumber);
                var toPad = toFp.Pads.FirstOrDefault(p => p.PadNumber == rLine.ToPadNumber);
                if (fromPad == null || toPad == null) continue;

                // Calculate pad center coordinates in mm
                double fromRad = fromFp.Rotation * Math.PI / 180.0;
                double fx = fromPad.X * Math.Cos(fromRad) - fromPad.Y * Math.Sin(fromRad);
                double fy = fromPad.X * Math.Sin(fromRad) + fromPad.Y * Math.Cos(fromRad);
                double x1 = fromFp.X + fx;
                double y1 = fromFp.Y + fy;

                double toRad = toFp.Rotation * Math.PI / 180.0;
                double tx = toPad.X * Math.Cos(toRad) - toPad.Y * Math.Sin(toRad);
                double ty = toPad.X * Math.Sin(toRad) + toPad.Y * Math.Cos(toRad);
                double x2 = toFp.X + tx;
                double y2 = toFp.Y + ty;

                // Route from (x1, y1) to (x2, y2)
                if (Math.Abs(x1 - x2) < 0.1 || Math.Abs(y1 - y2) < 0.1)
                {
                    var trace = new PcbTrace
                    {
                        StartX = x1,
                        StartY = y1,
                        EndX = x2,
                        EndY = y2,
                        Width_mm = _pcbDoc.Rules.DefaultTraceWidth_mm,
                        Layer = PcbLayerType.FCu,
                        NetName = rLine.NetName
                    };
                    _pcbDoc.Traces.Add(trace);
                    CanvasTraces.Add(new PcbTraceVM(trace));
                }
                else
                {
                    // L-bend routing: horizontal on F.Cu, vertical on B.Cu, connected with a via at (x2, y1)
                    var traceH = new PcbTrace
                    {
                        StartX = x1,
                        StartY = y1,
                        EndX = x2,
                        EndY = y1,
                        Width_mm = _pcbDoc.Rules.DefaultTraceWidth_mm,
                        Layer = PcbLayerType.FCu,
                        NetName = rLine.NetName
                    };
                    _pcbDoc.Traces.Add(traceH);
                    CanvasTraces.Add(new PcbTraceVM(traceH));

                    var via = new PcbVia
                    {
                        X = x2,
                        Y = y1,
                        DrillDia_mm = _pcbDoc.Rules.DefaultViaDrill_mm,
                        PadDia_mm = _pcbDoc.Rules.DefaultViaPad_mm,
                        LayerFrom = PcbLayerType.FCu,
                        LayerTo = PcbLayerType.BCu,
                        NetName = rLine.NetName
                    };
                    _pcbDoc.Vias.Add(via);
                    CanvasVias.Add(new PcbViaVM(via));

                    var traceV = new PcbTrace
                    {
                        StartX = x2,
                        StartY = y1,
                        EndX = x2,
                        EndY = y2,
                        Width_mm = _pcbDoc.Rules.DefaultTraceWidth_mm,
                        Layer = PcbLayerType.BCu,
                        NetName = rLine.NetName
                    };
                    _pcbDoc.Traces.Add(traceV);
                    CanvasTraces.Add(new PcbTraceVM(traceV));
                }
            }

            // Clear the ratsnest as everything is now routed
            _pcbDoc.Ratsnest.Clear();
            CanvasRatsnestLines.Clear();

            // Run DRC automatically
            RunPcbDrc();

            System.Windows.MessageBox.Show("PCB layout successfully auto-routed with dual-layer orthogonal tracks!", "Auto-Route Complete",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        /// <summary>
        /// Professional autorouting via FreeRouting JAR (open-source autorouter).
        /// Requires Java 11+ on PATH and FreeRouting JAR configured in Preferences.
        /// </summary>
        [RelayCommand]
        private async System.Threading.Tasks.Task AutoRouteFreeRoutingAsync()
        {
            if (_pcbDoc == null || _pcbDoc.Footprints.Count == 0)
            {
                System.Windows.MessageBox.Show("Please import a schematic first.", "No Design",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (IsAutoRouting) return;

            IsAutoRouting   = true;
            AutoRouteStatus = "Exporting DSN to FreeRouting…";

            _routingCts = new CancellationTokenSource();

            try
            {
                // Sync board outline dimensions from UI before exporting
                _pcbDoc.Outline = new PcbBoardOutline { Width_mm = BoardWidth, Height_mm = BoardHeight };

                AutoRouteStatus = "FreeRouting in progress… (may take 30–120 sec)";
                var result = await FreeRoutingService.RouteAsync(_pcbDoc, _routingCts.Token);

                if (result.Success)
                {
                    // Rebuild canvas trace/via collections from the newly populated PcbDocument
                    CanvasTraces.Clear();
                    CanvasVias.Clear();
                    CanvasRatsnestLines.Clear();

                    foreach (var trace in _pcbDoc.Traces)
                        CanvasTraces.Add(new PcbTraceVM(trace));
                    foreach (var via in _pcbDoc.Vias)
                        CanvasVias.Add(new PcbViaVM(via));

                    AutoRouteStatus = $"Complete — {result.RoutedSegments} segments routed";
                    RunPcbDrc();

                    System.Windows.MessageBox.Show(
                        $"FreeRouting completed successfully!\n" +
                        $"{result.RoutedSegments} route segments imported.\n\n" +
                        $"DRC Errors: {DrcErrors}  |  Warnings: {DrcWarnings}",
                        "FreeRouting Complete",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    AutoRouteStatus = "Failed — see log";
                    DrcOutput = $"[FreeRouting FAILED]\n{result.ErrorMessage}\n\n[Log]\n{result.Log}";
                    System.Windows.MessageBox.Show(
                        result.ErrorMessage, "FreeRouting Failed",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            finally
            {
                IsAutoRouting = false;
                _routingCts?.Dispose();
                _routingCts = null;
            }
        }

        [RelayCommand]
        private void StopAutoRoute()
        {
            _routingCts?.Cancel();
            AutoRouteStatus = "Cancelling…";
        }

        /// <summary>Exports the current PCB design as a Specctra .dsn file (for manual FreeRouting use).</summary>
        [RelayCommand]
        private void ExportDsn()
        {
            if (_pcbDoc == null || _pcbDoc.Footprints.Count == 0)
            {
                System.Windows.MessageBox.Show("Please import a schematic first.", "No Design",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title      = "Export Specctra DSN File",
                Filter     = "Specctra Design (*.dsn)|*.dsn|All Files (*.*)|*.*",
                DefaultExt = ".dsn",
                FileName   = _pcbDoc.Title + ".dsn"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                _pcbDoc.Outline = new PcbBoardOutline { Width_mm = BoardWidth, Height_mm = BoardHeight };
                string dsnContent = SpecctraDsnExporter.Export(_pcbDoc);
                System.IO.File.WriteAllText(dlg.FileName, dsnContent);
                DrcOutput = $"DSN exported to:\n{dlg.FileName}\n\n" +
                            $"You can now run FreeRouting manually:\n" +
                            $"  java -jar freerouting.jar -de \"{dlg.FileName}\" -do board.ses -mp 50";
                System.Windows.MessageBox.Show($"Specctra DSN exported to:\n{dlg.FileName}",
                    "Export Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"DSN export failed:\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>Imports a previously generated FreeRouting .ses session file.</summary>
        [RelayCommand]
        private void ImportSes()
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Import FreeRouting Session (.ses)",
                Filter = "Specctra Session (*.ses)|*.ses|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                int count = SpecctraSessionImporter.Import(dlg.FileName, _pcbDoc);

                CanvasTraces.Clear();
                CanvasVias.Clear();
                CanvasRatsnestLines.Clear();

                foreach (var trace in _pcbDoc.Traces)
                    CanvasTraces.Add(new PcbTraceVM(trace));
                foreach (var via in _pcbDoc.Vias)
                    CanvasVias.Add(new PcbViaVM(via));

                RunPcbDrc();
                System.Windows.MessageBox.Show($"SES imported: {count} route segments loaded.\nDRC Errors: {DrcErrors}",
                    "SES Import Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"SES import failed:\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void RunPcbDrc()
        {
            _pcbDoc.Outline = new PcbBoardOutline { Width_mm = BoardWidth, Height_mm = BoardHeight };
            var result = _drcEngine.RunDrc(_pcbDoc);

            DrcPassed   = result.Passed;
            DrcErrors   = result.TotalErrors;
            DrcWarnings = result.TotalWarnings;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"PCB DESIGN RULE CHECK — {(result.Passed ? "PASSED" : "FAILED")}");
            sb.AppendLine(new string('━', 50));
            sb.AppendLine($"Errors:   {result.TotalErrors}");
            sb.AppendLine($"Warnings: {result.TotalWarnings}");
            sb.AppendLine();

            foreach (var v in result.Violations)
            {
                sb.AppendLine($"[{v.Severity.ToString().ToUpper()}] {v.Rule}");
                sb.AppendLine($"  {v.Message}");
                if (v.X != 0 || v.Y != 0) sb.AppendLine($"  @ ({v.X:F2}, {v.Y:F2}) mm");
            }

            if (result.Violations.Count == 0)
            {
                sb.AppendLine("No DRC violations found.");
                sb.AppendLine("\nDesign is ready for manufacturing.");
            }

            sb.AppendLine();
            sb.AppendLine("Reference: IPC-2221B Generic Standard on Printed Board Design");
            DrcOutput = sb.ToString();
        }

        [RelayCommand]
        private void ExportGerber()
        {
            var dlg = new SaveFileDialog
            {
                Title       = "Select Output Folder for Gerber Files",
                Filter      = "Gerber ZIP Archive (*.zip)|*.zip",
                DefaultExt  = ".zip",
                FileName    = _pcbDoc.Title + "_Gerbers"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var writer = new GerberWriter();
                var files  = writer.GenerateAllLayers(_pcbDoc);
                var dir    = System.IO.Path.GetDirectoryName(dlg.FileName)!;
                var prefix = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);

                foreach (var (filename, content) in files)
                    System.IO.File.WriteAllText(System.IO.Path.Combine(dir, filename), content);

                DrcOutput = $"Gerber files exported to:\n{dir}\n\nFiles generated:\n" +
                            string.Join("\n", files.Keys.Select(f => "  - " + f));

                System.Windows.MessageBox.Show(
                    $"Gerber files exported successfully to:\n{dir}",
                    "Export Complete", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ExportBom(Schematic? schematic)
        {
            if (schematic == null) return;

            var dlg = new SaveFileDialog
            {
                Title      = "Export Bill of Materials",
                Filter     = "CSV (*.csv)|*.csv|All Files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName   = schematic.Title + "_BOM"
            };

            if (dlg.ShowDialog() != true) return;

            var bom = BomGenerator.GenerateBom(schematic);
            var csv = BomGenerator.ToCsv(bom);
            System.IO.File.WriteAllText(dlg.FileName, csv);

            System.Windows.MessageBox.Show(
                $"BOM exported: {bom.Count} line items\n{dlg.FileName}",
                "BOM Exported", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ExportPickAndPlace()
        {
            var dlg = new SaveFileDialog
            {
                Title      = "Export Pick and Place Centroid File",
                Filter     = "CSV (*.csv)|*.csv|All Files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName   = _pcbDoc.Title + "_PickAndPlace"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var csv = CentroidExporter.GeneratePickAndPlace(_pcbDoc);
                System.IO.File.WriteAllText(dlg.FileName, csv);

                System.Windows.MessageBox.Show(
                    $"Pick & Place data exported successfully:\n{dlg.FileName}",
                    "Export Complete", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private string SuggestFootprintId(Component comp) => comp.GetType().Name switch
        {
            "Resistor"  or "Capacitor" => "R_0402_1005Metric",
            "Inductor"  => "L_0402_1005Metric",
            "Diode"     => "D_SOD-323",
            "BJT"       => "SOT-23",
            "MOSFET"    => "SOT-23",
            "OpAmp"     => "SOIC-8_3.9x4.9mm_P1.27mm",
            _           => "Generic"
        };

        private IEnumerable<PcbPad> GeneratePads(Component comp)
        {
            int padCount = comp.GetType().Name switch
            {
                "Resistor" or "Capacitor" or "Inductor" or "Diode" => 2,
                "BJT" or "MOSFET"  => 3,
                "OpAmp"            => 8,
                _                  => comp.Pins.Count
            };

            for (int i = 1; i <= padCount; i++)
            {
                yield return new PcbPad
                {
                    PadNumber   = i.ToString(),
                    Type        = PadType.SMD,
                    X           = (i - 1) * 1.27 - (padCount - 1) * 0.635,
                    Y           = 0,
                    Width_mm    = 0.9,
                    Height_mm   = 1.4,
                    Layer       = PcbLayerType.FCu
                };
            }
        }

        private string GenerateBomText(Schematic schematic)
        {
            var bom = BomGenerator.GenerateBom(schematic);
            var sb  = new System.Text.StringBuilder();
            sb.AppendLine($"BILL OF MATERIALS — {schematic.Title}");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine(new string('━', 60));
            sb.AppendLine($"{"#",-4} {"Qty",-5} {"Designators",-20} {"Type",-12} {"Value",-12} {"Package"}");
            sb.AppendLine(new string('─', 60));

            foreach (var item in bom)
            {
                string desig = string.Join(",", item.Designators.Take(4));
                if (item.Designators.Count > 4) desig += "...";
                sb.AppendLine($"{item.LineNumber,-4} {item.Quantity,-5} {desig,-20} {item.ComponentType,-12} {item.Value,-12} {item.Package}");
            }

            sb.AppendLine(new string('─', 60));
            sb.AppendLine($"Total line items: {bom.Count} | Total components: {bom.Sum(b => b.Quantity)}");
            return sb.ToString();
        }
    }

    public class PadVisualItem
    {
        public string Number { get; set; } = "";
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Color { get; set; } = "#FFCC3333";
    }

    /// <summary>Simple display model for canvas-rendered footprint boxes.</summary>
    public partial class PcbFootprintVM : ObservableObject
    {
        public PcbFootprint Model { get; }
        private readonly Action? _movedCallback;

        [ObservableProperty] private string _designator = "";
        [ObservableProperty] private string _value = "";
        [ObservableProperty] private double _canvasX;
        [ObservableProperty] private double _canvasY;
        [ObservableProperty] private double _width = 40;
        [ObservableProperty] private double _height = 30;
        [ObservableProperty] private string _layerColor = "#FF007ACC";

        public List<PadVisualItem> VisualPads { get; } = new();

        public PcbFootprintVM(PcbFootprint model, Action? movedCallback = null)
        {
            Model = model;
            _movedCallback = movedCallback;
            _designator = model.Designator;
            _value = model.Value;
            _canvasX = (model.X - model.CrtYd_Width_mm / 2.0) * 5.0; // center to top-left translation
            _canvasY = (model.Y - model.CrtYd_Height_mm / 2.0) * 5.0;
            _width = model.CrtYd_Width_mm * 5.0;
            _height = model.CrtYd_Height_mm * 5.0;
            _layerColor = "#FF007ACC";

            double cx = model.CrtYd_Width_mm / 2.0;
            double cy = model.CrtYd_Height_mm / 2.0;
            foreach (var pad in model.Pads)
            {
                VisualPads.Add(new PadVisualItem
                {
                    Number = pad.PadNumber,
                    Left = (cx + pad.X - pad.Width_mm / 2.0) * 5.0,
                    Top = (cy + pad.Y - pad.Height_mm / 2.0) * 5.0,
                    Width = pad.Width_mm * 5.0,
                    Height = pad.Height_mm * 5.0,
                    Color = pad.Type == PadType.SMD ? "#FFFF5555" : "#FFFFCC33" // Red for SMD pads, Yellow/Orange for THT pads
                });
            }
        }

        partial void OnCanvasXChanged(double value) => _movedCallback?.Invoke();
        partial void OnCanvasYChanged(double value) => _movedCallback?.Invoke();
    }

    public partial class PcbRatsnestLineVM : ObservableObject
    {
        [ObservableProperty] private double _x1;
        [ObservableProperty] private double _y1;
        [ObservableProperty] private double _x2;
        [ObservableProperty] private double _y2;

        public string NetName { get; set; } = "";
        public string FromDesignator { get; set; } = "";
        public string FromPadNumber { get; set; } = "";
        public string ToDesignator { get; set; } = "";
        public string ToPadNumber { get; set; } = "";
    }

    public partial class PcbTraceVM : ObservableObject
    {
        public PcbTrace Model { get; }

        [ObservableProperty] private double _x1;
        [ObservableProperty] private double _y1;
        [ObservableProperty] private double _x2;
        [ObservableProperty] private double _y2;
        [ObservableProperty] private double _thickness = 1.25;
        [ObservableProperty] private string _layerColor = "#FFCC3333";
        [ObservableProperty] private string _netName = "";

        public PcbTraceVM(PcbTrace model)
        {
            Model = model;
            _x1 = model.StartX * 5.0;
            _y1 = model.StartY * 5.0;
            _x2 = model.EndX * 5.0;
            _y2 = model.EndY * 5.0;
            _thickness = model.Width_mm * 5.0;
            _netName = model.NetName;
            _layerColor = model.Layer == PcbLayerType.FCu ? "#FFFF3333" : "#FF3333FF"; // Red for F.Cu, Blue for B.Cu
        }
    }

    public partial class PcbViaVM : ObservableObject
    {
        public PcbVia Model { get; }

        [ObservableProperty] private double _x;
        [ObservableProperty] private double _y;
        [ObservableProperty] private double _outerDia;
        [ObservableProperty] private double _innerDia;
        [ObservableProperty] private string _netName = "";

        public double LeftOuter => X - OuterDia / 2.0;
        public double TopOuter => Y - OuterDia / 2.0;
        public double LeftInner => X - InnerDia / 2.0;
        public double TopInner => Y - InnerDia / 2.0;

        public PcbViaVM(PcbVia model)
        {
            Model = model;
            _x = model.X * 5.0;
            _y = model.Y * 5.0;
            _outerDia = model.PadDia_mm * 5.0;
            _innerDia = model.DrillDia_mm * 5.0;
            _netName = model.NetName;
        }
    }
}
