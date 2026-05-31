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
            string targetNet = "0"; // Default to ground

            if (target is WireViewModel wire)
            {
                var net = _schematic.CoreSchematic.GetNetById(wire.TargetNetId);
                if (net != null)
                {
                    targetNet = net.Name;
                }
            }
            else if (target is PinNodeViewModel pin)
            {
                if (pin.IsConnected)
                {
                    targetNet = pin.ConnectedNetName;
                }
            }
            else if (target is ComponentNodeViewModel comp)
            {
                var firstPin = comp.CoreComponent.Pins.FirstOrDefault();
                if (firstPin != null)
                {
                    // Look up net by pin connected NetId
                    if (firstPin.ConnectedNetId.HasValue)
                    {
                        var net = _schematic.CoreSchematic.GetNetById(firstPin.ConnectedNetId.Value);
                        if (net != null)
                        {
                            targetNet = net.Name;
                        }
                    }
                }
            }
            
            // Drop visual probe tracker at clicked location
            var probe = new VoltageProbeItemViewModel(targetNet, x, y);
            _schematic.Items.Add(probe);
            
            // Raise the net probed notification to trigger oscilloscope highlighting
            _schematic.OnNetProbed(targetNet);
            
            // Auto-deactivate back to selection arrow
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
