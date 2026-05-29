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

        public SchematicViewModel(Schematic coreSchematic)
        {
            CoreSchematic = coreSchematic ?? throw new ArgumentNullException(nameof(coreSchematic));
            SimConfig = new EdaSimulator.UI.ViewModels.SimulationConfigViewModel(coreSchematic.SimConfig);
            ActiveTool = new SelectionTool(this); // Default to standard arrow pointer
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
