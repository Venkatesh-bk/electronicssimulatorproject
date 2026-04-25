using EdaSimulator.Engines.Models;
using EdaSimulator.UI.ViewModels.Canvas;

namespace EdaSimulator.UI.Tools
{
    /// <summary>
    /// Interaction state machine for grabbing a new component from the toolbox
    /// and "ghosting" it across the canvas until dropped.
    /// </summary>
    public class PlacementTool : ICanvasTool
    {
        public string ToolName => "Placement Tool";

        private SchematicViewModel _schematic;
        private ComponentNodeViewModel _ghostComponent;

        public PlacementTool(SchematicViewModel schematic, Component coreComponentToPlace)
        {
            _schematic = schematic;
            _ghostComponent = new ComponentNodeViewModel(coreComponentToPlace);
            
            // Render it partially transparent while "ghosting"
            // (Will require a WPF style trigger on a Ghosting property later)
            
            _schematic.AddComponentNode(_ghostComponent);
        }

        public void OnPointerDown(double x, double y, CanvasItemViewModel target)
        {
            // Left click drops the item permanently
            // In a real EDA tool, we'd snap to grid here and potentially spawn another ghost.
            _schematic.ActiveTool = new SelectionTool(_schematic); // Revert to select
        }

        public void OnPointerMove(double x, double y)
        {
            if (_ghostComponent != null)
            {
                // Follow the mouse cursor, centered
                _ghostComponent.X = x - (_ghostComponent.BoundsWidth / 2);
                _ghostComponent.Y = y - (_ghostComponent.BoundsHeight / 2);
            }
        }

        public void OnPointerUp(double x, double y) { /* Do nothing */ }

        public void Cancel()
        {
            if (_ghostComponent != null)
            {
                _schematic.RemoveItem(_ghostComponent);
                _ghostComponent = null;
            }
        }

        public void OnDeactivated()
        {
            Cancel();
        }
    }
}
