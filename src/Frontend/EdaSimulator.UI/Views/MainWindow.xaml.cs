using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.Models.Components;
using EdaSimulator.Engines.Simulation.Digital;
using EdaSimulator.UI.Commands;
using EdaSimulator.UI.ViewModels;

namespace EdaSimulator.UI.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Wire up the MVVM DataContext
            DataContext = new MainViewModel();
        }

        private MainViewModel? ViewModel => DataContext as MainViewModel;
        private ViewModels.Canvas.SchematicViewModel? CanvasViewModel => ViewModel?.ActiveSchematicViewModel;

        private static readonly System.Collections.Generic.List<ViewModels.Canvas.ComponentNodeViewModel> _copiedComponents = new();

        // --- Viewport Physics and Routing Intercepts ---
        private const double GRID_SIZE = 10.0;
        private double Snap(double value) => Math.Round(value / GRID_SIZE) * GRID_SIZE;

        private Point ApplyGravity(Point pos)
        {
            if (CanvasViewModel == null) return new Point(Snap(pos.X), Snap(pos.Y));

            double gravityThreshold = 15.0; // Magnetic Radius
            foreach (var item in CanvasViewModel.Items)
            {
                if (item is ViewModels.Canvas.PinNodeViewModel pin)
                {
                    double dx = pos.X - pin.X;
                    double dy = pos.Y - pin.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist < gravityThreshold)
                    {
                        return new Point(pin.X, pin.Y); // Snap exact
                    }
                }
            }

            return new Point(Snap(pos.X), Snap(pos.Y));
        }

        private Point? _panStartPoint;
        private double _panStartX;
        private double _panStartY;

        // PCB Footprint dragging state
        private bool _isDraggingPcbFootprint;
        private Point _pcbDragStartMouse;
        private double _pcbDragStartCanvasX;
        private double _pcbDragStartCanvasY;
        private PcbFootprintVM? _draggedPcbFootprint;

        private void SchematicCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (CanvasViewModel == null) return;

            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                // Initiate Canvas Panning
                _panStartPoint = e.GetPosition(MainScrollViewer);
                _panStartX = CanvasViewModel.PanX;
                _panStartY = CanvasViewModel.PanY;
                SchematicItemsControl.CaptureMouse();
                return;
            }

            if (CanvasViewModel.ActiveTool == null) return;

            var pos = e.GetPosition(SchematicItemsControl);
            var target = (e.OriginalSource as FrameworkElement)?.DataContext as ViewModels.Canvas.CanvasItemViewModel;

            // Enforce Grid Snapping natively against the core abstraction layer
            var grav = ApplyGravity(pos);
            CanvasViewModel.ActiveTool.OnPointerDown(grav.X, grav.Y, target!);
        }

        private void SchematicCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (CanvasViewModel == null) return;

            if (_panStartPoint.HasValue)
            {
                var curPos = e.GetPosition(MainScrollViewer);
                var dx = curPos.X - _panStartPoint.Value.X;
                var dy = curPos.Y - _panStartPoint.Value.Y;

                CanvasViewModel.PanX = _panStartX + dx;
                CanvasViewModel.PanY = _panStartY + dy;
                return;
            }

            if (CanvasViewModel.ActiveTool == null) return;
            
            var pos = e.GetPosition(SchematicItemsControl);
            var grav = ApplyGravity(pos);
            CanvasViewModel.ActiveTool.OnPointerMove(grav.X, grav.Y);
        }

        private void SchematicCanvas_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (CanvasViewModel == null) return;

            if (_panStartPoint.HasValue)
            {
                _panStartPoint = null;
                SchematicItemsControl.ReleaseMouseCapture();
                return;
            }

            if (CanvasViewModel.ActiveTool == null) return;
            
            var pos = e.GetPosition(SchematicItemsControl);
            var grav = ApplyGravity(pos);
            CanvasViewModel.ActiveTool.OnPointerUp(grav.X, grav.Y);
        }

        private void SchematicCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (CanvasViewModel == null) return;

            // Zoom In / Zoom Out mapping
            double zoomMod = e.Delta > 0 ? 1.1 : 0.9;
            CanvasViewModel.ZoomFactor = Math.Max(0.2, Math.Min(5.0, CanvasViewModel.ZoomFactor * zoomMod));
            
            e.Handled = true;
        }

        // --- Drag and Drop Toolbox Integration ---

        private Point _dragStartPoint;

        private void ToolboxList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void ToolboxList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _dragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (ToolboxList.SelectedItem is ListBoxItem item)
                    {
                        string? componentType = item.Tag as string;
                        if (!string.IsNullOrEmpty(componentType))
                        {
                            DragDrop.DoDragDrop(ToolboxList, componentType, DragDropEffects.Copy);
                        }
                    }
                }
            }
        }

        private string GetNextDesignator(string prefix)
        {
            if (CanvasViewModel == null) return prefix + "1";
            
            int maxId = 0;
            foreach (var item in CanvasViewModel.Items)
            {
                if (item is ViewModels.Canvas.ComponentNodeViewModel comp && comp.Designator.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string numericPart = comp.Designator.Substring(prefix.Length);
                    if (int.TryParse(numericPart, out int val))
                    {
                        if (val > maxId) maxId = val;
                    }
                }
            }
            return prefix + (maxId + 1);
        }

        private void SchematicCanvas_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string componentType = (string)e.Data.GetData(DataFormats.StringFormat);
                var pos = e.GetPosition(SchematicItemsControl);

                Component? coreComponent = null;

                switch (componentType)
                {
                    case "Resistor": coreComponent = new Resistor(GetNextDesignator("R"), "1k"); break;
                    case "Capacitor": coreComponent = new Capacitor(GetNextDesignator("C"), "1u"); break;
                    case "Inductor": coreComponent = new Inductor(GetNextDesignator("L"), "1m"); break;
                    case "VoltageSource": coreComponent = new VoltageSource(GetNextDesignator("V"), "DC 5"); break;
                    case "CurrentSource": coreComponent = new CurrentSource(GetNextDesignator("I"), "DC 1m"); break;
                    case "Switch": coreComponent = new Switch(GetNextDesignator("SW")); break;
                    case "Potentiometer": coreComponent = new Potentiometer(GetNextDesignator("POT"), "10k"); break;
                    
                    case "Diode": coreComponent = new Diode(GetNextDesignator("D"), "1N4148"); break;
                    case "BJT": coreComponent = new BJT(GetNextDesignator("Q"), "2N2222"); break;
                    case "MOSFET": coreComponent = new MOSFET(GetNextDesignator("M"), "2N7002"); break;
                    case "OpAmp": coreComponent = new OpAmp(GetNextDesignator("X"), "LM358"); break;

                    // Sinusoidal voltage source: SIN(offset amplitude frequency)
                    case "SinVSource": coreComponent = new VoltageSource(GetNextDesignator("V"), "SIN(0 5 1k)"); break;

                    // Digital gates — mapped to generic 2-input logic gate subcircuit
                    // In SPICE they can reference 74HC logic family subcircuits
                    case "NandGate": coreComponent = new OpAmp(GetNextDesignator("U"), "74HC00"); break;
                    case "NorGate":  coreComponent = new OpAmp(GetNextDesignator("U"), "74HC02"); break;
                    case "XorGate":  coreComponent = new OpAmp(GetNextDesignator("U"), "74HC86"); break;

                    // Microcontrollers (MCU)
                    case "ArduinoUno": coreComponent = new McuComponent(GetNextDesignator("MCU"), "Arduino Uno R3"); break;
                    case "Esp32Wroom": coreComponent = new McuComponent(GetNextDesignator("MCU"), "ESP32-WROOM-32"); break;
                    case "Stm32BluePill": coreComponent = new McuComponent(GetNextDesignator("MCU"), "STM32 Blue Pill"); break;

                    // === Power Symbols ===
                    case "Ground": coreComponent = new GroundSymbol(GetNextDesignator("GND")); break;
                    case "VCC":    coreComponent = new PowerRail(GetNextDesignator("VCC"), 5.0); break;
                    case "VDD":    coreComponent = new PowerRail(GetNextDesignator("VDD"), 3.3); break;
                    case "V12":    coreComponent = new PowerRail(GetNextDesignator("V12"), 12.0); break;
                    case "VN12":   coreComponent = new PowerRail(GetNextDesignator("VN"), -12.0); break;

                    // === Mathematical Blocks ===
                    case "BlockGain": coreComponent = new BlockGainComponent(GetNextDesignator("XG"), "1.0"); break;
                    case "BlockIntegrator": coreComponent = new BlockIntegratorComponent(GetNextDesignator("XI"), "0.0"); break;
                    case "BlockSum": coreComponent = new BlockSumComponent(GetNextDesignator("XS"), "+-"); break;
                    case "BlockSource": coreComponent = new BlockSourceComponent(GetNextDesignator("XSO"), "Constant 1.0"); break;
                    case "BlockTransferFunction": coreComponent = new BlockTransferFunctionComponent(GetNextDesignator("XTF"), "1 / 1 1"); break;
                    case "AnnotationNote": coreComponent = new AnnotationNote(GetNextDesignator("NOTE"), "Double-click to edit note text"); break;
                }

                if (coreComponent != null && CanvasViewModel != null)
                {
                    var vm = new ViewModels.Canvas.ComponentNodeViewModel(coreComponent)
                    {
                        X = Snap(pos.X - 25),
                        Y = Snap(pos.Y - 25)
                    };

                    // Ground symbols auto-connect their single pin to net "0"
                    if (coreComponent is GroundSymbol gnd)
                    {
                        CanvasViewModel.CoreSchematic.ConnectPinToNet(
                            gnd.Pins[0], CanvasViewModel.CoreSchematic.MasterGroundNet.Id);
                    }

                    // Push to undo history so Ctrl+Z removes the placed component
                    var cmd = new AddComponentCommand(CanvasViewModel, vm);
                    CanvasViewModel.History.ExecuteCommand(cmd);
                }
            }
        }

        private void SchematicCanvas_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (CanvasViewModel == null) return;
            var target = (e.OriginalSource as System.Windows.FrameworkElement)?.DataContext
                         as ViewModels.Canvas.ComponentNodeViewModel;
            if (target == null) return;

            // Open inline property editor popup
            var dlg = new ComponentPropertyDialog(target);
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        private void SelectTool_Click(object sender, RoutedEventArgs e)
        {
            if (CanvasViewModel != null) CanvasViewModel.ActiveTool = new Tools.SelectionTool(CanvasViewModel);
        }

        private void WireTool_Click(object sender, RoutedEventArgs e)
        {
            if (CanvasViewModel != null) CanvasViewModel.ActiveTool = new Tools.WiringTool(CanvasViewModel);
        }

        private void ProbeTool_Click(object sender, RoutedEventArgs e)
        {
            if (CanvasViewModel != null) CanvasViewModel.ActiveTool = new Tools.ProbeTool(CanvasViewModel);
        }

        private void CurrentProbeTool_Click(object sender, RoutedEventArgs e)
        {
            if (CanvasViewModel != null) CanvasViewModel.ActiveTool = new Tools.CurrentProbeTool(CanvasViewModel);
        }

        private void NetLabelTool_Click(object sender, RoutedEventArgs e)
        {
            if (CanvasViewModel != null) CanvasViewModel.ActiveTool = new Tools.NetLabelTool(CanvasViewModel);
        }

        private void Undo_Click(object sender, RoutedEventArgs e) => CanvasViewModel?.History.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => CanvasViewModel?.History.Redo();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (CanvasViewModel == null) return;

            // Undo / Redo Global Hooks
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.Z)
                {
                    CanvasViewModel.History.Undo();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Y)
                {
                    CanvasViewModel.History.Redo();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.E && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                {
                    ExportSchematicPng();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.C)
                {
                    _copiedComponents.Clear();
                    foreach (var item in CanvasViewModel.Items)
                    {
                        if (item.IsSelected && item is ViewModels.Canvas.ComponentNodeViewModel comp)
                        {
                            _copiedComponents.Add(comp);
                        }
                    }
                    if (_copiedComponents.Count > 0)
                    {
                        if (ViewModel != null)
                            ViewModel.StatusText = $"{_copiedComponents.Count} component(s) copied to clipboard.";
                        e.Handled = true;
                        return;
                    }
                }
                if (e.Key == Key.V)
                {
                    if (_copiedComponents.Count > 0)
                    {
                        var pastedList = new System.Collections.Generic.List<ViewModels.Canvas.ComponentNodeViewModel>();

                        foreach (var copied in _copiedComponents)
                        {
                            var sourceCore = copied.CoreComponent;
                            // Extract alphabetical prefix
                            string prefix = "";
                            foreach (char c in sourceCore.Designator)
                            {
                                if (char.IsLetter(c)) prefix += c;
                                else break;
                            }
                            if (string.IsNullOrEmpty(prefix)) prefix = "U";
                            string newDesignator = GetNextDesignator(prefix);

                            Component? cloneCore = null;
                            if (sourceCore is CustomComponent custom)
                            {
                                cloneCore = new CustomComponent(newDesignator, custom.Value, custom.LibraryModel);
                            }
                            else if (sourceCore is PowerRail rail)
                            {
                                cloneCore = new PowerRail(newDesignator, rail.Voltage);
                            }
                            else
                            {
                                var type = sourceCore.GetType();
                                var constructors = type.GetConstructors();
                                var ctor = System.Linq.Enumerable.FirstOrDefault(constructors, c => {
                                    var pars = c.GetParameters();
                                    return pars.Length == 2 && pars[0].ParameterType == typeof(string) && pars[1].ParameterType == typeof(string);
                                });
                                if (ctor != null)
                                {
                                    cloneCore = (Component)ctor.Invoke(new object[] { newDesignator, sourceCore.Value });
                                }
                                else
                                {
                                    ctor = System.Linq.Enumerable.FirstOrDefault(constructors, c => {
                                        var pars = c.GetParameters();
                                        return pars.Length == 1 && pars[0].ParameterType == typeof(string);
                                    });
                                    if (ctor != null)
                                    {
                                        cloneCore = (Component)ctor.Invoke(new object[] { newDesignator });
                                    }
                                }
                            }

                            if (cloneCore != null)
                            {
                                // Copy component-specific dynamic states
                                if (sourceCore is Potentiometer srcPot && cloneCore is Potentiometer destPot)
                                {
                                    destPot.WiperPosition = srcPot.WiperPosition;
                                }
                                else if (sourceCore is Switch srcSw && cloneCore is Switch destSw)
                                {
                                    destSw.IsClosed = srcSw.IsClosed;
                                }
                                else if (sourceCore is McuComponent srcMcu && cloneCore is McuComponent destMcu)
                                {
                                    destMcu.FirmwarePath = srcMcu.FirmwarePath;
                                }

                                var cloneVm = new ViewModels.Canvas.ComponentNodeViewModel(cloneCore)
                                {
                                    X = Snap(copied.X + 40),
                                    Y = Snap(copied.Y + 40),
                                    RotationAngle = copied.RotationAngle,
                                    IsMirroredX = copied.IsMirroredX,
                                    IsMirroredY = copied.IsMirroredY
                                };
                                pastedList.Add(cloneVm);
                            }
                        }

                        if (pastedList.Count > 0)
                        {
                            foreach (var item in CanvasViewModel.Items)
                            {
                                item.IsSelected = false;
                            }

                            foreach (var pasted in pastedList)
                            {
                                pasted.IsSelected = true;
                                CanvasViewModel.History.ExecuteCommand(new AddComponentCommand(CanvasViewModel, pasted));
                            }

                            if (ViewModel != null)
                                ViewModel.StatusText = $"{pastedList.Count} component(s) pasted.";
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }

            // Delete / Backspace — remove selected canvas items via undoable command
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                var toDelete = new System.Collections.Generic.List<ViewModels.Canvas.CanvasItemViewModel>();
                foreach (var item in CanvasViewModel.Items)
                    if (item.IsSelected) toDelete.Add(item);

                foreach (var item in toDelete)
                    CanvasViewModel.History.ExecuteCommand(new DeleteItemCommand(CanvasViewModel, item));

                e.Handled = true;
                return;
            }

            // Tool Toggles
            if (e.Key == Key.P)
            {
                CanvasViewModel.ActiveTool = new Tools.ProbeTool(CanvasViewModel);
                return;
            }
            if (e.Key == Key.I)
            {
                CanvasViewModel.ActiveTool = new Tools.CurrentProbeTool(CanvasViewModel);
                return;
            }
            if (e.Key == Key.L)
            {
                CanvasViewModel.ActiveTool = new Tools.NetLabelTool(CanvasViewModel);
                return;
            }
            if (e.Key == Key.W)
            {
                CanvasViewModel.ActiveTool = new Tools.WiringTool(CanvasViewModel);
                return;
            }
            if (e.Key == Key.Escape)
            {
                CanvasViewModel.ActiveTool = new Tools.SelectionTool(CanvasViewModel);
                return;
            }

            foreach (var item in CanvasViewModel.Items)
            {
                if (item.IsSelected && item is ViewModels.Canvas.ComponentNodeViewModel comp)
                {
                    if (e.Key == Key.R)
                    {
                        comp.RotationAngle = (comp.RotationAngle + 90) % 360;
                    }
                    else if (e.Key == Key.X)
                    {
                        comp.IsMirroredX = !comp.IsMirroredX;
                    }
                    else if (e.Key == Key.Y)
                    {
                        comp.IsMirroredY = !comp.IsMirroredY;
                    }
                }
            }
                       // F2: Quick-Edit selected component value (industry standard)
            if (e.Key == Key.F2)
            {
                var selectedComp = CanvasViewModel?.Items
                    .OfType<ViewModels.Canvas.ComponentNodeViewModel>()
                    .FirstOrDefault(c => c.IsSelected);
                if (selectedComp != null)
                {
                    var dlg = new ComponentPropertyDialog(selectedComp);
                    dlg.Owner = this;
                    dlg.ShowDialog();
                    e.Handled = true;
                    return;
                }
            }

            // ── Phase 7: Global Keyboard Shortcuts ───────────────────────────────────
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.S)
                {
                    ViewModel?.SaveProjectCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.O)
                {
                    ViewModel?.LoadProjectCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.N)
                {
                    ViewModel?.NewProjectCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                // Ctrl+Shift+F: Zoom to Fit all components
                if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                {
                    ZoomToFit();
                    e.Handled = true;
                    return;
                }
            }
        }

        private void ZoomToFit()
        {
            if (CanvasViewModel == null) return;
            var components = CanvasViewModel.Items.OfType<ViewModels.Canvas.ComponentNodeViewModel>().ToList();
            if (components.Count == 0) return;

            double minX = components.Min(c => c.X);
            double minY = components.Min(c => c.Y);
            double maxX = components.Max(c => c.X + c.BoundsWidth);
            double maxY = components.Max(c => c.Y + c.BoundsHeight);

            double contentWidth  = maxX - minX + 100;
            double contentHeight = maxY - minY + 100;

            double viewWidth  = MainScrollViewer.ActualWidth  > 0 ? MainScrollViewer.ActualWidth  : 800;
            double viewHeight = MainScrollViewer.ActualHeight > 0 ? MainScrollViewer.ActualHeight : 600;

            double scaleX = viewWidth  / contentWidth;
            double scaleY = viewHeight / contentHeight;
            double zoom   = Math.Min(Math.Min(scaleX, scaleY), 2.0);
            zoom          = Math.Max(zoom, 0.1);

            CanvasViewModel.ZoomFactor = zoom;
            CanvasViewModel.PanX = -(minX - 50) * zoom;
            CanvasViewModel.PanY = -(minY - 50) * zoom;

            ViewModel?.StatusText?.ToString(); // trigger status update
            if (ViewModel != null)
                ViewModel.StatusText = $"Zoom to fit: {zoom:P0}  |  Bounds: {contentWidth:F0}×{contentHeight:F0} px";
        }

        // ── Phase 7: Menu Click Handlers ─────────────────────────────────────────────

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (CanvasViewModel != null)
                CanvasViewModel.ZoomFactor = Math.Min(CanvasViewModel.ZoomFactor * 1.25, 10.0);
        }

        private void MenuZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (CanvasViewModel != null)
                CanvasViewModel.ZoomFactor = Math.Max(CanvasViewModel.ZoomFactor / 1.25, 0.1);
        }

        private void MenuZoomReset_Click(object sender, RoutedEventArgs e)
        {
            if (CanvasViewModel != null)
            {
                CanvasViewModel.ZoomFactor = 1.0;
                CanvasViewModel.PanX = 0;
                CanvasViewModel.PanY = 0;
            }
        }

        private void MenuZoomFit_Click(object sender, RoutedEventArgs e) => ZoomToFit();

        private void MenuPcbDrc_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.PcbVM == null) return;
            ViewModel.PcbVM.RunPcbDrcPublic();
            // Switch to PCB tab to show results
            MessageBox.Show(
                $"PCB DRC complete.\nErrors: {ViewModel.PcbVM.DrcErrors}  Warnings: {ViewModel.PcbVM.DrcWarnings}\n\n" +
                $"Details shown in the PCB Layout → DRC tab.",
                "PCB DRC Results",
                MessageBoxButton.OK,
                ViewModel.PcbVM.DrcErrors == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "EDA Simulator Platform\nProfessional Electronic Design Automation\n\n" +
                "Phase 7: Professional UX & Workflow\n" +
                "Capabilities: Schematic Capture, SPICE Simulation,\n" +
                "GPU-Accelerated Monte Carlo, AI/ML Research Database\n\n" +
                "Knowledge Base: 6-Volume Physics Library (High School → PhD)\n" +
                "Research PKL: 118 MB compressed EDA dataset\n\n" +
                "Matching: Proteus Professional + MATLAB/Simulink level",
                "About EDA Simulator", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            bool? result = settingsWindow.ShowDialog();
            // After settings close, refresh FreeRouting availability in PCB VM
            if (result == true && DataContext is MainViewModel mv)
                mv.PcbVM.RefreshFreeRoutingAvailability();
        }

        private void MenuExportPng_Click(object sender, RoutedEventArgs e) => ExportSchematicPng();

        private void MenuExportSvg_Click(object sender, RoutedEventArgs e) => ExportSchematicSvg();

        private void ExportSchematicSvg()
        {
            var vm = CanvasViewModel;
            if (vm == null) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title      = "Export Schematic as SVG",
                Filter     = "SVG Image (*.svg)|*.svg|All Files (*.*)|*.*",
                DefaultExt = ".svg",
                FileName   = (vm.CoreSchematic.Title ?? "Schematic") + ".svg"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var svg = EdaSimulator.UI.IO.SvgExporter.Export(vm);
                System.IO.File.WriteAllText(dlg.FileName, svg);

                MessageBox.Show($"Schematic exported to:\n{dlg.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SVG export failed:\n{ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportSchematicPng()
        {
            if (SchematicItemsControl == null) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title      = "Export Schematic as PNG",
                Filter     = "PNG Image (*.png)|*.png|All Files (*.*)|*.*",
                DefaultExt = ".png",
                FileName   = (CanvasViewModel?.CoreSchematic.Title ?? "Schematic") + ".png"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                // Measure/arrange the canvas at its full desired size
                SchematicItemsControl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                SchematicItemsControl.Arrange(new Rect(SchematicItemsControl.DesiredSize));
                SchematicItemsControl.UpdateLayout();

                double dpi = 150;
                double w   = Math.Max(SchematicItemsControl.ActualWidth,  200);
                double h   = Math.Max(SchematicItemsControl.ActualHeight, 200);

                var rtb = new RenderTargetBitmap(
                    (int)(w * dpi / 96), (int)(h * dpi / 96),
                    dpi, dpi, PixelFormats.Pbgra32);

                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    // Dark background matching the canvas
                    dc.DrawRectangle(
                        new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x1A)),
                        null,
                        new Rect(0, 0, w, h));
                    var vb = new VisualBrush(SchematicItemsControl);
                    dc.DrawRectangle(vb, null, new Rect(0, 0, w, h));
                }
                rtb.Render(visual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using var stream = System.IO.File.Create(dlg.FileName);
                encoder.Save(stream);

                MessageBox.Show($"Schematic exported to:\n{dlg.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PNG export failed:\n{ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuHelp_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new HelpWindow();
            helpWindow.Owner = this;
            helpWindow.ShowDialog();
        }

        private void MenuActivation_Click(object sender, RoutedEventArgs e)
        {
            var activationWindow = new ActivationWindow();
            activationWindow.Owner = this;
            activationWindow.ShowDialog();
        }

        private void MenuComponentHub_Click(object sender, RoutedEventArgs e)
        {
            var hubWindow = new ComponentHubWindow();
            hubWindow.Owner = this;
            hubWindow.ShowDialog();

            if (hubWindow.DataContext is ViewModels.ComponentHubViewModel vm && !string.IsNullOrEmpty(vm.PlacedComponentId))
            {
                PlaceLibraryComponent(vm.PlacedComponentId);
            }
        }

        private void PlaceLibraryComponent(string componentId)
        {
            var libComp = EdaSimulator.Engines.Library.ComponentLibraryService.Instance.GetComponentById(componentId);
            if (libComp == null || CanvasViewModel == null || ViewModel == null) return;

            string prefix = "U";
            string cat = libComp.Category.ToLowerInvariant();
            if (cat.Contains("diode")) prefix = "D";
            else if (cat.Contains("npn") || cat.Contains("pnp") || cat.Contains("mosfet") || cat.Contains("transistor")) prefix = "Q";
            else if (cat.Contains("op-amp") || cat.Contains("amplifier") || cat.Contains("ic")) prefix = "U";

            string designator = GetNextDesignator(prefix);
            var coreComp = new EdaSimulator.Engines.Models.Components.CustomComponent(designator, libComp.Name, libComp);

            var vm = new ViewModels.Canvas.ComponentNodeViewModel(coreComp)
            {
                X = Snap(150),
                Y = Snap(150)
            };

            var cmd = new AddComponentCommand(CanvasViewModel, vm);
            CanvasViewModel.History.ExecuteCommand(cmd);
            ViewModel.StatusText = $"Placed {libComp.Name} ({designator}) on schematic.";
        }

        private void MenuKnowledgeBase_Click(object sender, RoutedEventArgs e)
        {
            var kbPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!
                    .Replace("bin\\Debug\\net8.0-windows", "").Replace("src\\Frontend\\EdaSimulator.UI\\", ""),
                "KnowledgeBase");

            if (System.IO.Directory.Exists(kbPath))
                System.Diagnostics.Process.Start("explorer.exe", kbPath);
            else
                MessageBox.Show($"Knowledge Base folder not found at:\n{kbPath}", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // ── PCB Footprint Dragging Event Handlers ────────────────────────────────────

        private void PcbFootprint_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                PcbFootprint_MouseDoubleClick(sender, e);
                return;
            }

            if (sender is FrameworkElement fe && fe.DataContext is PcbFootprintVM fpVM)
            {
                _isDraggingPcbFootprint = true;
                _draggedPcbFootprint = fpVM;
                _pcbDragStartMouse = e.GetPosition(this);
                _pcbDragStartCanvasX = fpVM.CanvasX;
                _pcbDragStartCanvasY = fpVM.CanvasY;
                fe.CaptureMouse();
                e.Handled = true;
            }
        }

        private void PcbFootprint_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingPcbFootprint && _draggedPcbFootprint != null && sender is FrameworkElement fe)
            {
                var curMouse = e.GetPosition(this);
                double dx = curMouse.X - _pcbDragStartMouse.X;
                double dy = curMouse.Y - _pcbDragStartMouse.Y;

                double newCanvasX = _pcbDragStartCanvasX + dx;
                double newCanvasY = _pcbDragStartCanvasY + dy;

                // Snap to 1mm grid (which is 5px) for a professional layout feel
                newCanvasX = Math.Round(newCanvasX / 5.0) * 5.0;
                newCanvasY = Math.Round(newCanvasY / 5.0) * 5.0;

                // Clamp to board boundary
                if (DataContext is MainViewModel mainVM)
                {
                    double maxCanvasX = mainVM.PcbVM.BoardWidth * 5 - _draggedPcbFootprint.Width;
                    double maxCanvasY = mainVM.PcbVM.BoardHeight * 5 - _draggedPcbFootprint.Height;
                    newCanvasX = Math.Max(0, Math.Min(maxCanvasX, newCanvasX));
                    newCanvasY = Math.Max(0, Math.Min(maxCanvasY, newCanvasY));
                }

                _draggedPcbFootprint.CanvasX = newCanvasX;
                _draggedPcbFootprint.CanvasY = newCanvasY;

                // Sync back to physical model coordinates (mm)
                _draggedPcbFootprint.Model.X = newCanvasX / 5.0;
                _draggedPcbFootprint.Model.Y = newCanvasY / 5.0;
            }
        }

        private void PcbFootprint_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingPcbFootprint)
            {
                if (sender is FrameworkElement fe)
                {
                    fe.ReleaseMouseCapture();
                }
                _isDraggingPcbFootprint = false;
                _draggedPcbFootprint = null;
                e.Handled = true;
            }
        }

        private void PcbFootprint_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ViewModels.PcbFootprintVM fpVM)
            {
                if (DataContext is MainViewModel mainVM)
                {
                    var dlg = new FootprintEditorWindow(fpVM, mainVM.PcbVM);
                    dlg.Owner = this;
                    dlg.ShowDialog();
                    e.Handled = true;
                }
            }
        }

        private void PcbTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // When the user switches to the 3D Board View tab, rebuild the 3D geometry
            if (sender is TabControl tc && tc.SelectedItem is TabItem ti && ti.Header?.ToString() == "3D Board View")
            {
                if (DataContext is MainViewModel mv)
                    mv.PcbVM.Rebuild3DGeometry(PcbViewport3D);
            }
        }

        private void ResetCamera_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel mv)
                mv.PcbVM.Rebuild3DGeometry(PcbViewport3D);
            PcbViewport3D.ZoomExtents();
        }
    }
}