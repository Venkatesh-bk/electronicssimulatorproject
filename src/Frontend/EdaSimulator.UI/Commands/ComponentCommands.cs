using EdaSimulator.UI.ViewModels.Canvas;

namespace EdaSimulator.UI.Commands
{
    /// <summary>
    /// Undoable command for placing a component node on the canvas.
    /// Undo removes it; Redo re-adds it.
    /// </summary>
    public class AddComponentCommand : IUndoableCommand
    {
        private readonly SchematicViewModel _schematic;
        private readonly ComponentNodeViewModel _component;

        public AddComponentCommand(SchematicViewModel schematic, ComponentNodeViewModel component)
        {
            _schematic = schematic;
            _component = component;
        }

        public void Execute()
        {
            // Guard: only add if not already present
            if (!_schematic.Items.Contains(_component))
                _schematic.AddComponentNode(_component);
        }

        public void Undo()
        {
            _schematic.RemoveItem(_component);
        }
    }

    /// <summary>
    /// Undoable command for deleting any canvas item (component, wire, etc).
    /// Undo re-inserts the item. For components, pins are also restored.
    /// </summary>
    public class DeleteItemCommand : IUndoableCommand
    {
        private readonly SchematicViewModel _schematic;
        private readonly CanvasItemViewModel _item;

        public DeleteItemCommand(SchematicViewModel schematic, CanvasItemViewModel item)
        {
            _schematic = schematic;
            _item = item;
        }

        public void Execute()
        {
            _schematic.RemoveItem(_item);
        }

        public void Undo()
        {
            if (_item is ComponentNodeViewModel comp)
                _schematic.AddComponentNode(comp);
            else if (!_schematic.Items.Contains(_item))
                _schematic.Items.Add(_item);
        }
    }
}
