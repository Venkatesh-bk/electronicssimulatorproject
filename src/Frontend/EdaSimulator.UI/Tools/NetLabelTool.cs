using EdaSimulator.UI.ViewModels.Canvas;
using EdaSimulator.UI.Views;
using System.Windows;

namespace EdaSimulator.UI.Tools
{
    public class NetLabelTool : ICanvasTool
    {
        private readonly SchematicViewModel _schematic;

        public NetLabelTool(SchematicViewModel schematic)
        {
            _schematic = schematic;
        }

        public string ToolName => "Net Label";

        public void OnPointerDown(double x, double y, CanvasItemViewModel target)
        {
            WireViewModel? targetWire = null;
            PinNodeViewModel? targetPin = null;

            if (target is WireViewModel wire)
            {
                targetWire = wire;
            }
            else if (target is PinNodeViewModel pin)
            {
                targetPin = pin;
            }

            if (targetWire != null || targetPin != null)
            {
                System.Guid netId = System.Guid.Empty;
                string currentName = string.Empty;

                if (targetWire != null)
                {
                    netId = targetWire.TargetNetId;
                    var net = _schematic.CoreSchematic.GetNetById(netId);
                    if (net != null)
                    {
                        currentName = net.Name;
                    }
                }
                else if (targetPin != null)
                {
                    if (targetPin.CorePin.ConnectedNetId.HasValue)
                    {
                        netId = targetPin.CorePin.ConnectedNetId.Value;
                        var net = _schematic.CoreSchematic.GetNetById(netId);
                        if (net != null)
                        {
                            currentName = net.Name;
                        }
                    }
                    else
                    {
                        // Pin is not connected, cannot add net label directly to a floating pin
                        MessageBox.Show("Cannot add a net label to a floating pin. Connect a wire to it first.", "Net Label",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        _schematic.ActiveTool = new SelectionTool(_schematic);
                        return;
                    }
                }

                // Show Net Label dialog
                var activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) 
                                   ?? Application.Current.MainWindow;
                var dlg = new NetLabelDialog(currentName)
                {
                    Owner = activeWindow
                };

                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        _schematic.RenameNet(netId, dlg.NetName);
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"Failed to rename net: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                // Switch back to selection tool
                _schematic.ActiveTool = new SelectionTool(_schematic);
            }
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
