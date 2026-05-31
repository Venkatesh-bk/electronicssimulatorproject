using EdaSimulator.UI.ViewModels.Canvas;

namespace EdaSimulator.UI.Tools
{
    public class CurrentProbeTool : ICanvasTool
    {
        private readonly SchematicViewModel _schematic;

        public CurrentProbeTool(SchematicViewModel schematic)
        {
            _schematic = schematic;
        }

        public string ToolName => "Current Probe";

        public void OnPointerDown(double x, double y, CanvasItemViewModel target)
        {
            if (target is ComponentNodeViewModel comp)
            {
                // Current probes bind strictly to Component bodies, not abstract Nets/Wires
                var probe = new CurrentProbeItemViewModel(comp.CoreComponent.Designator, x, y);
                _schematic.Items.Add(probe);
                
                // Raise the net probed notification using standard SPICE current syntax: I(Designator)
                _schematic.OnNetProbed($"I({comp.CoreComponent.Designator})");
                
                // Return to selection arrow automatically
                _schematic.ActiveTool = new SelectionTool(_schematic);
            }
            // If missed bounding hit, we just ignore click until they accurately click a device
        }

        public void OnPointerMove(double x, double y) { }

        public void OnPointerUp(double x, double y) { }

        public void Cancel()
        {
            _schematic.ActiveTool = new SelectionTool(_schematic);
        }

        public void OnDeactivated() { }
    }
}
