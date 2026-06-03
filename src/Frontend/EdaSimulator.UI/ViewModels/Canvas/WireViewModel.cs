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

        [ObservableProperty]
        private string _wireColor = "#FF00FF00";

        [ObservableProperty]
        private string _opVoltageText = string.Empty;

        [ObservableProperty]
        private bool _showOpVoltage = false;

        [ObservableProperty]
        private double _labelX;

        [ObservableProperty]
        private double _labelY;

        partial void OnPointsChanged(PointCollection value)
        {
            UpdateLabelPosition();
        }

        public void UpdateLabelPosition()
        {
            if (Points == null || Points.Count < 2) return;

            // Calculate midpoint of the polyline segments
            int midIndex = Points.Count / 2;
            var p1 = Points[midIndex - 1];
            var p2 = Points[midIndex];

            LabelX = (p1.X + p2.X) / 2.0;
            LabelY = (p1.Y + p2.Y) / 2.0;
        }

        partial void OnNetLabelChanged(string value)
        {
            WireColor = GetColorForNet(value);
        }

        private string GetColorForNet(string netName)
        {
            if (string.IsNullOrWhiteSpace(netName))
                return "#FF00FF00"; // Default green for unnamed signal nets

            string nameLower = netName.ToLowerInvariant();

            // Ground nets (slate gray)
            if (nameLower == "0" || nameLower == "gnd" || nameLower == "ground")
                return "#FF7F8C8D";

            // Power rails (red)
            if (nameLower.Contains("vcc") || nameLower.Contains("vdd") || nameLower.Contains("v12") || nameLower.Contains("v5") || nameLower.Contains("v3.3") || nameLower.Contains("power"))
                return "#FFE74C3C";

            // Hash-based color for other custom named nets (e.g. "CLOCK", "RESET", "INPUT")
            int hash = 0;
            foreach (char c in netName)
            {
                hash = c + (hash << 5) - hash;
            }

            double h = Math.Abs(hash % 360);
            double s = 0.85; // 85% saturation
            double l = 0.60; // 60% lightness

            return HslToHex(h, s, l);
        }

        private static string HslToHex(double h, double s, double l)
        {
            double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
            double x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
            double m = l - c / 2.0;

            double r = 0, g = 0, b = 0;
            if (0 <= h && h < 60) { r = c; g = x; b = 0; }
            else if (60 <= h && h < 120) { r = x; g = c; b = 0; }
            else if (120 <= h && h < 180) { r = 0; g = c; b = x; }
            else if (180 <= h && h < 240) { r = 0; g = x; b = c; }
            else if (240 <= h && h < 300) { r = x; g = 0; b = c; }
            else if (300 <= h && h < 360) { r = c; g = 0; b = x; }

            byte rByte = (byte)Math.Min(255, Math.Max(0, (r + m) * 255));
            byte gByte = (byte)Math.Min(255, Math.Max(0, (g + m) * 255));
            byte bByte = (byte)Math.Min(255, Math.Max(0, (b + m) * 255));

            return $"#FF{rByte:X2}{gByte:X2}{bByte:X2}";
        }

        public WireViewModel(Guid targetNetId)
        {
            TargetNetId = targetNetId;
            ZIndex = 0; // Wires render below components
            _points = new PointCollection();
            _wireColor = GetColorForNet(NetLabel);
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

        public override void MoveBy(double dx, double dy)
        {
            // Wires do not move independently on drag
        }
    }
}
