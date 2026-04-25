using System.Collections.Generic;
using EdaSimulator.UI.ViewModels.Canvas;

namespace EdaSimulator.UI.Commands
{
    public class MoveItemsCommand : IUndoableCommand
    {
        private readonly List<ComponentNodeViewModel> _targets;
        private readonly double _dx;
        private readonly double _dy;

        public MoveItemsCommand(IEnumerable<CanvasItemViewModel> items, double dx, double dy)
        {
            _targets = new List<ComponentNodeViewModel>();
            foreach (var item in items)
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
            // Already happened visually during dragging before pushing command,
            // but if redoing, we do it again.
            foreach (var t in _targets)
            {
                t.MoveBy(_dx, _dy);
            }
        }

        public void Undo()
        {
            foreach (var t in _targets)
            {
                t.MoveBy(-_dx, -_dy);
            }
        }
    }
}
