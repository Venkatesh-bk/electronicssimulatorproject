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
        private CanvasItemViewModel? _draggedItem;
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
            bool isDraggable = target is ComponentNodeViewModel || 
                               target is NetLabelItemViewModel || 
                               target is VoltageProbeItemViewModel || 
                               target is CurrentProbeItemViewModel;

            if (isDraggable)
            {
                if (!target.IsSelected)
                {
                    foreach (var item in _schematic.Items)
                        item.IsSelected = false;
                    
                    target.IsSelected = true;
                }
                
                _draggedItem = target;
                _schematic.SelectedComponent = target as ComponentNodeViewModel;
                _schematic.SelectedWire = null;
                _isDragging = true;
                _lastMouseX = x;
                _lastMouseY = y;
                _totalDx = 0;
                _totalDy = 0;
            }
            else if (target is WireViewModel wire)
            {
                if (!wire.IsSelected)
                {
                    foreach (var item in _schematic.Items)
                        item.IsSelected = false;
                    
                    wire.IsSelected = true;
                }
                _schematic.SelectedWire = wire;
                _schematic.SelectedComponent = null;
            }
            else
            {
                // Clicked empty space: clear all selections and start box select
                foreach (var item in _schematic.Items)
                {
                    item.IsSelected = false;
                }
                _schematic.SelectedComponent = null;
                _schematic.SelectedWire = null;
                
                _isBoxSelecting = true;
                _lastMouseX = x; // Serves as the anchor corner X
                _lastMouseY = y; // Serves as the anchor corner Y
                
                _schematic.SelectionBounds = new System.Windows.Rect(x, y, 0, 0);
                _schematic.IsSelectionBoundsVisible = true;
            }
        }

        public void OnPointerMove(double x, double y)
        {
            if (_isDragging && _draggedItem != null)
            {
                double dx = x - _lastMouseX;
                double dy = y - _lastMouseY;
                
                _totalDx += dx;
                _totalDy += dy;
                
                // MULTI-SELECT MOVE BINDING
                // If the dragged item is part of a multiple selection, drag them all!
                if (_draggedItem.IsSelected)
                {
                    foreach (var item in _schematic.Items)
                    {
                        if (item.IsSelected && !(item is WireViewModel) && !(item is PinNodeViewModel))
                        {
                            item.MoveBy(dx, dy);
                        }
                    }
                }
                else
                {
                    _draggedItem.MoveBy(dx, dy);
                }

                // ── Wire drag-follow: update any wire endpoint that belongs to a moved pin ──
                foreach (var item in _schematic.Items)
                {
                    if (item is WireViewModel wire)
                    {
                        foreach (var canvasItem in _schematic.Items)
                        {
                            if (canvasItem is ComponentNodeViewModel comp && 
                                (comp.IsSelected || comp == _draggedItem))
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

                // Dynamically highlight components, net labels, and probes falling inside bounds
                foreach (var item in _schematic.Items)
                {
                    if (item is ComponentNodeViewModel comp)
                    {
                        var compRect = new System.Windows.Rect(comp.X, comp.Y, comp.BoundsWidth, comp.BoundsHeight);
                        comp.IsSelected = _schematic.SelectionBounds.IntersectsWith(compRect);
                    }
                    else if (item is NetLabelItemViewModel netLabel)
                    {
                        var labelRect = new System.Windows.Rect(netLabel.X, netLabel.Y, 50, 20);
                        netLabel.IsSelected = _schematic.SelectionBounds.IntersectsWith(labelRect);
                    }
                    else if (item is VoltageProbeItemViewModel or CurrentProbeItemViewModel)
                    {
                        var probeRect = new System.Windows.Rect(item.X, item.Y, 20, 20);
                        item.IsSelected = _schematic.SelectionBounds.IntersectsWith(probeRect);
                    }
                }
            }
        }

        public void OnPointerUp(double x, double y)
        {
            if (_isDragging && (_totalDx != 0 || _totalDy != 0))
            {
                // Ensure the dragged item is explicitly in the selection target lists so undo works
                if (_draggedItem != null) _draggedItem.IsSelected = true;

                var cmd = new EdaSimulator.UI.Commands.MoveItemsCommand(_schematic, _totalDx, _totalDy);
                _schematic.History.PushInteractionCommand(cmd);
            }

            _isDragging = false;
            _draggedItem = null;
            
            if (_isBoxSelecting)
            {
                _isBoxSelecting = false;
                _schematic.IsSelectionBoundsVisible = false;
            }
        }

        public void Cancel()
        {
            _isDragging = false;
            _draggedItem = null;
        }

        public void OnDeactivated()
        {
            Cancel();
        }
    }
}
