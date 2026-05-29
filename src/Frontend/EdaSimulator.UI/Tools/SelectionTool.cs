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
        private ComponentNodeViewModel? _draggedComponent;
        private double _lastMouseX;
        private double _lastMouseY;
        private double _totalDx;
        private double _totalDy;
        private bool _isDragging;
        private bool _isBoxSelecting;

        public SelectionTool(SchematicViewModel schematic)
        {
            _schematic = schematic;
        }

        public void OnPointerDown(double x, double y, CanvasItemViewModel target)
        {
            if (target is ComponentNodeViewModel comp)
            {
                // If clicking an unselected component, clear others.
                if (!comp.IsSelected)
                {
                    foreach (var item in _schematic.Items)
                        item.IsSelected = false;
                    
                    comp.IsSelected = true;
                }
                
                _draggedComponent = comp;
                _schematic.SelectedComponent = comp;
                _isDragging = true;
                _lastMouseX = x;
                _lastMouseY = y;
                _totalDx = 0;
                _totalDy = 0;
            }
            else
            {
                // Clicked empty space: clear all selections and start box select
                foreach (var item in _schematic.Items)
                {
                    item.IsSelected = false;
                }
                _schematic.SelectedComponent = null;
                
                _isBoxSelecting = true;
                _lastMouseX = x; // Serves as the anchor corner X
                _lastMouseY = y; // Serves as the anchor corner Y
                
                _schematic.SelectionBounds = new System.Windows.Rect(x, y, 0, 0);
                _schematic.IsSelectionBoundsVisible = true;
            }
        }

        public void OnPointerMove(double x, double y)
        {
            if (_isDragging && _draggedComponent != null)
            {
                double dx = x - _lastMouseX;
                double dy = y - _lastMouseY;
                
                _totalDx += dx;
                _totalDy += dy;
                
                // MULTI-SELECT MOVE BINDING
                // If the dragged item is part of a multiple selection, drag them all!
                if (_draggedComponent.IsSelected)
                {
                    foreach (var item in _schematic.Items)
                    {
                        if (item is ComponentNodeViewModel comp && comp.IsSelected)
                        {
                            comp.MoveBy(dx, dy);
                        }
                    }
                }
                else
                {
                    _draggedComponent.MoveBy(dx, dy);
                }

                // ── Wire drag-follow: update any wire endpoint that belongs to a moved pin ──
                foreach (var item in _schematic.Items)
                {
                    if (item is WireViewModel wire)
                    {
                        foreach (var canvasItem in _schematic.Items)
                        {
                            if (canvasItem is ComponentNodeViewModel comp && 
                                (comp.IsSelected || comp == _draggedComponent))
                            {
                                foreach (var pin in comp.Pins)
                                {
                                    wire.UpdateEndpoint(pin.CorePin.Id, pin.X, pin.Y);
                                }
                            }
                        }
                    }
                }

                _lastMouseX = x;
                _lastMouseY = y;
            }
            else if (_isBoxSelecting)
            {
                double left = System.Math.Min(x, _lastMouseX);
                double top = System.Math.Min(y, _lastMouseY);
                double width = System.Math.Abs(x - _lastMouseX);
                double height = System.Math.Abs(y - _lastMouseY);

                _schematic.SelectionBounds = new System.Windows.Rect(left, top, width, height);

                // Dynamically highlight components falling inside bounds
                foreach (var item in _schematic.Items)
                {
                    if (item is ComponentNodeViewModel comp)
                    {
                        var compRect = new System.Windows.Rect(comp.X, comp.Y, comp.BoundsWidth, comp.BoundsHeight);
                        comp.IsSelected = _schematic.SelectionBounds.IntersectsWith(compRect);
                    }
                }
            }
        }

        public void OnPointerUp(double x, double y)
        {
            if (_isDragging && (_totalDx != 0 || _totalDy != 0))
            {
                // Ensure the dragged item is explicitly in the selection target lists so undo works
                if (_draggedComponent != null) _draggedComponent.IsSelected = true;

                var cmd = new EdaSimulator.UI.Commands.MoveItemsCommand(_schematic.Items, _totalDx, _totalDy);
                _schematic.History.PushInteractionCommand(cmd);
            }

            _isDragging = false;
            _draggedComponent = null;
            
            if (_isBoxSelecting)
            {
                _isBoxSelecting = false;
                _schematic.IsSelectionBoundsVisible = false;
            }
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
