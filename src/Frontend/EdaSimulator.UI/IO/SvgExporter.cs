using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Globalization;
using System.Windows.Media;
using System.Windows;
using EdaSimulator.UI.ViewModels.Canvas;

namespace EdaSimulator.UI.IO
{
    /// <summary>
    /// Exports a schematic visual graph into a clean, high-fidelity vector SVG file.
    /// Maps WPF viewmodels (components, pins, wires) to scalable vector shapes.
    /// </summary>
    public static class SvgExporter
    {
        public static string Export(SchematicViewModel viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));

            var components = viewModel.Items.OfType<ComponentNodeViewModel>().ToList();
            var wires = viewModel.Items.OfType<WireViewModel>().ToList();

            // 1. Calculate bounding box of the schematic
            double minX = 0;
            double minY = 0;
            double maxX = 800;
            double maxY = 600;

            if (components.Count > 0 || wires.Count > 0)
            {
                minX = double.PositiveInfinity;
                minY = double.PositiveInfinity;
                maxX = double.NegativeInfinity;
                maxY = double.NegativeInfinity;

                foreach (var comp in components)
                {
                    minX = Math.Min(minX, comp.X);
                    minY = Math.Min(minY, comp.Y);
                    maxX = Math.Max(maxX, comp.X + comp.BoundsWidth);
                    maxY = Math.Max(maxY, comp.Y + comp.BoundsHeight);
                }

                foreach (var wire in wires)
                {
                    if (wire.Points != null)
                    {
                        foreach (var pt in wire.Points)
                        {
                            minX = Math.Min(minX, pt.X);
                            minY = Math.Min(minY, pt.Y);
                            maxX = Math.Max(maxX, pt.X);
                            maxY = Math.Max(maxY, pt.Y);
                        }
                    }
                }

                // Add margin padding (50px)
                minX -= 50;
                minY -= 50;
                maxX += 50;
                maxY += 50;

                // Enforce minimum width/height of 200px
                if (maxX - minX < 200) maxX = minX + 200;
                if (maxY - minY < 200) maxY = minY + 200;
            }

            double width = maxX - minX;
            double height = maxY - minY;

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" ");
            sb.AppendLine($"     width=\"{width.ToString("F0", CultureInfo.InvariantCulture)}\" ");
            sb.AppendLine($"     height=\"{height.ToString("F0", CultureInfo.InvariantCulture)}\" ");
            sb.AppendLine($"     viewBox=\"{minX.ToString("F0", CultureInfo.InvariantCulture)} {minY.ToString("F0", CultureInfo.InvariantCulture)} {width.ToString("F0", CultureInfo.InvariantCulture)} {height.ToString("F0", CultureInfo.InvariantCulture)}\">");
            
            // CSS styles for fonts and rendering
            sb.AppendLine("  <style>");
            sb.AppendLine("    .bg { fill: #0D0D1A; }");
            sb.AppendLine("    .wire { stroke: #00FF00; stroke-width: 2px; fill: none; stroke-linecap: round; stroke-linejoin: round; }");
            sb.AppendLine("    .net-label-bg { fill: #1E1E1E; stroke: #00FF00; stroke-width: 1px; }");
            sb.AppendLine("    .net-label-text { fill: #00BB00; font-family: Consolas, monospace; font-size: 9px; font-weight: bold; }");
            sb.AppendLine("    .comp-path { fill: none; stroke-width: 2px; stroke-linecap: round; stroke-linejoin: round; }");
            sb.AppendLine("    .comp-designator { fill: #FFFFFF; font-family: 'Segoe UI', Helvetica, sans-serif; font-size: 10px; font-weight: bold; text-anchor: middle; }");
            sb.AppendLine("    .comp-value { fill: #90CAF9; font-family: 'Segoe UI', Helvetica, sans-serif; font-size: 9px; text-anchor: middle; }");
            sb.AppendLine("    .comp-power { fill: #E74C3C; font-family: 'Segoe UI', Helvetica, sans-serif; font-size: 9px; font-weight: bold; text-anchor: middle; }");
            sb.AppendLine("    .pin-wired { fill: #00FF00; stroke: none; }");
            sb.AppendLine("    .pin-floating { fill: #FF0000; stroke: none; }");
            sb.AppendLine("  </style>");

            // 2. Draw canvas dark background
            sb.AppendLine($"  <rect class=\"bg\" x=\"{minX.ToString("F0", CultureInfo.InvariantCulture)}\" y=\"{minY.ToString("F0", CultureInfo.InvariantCulture)}\" width=\"100%\" height=\"100%\" />");

            // 3. Draw Wires
            sb.AppendLine("  <!-- Wires -->");
            foreach (var wire in wires)
            {
                if (wire.Points == null || wire.Points.Count < 2) continue;

                var ptsStr = string.Join(" ", wire.Points.Select(p => $"{p.X.ToString("F2", CultureInfo.InvariantCulture)},{p.Y.ToString("F2", CultureInfo.InvariantCulture)}"));
                sb.AppendLine($"  <polyline class=\"wire\" points=\"{ptsStr}\" />");

                // If wire has net label, render at midpoint
                if (!string.IsNullOrEmpty(wire.NetLabel))
                {
                    var mid = GetWireMidpoint(wire.Points);
                    // Draw a nice label background card and text
                    double textWidth = wire.NetLabel.Length * 6.0 + 6.0;
                    double badgeX = mid.X - textWidth / 2.0;
                    double badgeY = mid.Y - 14;

                    sb.AppendLine($"  <rect class=\"net-label-bg\" x=\"{badgeX.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{badgeY.ToString("F2", CultureInfo.InvariantCulture)}\" width=\"{textWidth.ToString("F2", CultureInfo.InvariantCulture)}\" height=\"11\" rx=\"2\" ry=\"2\" />");
                    sb.AppendLine($"  <text class=\"net-label-text\" x=\"{mid.X.ToString("F2", CultureInfo.InvariantCulture)}\" y=\"{(mid.Y - 5).ToString("F2", CultureInfo.InvariantCulture)}\" text-anchor=\"middle\">{EscapeXml(wire.NetLabel)}</text>");
                }
            }

            // 4. Draw Components
            sb.AppendLine("  <!-- Components -->");
            foreach (var comp in components)
            {
                double centerX = comp.X + comp.BoundsWidth / 2.0;
                double centerY = comp.Y + comp.BoundsHeight / 2.0;

                string strokeColor = "#4FC3F7"; // default light blue
                if (comp.IsGround) strokeColor = "#95A5A6"; // gray
                else if (comp.IsPower) strokeColor = "#E74C3C"; // red

                sb.AppendLine("  <g>");
                // Transformation group for rotated & scaled component body path
                string transform = $"translate({centerX.ToString("F3", CultureInfo.InvariantCulture)}, {centerY.ToString("F3", CultureInfo.InvariantCulture)})";
                if (comp.RotationAngle != 0)
                    transform += $" rotate({comp.RotationAngle.ToString("F1", CultureInfo.InvariantCulture)})";
                if (comp.ScaleX != 1.0 || comp.ScaleY != 1.0)
                    transform += $" scale({comp.ScaleX.ToString("F1", CultureInfo.InvariantCulture)}, {comp.ScaleY.ToString("F1", CultureInfo.InvariantCulture)})";

                sb.AppendLine($"    <path class=\"comp-path\" stroke=\"{strokeColor}\" d=\"{comp.PathData}\" transform=\"{transform}\" />");

                // Draw component labels (designator / value / power)
                if (comp.IsPowerSymbol)
                {
                    double labelY = comp.Y - 6;
                    sb.AppendLine($"    <text class=\"comp-power\" x=\"{centerX.ToString("F3", CultureInfo.InvariantCulture)}\" y=\"{labelY.ToString("F3", CultureInfo.InvariantCulture)}\">{EscapeXml(comp.PowerLabel)}</text>");
                }
                else
                {
                    double desY = comp.Y - 6;
                    double valY = comp.Y + comp.BoundsHeight + 12;
                    sb.AppendLine($"    <text class=\"comp-designator\" x=\"{centerX.ToString("F3", CultureInfo.InvariantCulture)}\" y=\"{desY.ToString("F3", CultureInfo.InvariantCulture)}\">{EscapeXml(comp.Designator)}</text>");
                    sb.AppendLine($"    <text class=\"comp-value\" x=\"{centerX.ToString("F3", CultureInfo.InvariantCulture)}\" y=\"{valY.ToString("F3", CultureInfo.InvariantCulture)}\">{EscapeXml(comp.Value)}</text>");
                }
                sb.AppendLine("  </g>");
            }

            // 5. Draw Pins
            sb.AppendLine("  <!-- Pins -->");
            foreach (var comp in components)
            {
                foreach (var pin in comp.Pins)
                {
                    string pinClass = pin.IsConnected ? "pin-wired" : "pin-floating";
                    // Pins are 4x4px small squares on canvas, let's draw them as 4x4 rects or small circles
                    sb.AppendLine($"  <circle class=\"{pinClass}\" cx=\"{pin.X.ToString("F2", CultureInfo.InvariantCulture)}\" cy=\"{pin.Y.ToString("F2", CultureInfo.InvariantCulture)}\" r=\"2.5\" />");
                }
            }

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        private static Point GetWireMidpoint(PointCollection points)
        {
            if (points == null || points.Count == 0) return new Point(0, 0);
            if (points.Count == 1) return points[0];

            double totalLength = 0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                totalLength += Math.Sqrt(Math.Pow(points[i + 1].X - points[i].X, 2) + Math.Pow(points[i + 1].Y - points[i].Y, 2));
            }

            double halfLength = totalLength / 2.0;
            double currentLength = 0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                double segLength = Math.Sqrt(Math.Pow(points[i + 1].X - points[i].X, 2) + Math.Pow(points[i + 1].Y - points[i].Y, 2));
                if (currentLength + segLength >= halfLength)
                {
                    double ratio = (halfLength - currentLength) / segLength;
                    double x = points[i].X + ratio * (points[i + 1].X - points[i].X);
                    double y = points[i].Y + ratio * (points[i + 1].Y - points[i].Y);
                    return new Point(x, y);
                }
                currentLength += segLength;
            }

            return points[points.Count - 1];
        }

        private static string EscapeXml(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("&", "&amp;")
                      .Replace("<", "&lt;")
                      .Replace(">", "&gt;")
                      .Replace("\"", "&quot;")
                      .Replace("'", "&apos;");
        }
    }
}
