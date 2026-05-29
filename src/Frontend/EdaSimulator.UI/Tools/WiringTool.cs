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
        private PinNodeViewModel? _startPin;
        private WireViewModel? _currentWire;

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
                    var pts = new System.Windows.Media.PointCollection();
                    pts.Add(new System.Windows.Point(pin.X, pin.Y));
                    pts.Add(new System.Windows.Point(pin.X, pin.Y)); // Initialize corner underneath start
                    pts.Add(new System.Windows.Point(x, y));         // Final tracker point
                    _currentWire.Points = pts;
                    
                    _schematic.AddWire(_currentWire);
                }
            }
            else
            {
                // Completing the wire
                if (target is PinNodeViewModel endPin && endPin != _startPin)
                {
                    // Snap the final trackers strictly to the physical endpoint
                    var pts = new System.Windows.Media.PointCollection(_currentWire.Points);
                    int count = pts.Count;
                    pts[count - 2] = new System.Windows.Point(endPin.X, pts[count - 3].Y);
                    pts[count - 1] = new System.Windows.Point(endPin.X, endPin.Y);
                    _currentWire.Points = pts;
                    
                    // BRIDGING THE VISUAL TO THE CORE LOGIC:
                    Guid activeNetId;
                    string netName;
                    
                    // Does the start pin already connect to a simulated net?
                    if (_startPin.CorePin.ConnectedNetId.HasValue && _startPin.CorePin.ConnectedNetId.Value != Guid.Empty)
                    {
                        activeNetId = _startPin.CorePin.ConnectedNetId.Value;
                        var existingNet = _schematic.CoreSchematic.GetNetById(activeNetId);
                        netName = existingNet?.Name ?? activeNetId.ToString("N")[..6];
                    }
                    else if (endPin.CorePin.ConnectedNetId.HasValue && endPin.CorePin.ConnectedNetId.Value != Guid.Empty)
                    {
                        activeNetId = endPin.CorePin.ConnectedNetId.Value;
                        var existingNet = _schematic.CoreSchematic.GetNetById(activeNetId);
                        netName = existingNet?.Name ?? activeNetId.ToString("N")[..6];
                    }
                    else 
                    {
                        // Generate a unique net name. Using 8 chars (not 4) avoids realistic collision.
                        string uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                        netName = "N_" + uniqueSuffix;
                        var newNet = _schematic.CoreSchematic.CreateNet(netName);
                        activeNetId = newNet.Id;
                    }

                    // Perform the actual physics routing
                    _schematic.CoreSchematic.ConnectPinToNet(_startPin.CorePin, activeNetId);
                    _schematic.CoreSchematic.ConnectPinToNet(endPin.CorePin, activeNetId);
                    
                    // ── Step 1: Stamp wire metadata for drag-tracking and net labeling ──
                    _currentWire.TargetNetId  = activeNetId;
                    _currentWire.StartPinId   = _startPin.CorePin.Id;
                    _currentWire.EndPinId     = endPin.CorePin.Id;
                    _currentWire.NetLabel     = netName;

                    // ── Step 2: Update visual pin state so dots turn green immediately ──
                    _startPin.ConnectedNetName = netName;
                    endPin.ConnectedNetName    = netName;

                    // Clean state machine back to empty wiring ready for next line
                    _startPin = null;
                    _currentWire = null;
                }
                else
                {
                    // User clicked in empty canvas space to place an absolute corner anchor
                    // Lock the current corner and tracking end
                    var pts = new System.Windows.Media.PointCollection(_currentWire.Points);
                    var n = pts.Count;
                    var lockedEnd = pts[n - 1];

                    // Append 2 new tracker points to continue orthogonal logic
                    pts.Add(new System.Windows.Point(lockedEnd.X, lockedEnd.Y)); 
                    pts.Add(new System.Windows.Point(lockedEnd.X, lockedEnd.Y));
                    _currentWire.Points = pts;
                }
            }
        }

        public void OnPointerMove(double x, double y)
        {
            if (_startPin != null && _currentWire != null && _currentWire.Points.Count >= 3)
            {
                // Clone the collection so WPF detects a property change and redraws the UI
                var pts = new System.Windows.Media.PointCollection(_currentWire.Points);
                int count = pts.Count;
                var prevPoint = pts[count - 3]; // The last anchored click coordinate
                
                // Track moving corner
                pts[count - 2] = new System.Windows.Point(x, prevPoint.Y);
                pts[count - 1] = new System.Windows.Point(x, y);

                _currentWire.Points = pts;
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
