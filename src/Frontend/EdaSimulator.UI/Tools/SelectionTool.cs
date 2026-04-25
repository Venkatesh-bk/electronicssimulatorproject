using EdaSimulator.UI.ViewModels.Canvas;

namespace EdaSimulator.UI.Tools
{
    /// <summary>
    /// Standard interaction tool for selecting items and dragging them around the workspace.
    /// </summary>
    public class SelectionTool : ICanvasTool
    {
        public string ToolName => "Selection Tool";
        
        private SchematicViewModel _schematic;
        private ComponentNodeViewModel _draggedComponent;
        private double _lastMouseX;
        private double _lastMouseY;
        private bool _isDragging;

        public SelectionTool(SchematicViewModel schematic)
        {
            _schematic = schematic;
        }

        public void OnPointerDown(double x, double y, CanvasItemViewModel target)
        {
            // Clear prior selection
            foreach (var item in _schematic.Items)
                item.IsSelected = false;

            if (target is ComponentNodeViewModel comp)
            {
                comp.IsSelected = true;
                _draggedComponent = comp;
                _schematic.SelectedComponent = comp;
                _isDragging = true;
                _lastMouseX = x;
                _lastMouseY = y;
            }
            else
            {
                _schematic.SelectedComponent = null;
            }
        }

        public void OnPointerMove(double x, double y)
        {
            if (_isDragging && _draggedComponent != null)
            {
                double dx = x - _lastMouseX;
                double dy = y - _lastMouseY;
                
                _draggedComponent.MoveBy(dx, dy);

                _lastMouseX = x;
                _lastMouseY = y;
            }
        }

        public void OnPointerUp(double x, double y)
        {
            _isDragging = false;
            _draggedComponent = null;
        }

        public void Cancel()
        {
            _isDragging = false;
            _draggedComponent = null;
        }

        public void OnDeactivated()
        {
            Cancel();
        }
    }
}
