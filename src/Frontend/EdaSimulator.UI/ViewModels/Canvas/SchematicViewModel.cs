using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EdaSimulator.Engines.Models;
using EdaSimulator.UI.Tools;

namespace EdaSimulator.UI.ViewModels.Canvas
{
    /// <summary>
    /// Binds the UI items collections to the underlying engine Graph.
    /// Exposes bindable properties and tracks global canvas viewport state.
    /// </summary>
    public partial class SchematicViewModel : ObservableObject
    {
        public Schematic CoreSchematic { get; }
        public string Title => CoreSchematic.Title;
        
        public EdaSimulator.UI.ViewModels.SimulationConfigViewModel SimConfig { get; }

        public ObservableCollection<CanvasItemViewModel> Items { get; } = new();

        [ObservableProperty]
        private double _panX;

        [ObservableProperty]
        private double _panY;

        [ObservableProperty]
        private double _zoomFactor = 1.0;

        [ObservableProperty]
        private System.Windows.Rect _selectionBounds;

        [ObservableProperty]
        private bool _isSelectionBoundsVisible;

        [ObservableProperty]
        private ComponentNodeViewModel? _selectedComponent;

        [ObservableProperty]
        private WireViewModel? _selectedWire;

        public EdaSimulator.UI.Commands.CommandManager History { get; } = new EdaSimulator.UI.Commands.CommandManager();

        private ICanvasTool? _activeTool;
        public ICanvasTool? ActiveTool
        {
            get => _activeTool;
            set
            {
                if (_activeTool != value)
                {
                    _activeTool?.OnDeactivated();
                    _activeTool = value;
                    OnPropertyChanged(nameof(ActiveTool));
                }
            }
        }

        public event Action<string>? NetProbed;

        public void OnNetProbed(string netName)
        {
            NetProbed?.Invoke(netName);
        }

        public void RenameNet(Guid netId, string newName)
        {
            var net = CoreSchematic.GetNetById(netId);
            if (net == null) return;

            // Rename engine net
            net.Name = newName;

            // Update all wires sharing this net ID
            foreach (var w in Items.OfType<WireViewModel>().Where(w => w.TargetNetId == netId))
            {
                w.NetLabel = newName;
            }

            // Update all visual net label badges sharing this net ID
            foreach (var label in Items.OfType<NetLabelItemViewModel>().Where(l => l.AssociatedNetId == netId))
            {
                label.NetName = newName;
            }

            // Update all visual pins connected to this net
            var connectedPinIds = net.ConnectedPinIds;
            foreach (var comp in Items.OfType<ComponentNodeViewModel>())
            {
                foreach (var pin in comp.Pins)
                {
                    if (connectedPinIds.Contains(pin.Id))
                    {
                        pin.ConnectedNetName = newName;
                    }
                }
            }
        }

        public void ReconstructWiresFromNets()
        {
            // Remove existing visual wires first to avoid duplicates
            var existingWires = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OfType<WireViewModel>(Items));
            foreach (var w in existingWires)
            {
                Items.Remove(w);
            }

            // Find all PinNodeViewModels on the canvas
            var pinVms = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OfType<PinNodeViewModel>(Items));

            // Reconstruct wires for each net
            foreach (var net in CoreSchematic.Nets.Values)
            {
                if (net.ConnectedPinIds.Count < 2) continue;

                // Resolve PinNodeViewModels for this net
                var netPins = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(pinVms, p => net.ConnectedPinIds.Contains(p.Id)));
                if (netPins.Count < 2) continue;

                // Daisy-chain wire creation between consecutive pins of this net
                for (int i = 0; i < netPins.Count - 1; i++)
                {
                    var p1 = netPins[i];
                    var p2 = netPins[i + 1];

                    var wire = new WireViewModel(net.Id)
                    {
                        StartPinId = p1.Id,
                        EndPinId = p2.Id,
                        NetLabel = net.Name
                    };

                    // Draw orthogonal path between p1 and p2:
                    // Pt1 (p1.X, p1.Y) -> Pt2 (p2.X, p1.Y) -> Pt3 (p2.X, p2.Y)
                    wire.Points.Add(new System.Windows.Point(p1.X, p1.Y));
                    wire.Points.Add(new System.Windows.Point(p2.X, p1.Y));
                    wire.Points.Add(new System.Windows.Point(p2.X, p2.Y));

                    // Set pin connected net names
                    p1.ConnectedNetName = net.Name;
                    p2.ConnectedNetName = net.Name;

                    wire.UpdateLabelPosition();

                    AddWire(wire);
                }
            }
        }

        public SchematicViewModel(Schematic coreSchematic)
        {
            CoreSchematic = coreSchematic ?? throw new ArgumentNullException(nameof(coreSchematic));
            SimConfig = new EdaSimulator.UI.ViewModels.SimulationConfigViewModel(coreSchematic.SimConfig);
            ActiveTool = new SelectionTool(this); // Default to standard arrow pointer

            // Populate visual components from core schematic
            foreach (var comp in coreSchematic.Components.Values)
            {
                var compNode = new ComponentNodeViewModel(comp);
                Items.Add(compNode);
                foreach (var pin in compNode.Pins)
                {
                    Items.Add(pin);
                }
            }
        }

        public void AddComponentNode(ComponentNodeViewModel compNode)
        {
            CoreSchematic.AddComponent(compNode.CoreComponent);
            Items.Add(compNode);
            
            // Flatten child pins into the main canvas so they can render natively and independently hit-test
            foreach (var pin in compNode.Pins)
            {
                Items.Add(pin);
            }
        }

        public void AddWire(WireViewModel wire)
        {
            Items.Add(wire);
        }

        public void RemoveItem(CanvasItemViewModel item)
        {
            if (item is ComponentNodeViewModel comp)
            {
                CoreSchematic.RemoveComponent(comp.CoreComponent.Id);
                foreach (var pin in comp.Pins) Items.Remove(pin);
            }
            // Wires don't strictly delete engine nets unless it's the last wire for that net.
            
            Items.Remove(item);
        }

        public (bool isValid, string logOutput) RunDRC()
        {
            var issues = CoreSchematic.Validate();
            
            // Map validation visual crosses
            foreach (var item in Items)
            {
                if (item is PinNodeViewModel pinVm)
                {
                    pinVm.HasError = pinVm.CorePin.IsFloating;
                }
            }

            if (issues.Count == 0)
                return (true, "--- DRC PASSED ---\nNo issues found.\n\n");

            bool hasCritical = false;
            string formattedLog = "--- DRC VALIDATION RESULTS ---\n\n";
            
            foreach (var issue in issues)
            {
                formattedLog += issue + "\n";
                if (issue.StartsWith("CRITICAL")) hasCritical = true;
            }

            formattedLog += "\n";

            return (!hasCritical, formattedLog);
        }
    }
}
