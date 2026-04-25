using System;
using EdaSimulator.UI.ViewModels.Canvas;

namespace EdaSimulator.UI.Tools
{
    /// <summary>
    /// Interaction state machine handling the clicking of Pins and drawing 
    /// of orthogonal tracking lines between them to form Nets.
    /// </summary>
    public class WiringTool : ICanvasTool
    {
        public string ToolName => "Wiring Tool";

        private SchematicViewModel _schematic;
        private PinNodeViewModel _startPin;
        private WireViewModel _currentWire;

        public WiringTool(SchematicViewModel schematic)
        {
            _schematic = schematic;
        }

        public void OnPointerDown(double x, double y, CanvasItemViewModel target)
        {
            if (_startPin == null)
            {
                // First click must be on a pin to start wiring
                if (target is PinNodeViewModel pin)
                {
                    _startPin = pin;
                    
                    // Start orthogonal wire path: [0] = Original, [1] = X-Corner, [2] = Mouse-Tracking
                    _currentWire = new WireViewModel(Guid.NewGuid());
                    _currentWire.Points.Add(new System.Windows.Point(pin.X, pin.Y));
                    _currentWire.Points.Add(new System.Windows.Point(pin.X, pin.Y)); // Initialize corner underneath start
                    _currentWire.Points.Add(new System.Windows.Point(x, y));         // Final tracker point
                    
                    _schematic.AddWire(_currentWire);
                }
            }
            else
            {
                // Completing the wire
                if (target is PinNodeViewModel endPin && endPin != _startPin)
                {
                    // Snap to the physical endpoint
                    _currentWire.Points[1] = new System.Windows.Point(endPin.X, _startPin.Y);
                    _currentWire.Points[2] = new System.Windows.Point(endPin.X, endPin.Y);
                    
                    // BRIDGING THE VISUAL TO THE CORE LOGIC:
                    Guid activeNetId;
                    
                    // Does the start pin already connect to a simulated net?
                    if (_startPin.CorePin.ConnectedNetId.HasValue && _startPin.CorePin.ConnectedNetId.Value != Guid.Empty)
                    {
                        activeNetId = _startPin.CorePin.ConnectedNetId.Value;
                    }
                    else if (endPin.CorePin.ConnectedNetId.HasValue && endPin.CorePin.ConnectedNetId.Value != Guid.Empty)
                    {
                        activeNetId = endPin.CorePin.ConnectedNetId.Value;
                    }
                    else 
                    {
                        // Generate a physical electrical simulation net
                        var newNet = _schematic.CoreSchematic.CreateNet("N_" + Guid.NewGuid().ToString().Substring(0, 4));
                        activeNetId = newNet.Id;
                    }

                    // Perform the actual physics routing
                    _schematic.CoreSchematic.ConnectPinToNet(_startPin.CorePin, activeNetId);
                    _schematic.CoreSchematic.ConnectPinToNet(endPin.CorePin, activeNetId);
                    
                    _currentWire.TargetNetId = activeNetId;

                    // Clean state machine back to empty wiring ready for next line
                    _startPin = null;
                    _currentWire = null;
                }
                else
                {
                    // User clicked in empty canvas space to place an absolute corner anchor
                    // Standard EDA tool usually finalizes the current line segments here and starts next.
                    // For right now, do nothing on empty canvas clicks, must click a pin.
                }
            }
        }

        public void OnPointerMove(double x, double y)
        {
            if (_startPin != null && _currentWire != null)
            {
                // Orthogonal 90-degree track drawing
                // Point[0] = Fixed Start
                // Point[1] = L-Shape corner traversing horizontally first
                // Point[2] = Ending coordinate tracked at mouse
                
                _currentWire.Points[1] = new System.Windows.Point(x, _startPin.Y);
                _currentWire.Points[2] = new System.Windows.Point(x, y);
            }
        }

        public void OnPointerUp(double x, double y) { /* Wiring usually relies on explicit clicks, not drags */ }

        public void Cancel()
        {
            if (_currentWire != null)
            {
                _schematic.RemoveItem(_currentWire);
                _currentWire = null;
            }
            _startPin = null;
        }

        public void OnDeactivated()
        {
            Cancel();
        }
    }
}
