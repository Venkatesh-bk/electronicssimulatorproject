using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EdaSimulator.Engines.Models;
using EdaSimulator.Engines.Models.Components;
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

        private MainViewModel ViewModel => DataContext as MainViewModel;
        private ViewModels.Canvas.SchematicViewModel CanvasViewModel => ViewModel?.ActiveSchematicViewModel;

        // --- Viewport Physics and Routing Intercepts ---
        private const double GRID_SIZE = 10.0;
        private double Snap(double value) => Math.Round(value / GRID_SIZE) * GRID_SIZE;

        private Point? _panStartPoint;
        private double _panStartX;
        private double _panStartY;

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
            CanvasViewModel.ActiveTool.OnPointerDown(Snap(pos.X), Snap(pos.Y), target);
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
            CanvasViewModel.ActiveTool.OnPointerMove(Snap(pos.X), Snap(pos.Y));
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
            CanvasViewModel.ActiveTool.OnPointerUp(Snap(pos.X), Snap(pos.Y));
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
                        string componentType = item.Tag as string;
                        if (!string.IsNullOrEmpty(componentType))
                        {
                            DragDrop.DoDragDrop(ToolboxList, componentType, DragDropEffects.Copy);
                        }
                    }
                }
            }
        }

        private void SchematicCanvas_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string componentType = (string)e.Data.GetData(DataFormats.StringFormat);
                var pos = e.GetPosition(SchematicItemsControl);

                Component coreComponent = null;
                var shortGuid = Guid.NewGuid().ToString().Substring(0, 4).ToUpper();

                switch (componentType)
                {
                    case "Resistor": coreComponent = new Resistor("R" + shortGuid, "1k"); break;
                    case "Capacitor": coreComponent = new Capacitor("C" + shortGuid, "1u"); break;
                    case "Inductor": coreComponent = new Inductor("L" + shortGuid, "1m"); break;
                    case "VoltageSource": coreComponent = new VoltageSource("V" + shortGuid, "DC 5"); break;
                    case "CurrentSource": coreComponent = new CurrentSource("I" + shortGuid, "DC 1m"); break;
                }

                if (coreComponent != null && CanvasViewModel != null)
                {
                    var vm = new ViewModels.Canvas.ComponentNodeViewModel(coreComponent)
                    {
                        X = Snap(pos.X - 25), // Basic center alignment
                        Y = Snap(pos.Y - 25)
                    };

                    CanvasViewModel.AddComponentNode(vm);
                }
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (CanvasViewModel != null)
            {
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
            }
        }
    }
}