using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace EdaSimulator.UI.ViewModels.Canvas
{
    /// <summary>
    /// Represents a polyline defining visual wire segments connecting pins.
    /// Multiple WireViewModels can map to the same Net ID in the core engine.
    /// </summary>
    public partial class WireViewModel : CanvasItemViewModel
    {
        public Guid TargetNetId { get; set; }

        /// <summary>
        /// Orthogonal points comprising the line.
        /// Ex: Pt1(10,10) -> Pt2(50,10) -> Pt3(50,80)
        /// </summary>
        public PointCollection Points { get; } = new PointCollection();

        public WireViewModel(Guid targetNetId)
        {
            TargetNetId = targetNetId;
            ZIndex = 0; // Wires render below components
        }
    }
}
