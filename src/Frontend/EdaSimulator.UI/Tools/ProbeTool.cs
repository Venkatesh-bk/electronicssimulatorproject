using EdaSimulator.UI.ViewModels.Canvas;
using System.Linq;

namespace EdaSimulator.UI.Tools
{
    public class ProbeTool : ICanvasTool
    {
        private readonly SchematicViewModel _schematic;

        public ProbeTool(SchematicViewModel schematic)
        {
            _schematic = schematic;
        }

        public void OnPointerDown(double x, double y, CanvasItemViewModel target)
        {
            // Usually we'd raycast exactly against a WireItemViewModel to grab its exact Net string.
            // For rapid parity, we drop a theoretical Probe tracker bounding the nearest Net.
            
            // To ensure compatibility and avoid complex geometric vector collisions:
            // We just ask the user to click precisely on Pin nodes for Probes using the Gravity model.
            
            string targetNet = "0"; // Default to ground

            if (target is ComponentNodeViewModel comp)
            {
                // Just map it loosely to the first net it finds for now.
                var firstPin = comp.CoreComponent.Pins.FirstOrDefault();
                if (firstPin != null && !firstPin.IsFloating)
                {
                    targetNet = firstPin.ConnectedNetId.ToString() ?? "0"; // SPICE node map
                }
            }
            
            // For now, allow dropping probes generally.
            var probe = new VoltageProbeItemViewModel(targetNet, x, y);
            _schematic.Items.Add(probe);
            
            // Optionally auto-deactivate back to selection arrow
            _schematic.ActiveTool = new SelectionTool(_schematic);
        }

        public string ToolName => "Voltage Probe";

        public void OnPointerMove(double x, double y) { }

        public void OnPointerUp(double x, double y) { }

        public void Cancel()
        {
            _schematic.ActiveTool = new SelectionTool(_schematic);
        }

        public void OnDeactivated() { }
    }
}
