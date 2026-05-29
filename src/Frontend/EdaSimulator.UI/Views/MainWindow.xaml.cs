using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.Models.Components;
using EdaSimulator.Engines.Simulation.Digital;
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

                    // === Power Symbols ===
                    case "Ground": coreComponent = new GroundSymbol(GetNextDesignator("GND")); break;
                    case "VCC":    coreComponent = new PowerRail(GetNextDesignator("VCC"), 5.0); break;
                    case "VDD":    coreComponent = new PowerRail(GetNextDesignator("VDD"), 3.3); break;
                    case "V12":    coreComponent = new PowerRail(GetNextDesignator("V12"), 12.0); break;
                    case "VN12":   coreComponent = new PowerRail(GetNextDesignator("VN"), -12.0); break;


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

                    CanvasViewModel.AddComponentNode(vm);
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

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (CanvasViewModel == null) return;

            // Undo / Redo Global Hooks
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.Z)
                {
                    CanvasViewModel.History.Undo();
                    return;
                }
                if (e.Key == Key.Y)
                {
                    CanvasViewModel.History.Redo();
                    return;
                }
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

            // ── Phase 7: Global Keyboard Shortcuts ───────────────────────────────────
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                ViewModel?.SaveProjectCommand.Execute(null);
            }
            else if (e.Key == Key.O && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                ViewModel?.LoadProjectCommand.Execute(null);
            }
            else if (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                ViewModel?.NewProjectCommand.Execute(null);
            }
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
            settingsWindow.ShowDialog();
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
    }
}