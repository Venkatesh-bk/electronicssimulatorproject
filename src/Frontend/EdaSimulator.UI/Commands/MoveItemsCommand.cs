using System.Collections.Generic;
using EdaSimulator.UI.ViewModels.Canvas;

namespace EdaSimulator.UI.Commands
{
    /// <summary>
    /// Undoable command that records the translation offset of all selected components
    /// and ensures connected wires dynamically snap to updated pin coordinates on Undo/Redo.
    /// </summary>
    public class MoveItemsCommand : IUndoableCommand
    {
        private readonly SchematicViewModel _schematic;
        private readonly List<ComponentNodeViewModel> _targets;
        private readonly double _dx;
        private readonly double _dy;

        public MoveItemsCommand(SchematicViewModel schematic, double dx, double dy)
        {
            _schematic = schematic;
            _targets = new List<ComponentNodeViewModel>();
            
            foreach (var item in schematic.Items)
            {
                if (item is ComponentNodeViewModel comp && comp.IsSelected)
                {
                    _targets.Add(comp);
                }
            }
            
            _dx = dx;
            _dy = dy;
        }

        public void Execute()
        {
            foreach (var t in _targets)
            {
                t.MoveBy(_dx, _dy);
            }
            UpdateWires();
        }

        public void Undo()
        {
            foreach (var t in _targets)
            {
                t.MoveBy(-_dx, -_dy);
            }
            UpdateWires();
        }

        private void UpdateWires()
        {
            foreach (var item in _schematic.Items)
            {
                if (item is WireViewModel wire)
                {
                    foreach (var t in _targets)
                    {
                        foreach (var pin in t.Pins)
                        {
                            wire.UpdateEndpoint(pin.CorePin.Id, pin.X, pin.Y);
                        }
                    }
                }
            }
        }
    }
}
