using System;
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

        /// <summary>Engine Pin ID of the wire's start endpoint.</summary>
        public Guid StartPinId { get; set; }

        /// <summary>Engine Pin ID of the wire's end endpoint.</summary>
        public Guid EndPinId { get; set; }

        /// <summary>Human-readable net name label shown at wire midpoint.</summary>
        [ObservableProperty]
        private string _netLabel = string.Empty;

        /// <summary>
        /// Orthogonal points comprising the line.
        /// Ex: Pt1(10,10) -> Pt2(50,10) -> Pt3(50,80)
        /// </summary>
        [ObservableProperty]
        private PointCollection _points;

        public WireViewModel(Guid targetNetId)
        {
            TargetNetId = targetNetId;
            ZIndex = 0; // Wires render below components
            _points = new PointCollection();
        }

        /// <summary>
        /// Updates one endpoint of the wire (start or end) when the attached pin moves.
        /// Keeps the routing valid after a component drag.
        /// </summary>
        public void UpdateEndpoint(Guid pinId, double newX, double newY)
        {
            if (Points == null || Points.Count < 2) return;

            var pts = new PointCollection(Points);
            if (pinId == StartPinId)
            {
                // Move the first point and the first orthogonal corner
                pts[0] = new System.Windows.Point(newX, newY);
                if (pts.Count >= 2)
                    pts[1] = new System.Windows.Point(pts[1].X, newY);
            }
            else if (pinId == EndPinId)
            {
                // Move the last point and the last orthogonal corner
                int last = pts.Count - 1;
                pts[last] = new System.Windows.Point(newX, newY);
                if (last >= 1)
                    pts[last - 1] = new System.Windows.Point(newX, pts[last - 1].Y);
            }
            Points = pts;
        }
    }
}
