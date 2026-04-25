using EdaSimulator.UI.ViewModels.Canvas;

namespace EdaSimulator.UI.Tools
{
    /// <summary>
    /// Contract for all interaction state machines (Selection, Wiring, Placement).
    /// Abstracts raw WPF mouse events into pure CAD spatial logic.
    /// </summary>
    public interface ICanvasTool
    {
        string ToolName { get; }

        /// <summary>
        /// Fired when the primary pointer (left click) goes down on the canvas.
        /// </summary>
        /// <param name="x">Absolute X on the canvas grid.</param>
        /// <param name="y">Absolute Y on the canvas grid.</param>
        /// <param name="target">The top-most visual item hit by the click, or null if empty space.</param>
        void OnPointerDown(double x, double y, CanvasItemViewModel target);

        /// <summary>
        /// Fired when the pointer moves across the canvas while the tool is active.
        /// </summary>
        void OnPointerMove(double x, double y);

        /// <summary>
        /// Fired when the primary pointer goes up.
        /// </summary>
        void OnPointerUp(double x, double y);

        /// <summary>
        /// Cancels the current tool action (usually mapped to Escape key or Right Click).
        /// </summary>
        void Cancel();
        
        /// <summary>
        /// Fired when the tool is deactivated from the UI.
        /// </summary>
        void OnDeactivated();
    }
}
