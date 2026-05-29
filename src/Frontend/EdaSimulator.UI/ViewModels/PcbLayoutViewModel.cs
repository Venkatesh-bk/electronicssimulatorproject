using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
        }

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

        public List<string> AvailableLayers => new()
        {
            "F.Cu", "B.Cu", "F.SilkS", "B.SilkS",
            "F.Mask", "B.Mask", "F.Paste", "B.Paste",
            "Edge.Cuts", "In1.Cu", "In2.Cu"
        };

        // ── Canvas Footprints (observable for UI) ────────────────────────────────────

        public ObservableCollection<PcbFootprintVM> CanvasFootprints { get; } = new();

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

                CanvasFootprints.Add(new PcbFootprintVM(fp));
            }

            BomOutput = GenerateBomText(schematic);
            DrcOutput = $"PCB imported from schematic '{schematic.Title}'.\n" +
                        $"{_pcbDoc.Footprints.Count} footprints placed in auto-grid layout.\n" +
                        $"Drag components to reposition them.\n" +
                        $"Run DRC to validate the design.";
            PcbTitle = _pcbDoc.Title;
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

    /// <summary>Simple display model for canvas-rendered footprint boxes.</summary>
    public partial class PcbFootprintVM : ObservableObject
    {
        public PcbFootprint Model { get; }

        [ObservableProperty] private string _designator = "";
        [ObservableProperty] private string _value = "";
        [ObservableProperty] private double _canvasX;
        [ObservableProperty] private double _canvasY;
        [ObservableProperty] private double _width = 40;
        [ObservableProperty] private double _height = 30;
        [ObservableProperty] private string _layerColor = "#FF007ACC";

        public PcbFootprintVM(PcbFootprint model)
        {
            Model = model;
            _designator = model.Designator;
            _value = model.Value;
            _canvasX = model.X * 5; // Scale 5px/mm for display
            _canvasY = model.Y * 5;
            _width = model.CrtYd_Width_mm * 8; // Scale for visual display
            _height = model.CrtYd_Height_mm * 8;
            _layerColor = "#FF007ACC";
        }
    }
}
