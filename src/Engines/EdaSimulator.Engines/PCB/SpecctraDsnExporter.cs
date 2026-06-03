using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace EdaSimulator.Engines.PCB
{
    /// <summary>
    /// Exports a <see cref="PcbDocument"/> to the Specctra DSN format consumed by FreeRouting.
    /// Specctra DSN specification — Cadence, Rev 2.0 (public domain reference).
    /// </summary>
    public static class SpecctraDsnExporter
    {
        private const double MM_TO_UM = 1000.0;  // DSN uses micro-meters internally

        /// <summary>
        /// Serializes the full PCB design into a Specctra .dsn string ready for FreeRouting.
        /// </summary>
        public static string Export(PcbDocument pcb)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"(pcb {Sanitize(pcb.Title)}");

            // ── Parser header ───────────────────────────────────────────────────────
            sb.AppendLine("  (parser");
            sb.AppendLine("    (string_quote \")");
            sb.AppendLine("    (space_in_quoted_tokens on)");
            sb.AppendLine("    (host_cad \"EdaSimulator\")");
            sb.AppendLine($"    (host_version \"1.0\")");
            sb.AppendLine("  )");

            // ── Resolution (1 unit = 1 µm) ─────────────────────────────────────────
            sb.AppendLine("  (resolution um 1)");
            sb.AppendLine("  (unit um)");

            // ── Structure (board outline + layers) ─────────────────────────────────
            AppendStructure(sb, pcb);

            // ── Placement (footprints at their current positions) ───────────────────
            AppendPlacement(sb, pcb);

            // ── Library (pad stacks) ───────────────────────────────────────────────
            AppendLibrary(sb, pcb);

            // ── Network (net list + pin assignments) ───────────────────────────────
            AppendNetwork(sb, pcb);

            // ── Wiring (already-routed traces & vias, if any) ─────────────────────
            AppendWiring(sb, pcb);

            sb.AppendLine(")");  // close (pcb …)

            return sb.ToString();
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Private sections
        // ────────────────────────────────────────────────────────────────────────────

        private static void AppendStructure(StringBuilder sb, PcbDocument pcb)
        {
            sb.AppendLine("  (structure");

            // Layer definitions
            sb.AppendLine("    (layer F.Cu");
            sb.AppendLine("      (type signal)");
            sb.AppendLine("      (property (index 0))");
            sb.AppendLine("    )");
            sb.AppendLine("    (layer B.Cu");
            sb.AppendLine("      (type signal)");
            sb.AppendLine("      (property (index 1))");
            sb.AppendLine("    )");

            // Board boundary
            double x0 = pcb.Outline.CornerX  * MM_TO_UM;
            double y0 = pcb.Outline.CornerY  * MM_TO_UM;
            double x1 = (pcb.Outline.CornerX + pcb.Outline.Width_mm)  * MM_TO_UM;
            double y1 = (pcb.Outline.CornerY + pcb.Outline.Height_mm) * MM_TO_UM;

            sb.AppendLine("    (boundary");
            sb.AppendLine($"      (rect pcb {F(x0)} {F(y0)} {F(x1)} {F(y1)})");
            sb.AppendLine("    )");

            // Global design rules
            double clearance  = pcb.Rules.MinClearance_mm  * MM_TO_UM;
            double traceWidth = pcb.Rules.DefaultTraceWidth_mm * MM_TO_UM;
            double viaDrill   = pcb.Rules.DefaultViaDrill_mm * MM_TO_UM;
            double viaPad     = pcb.Rules.DefaultViaPad_mm  * MM_TO_UM;

            sb.AppendLine("    (rule");
            sb.AppendLine($"      (clearance {F(clearance)})");
            sb.AppendLine($"      (width {F(traceWidth)})");
            sb.AppendLine("    )");

            // Default via padstack
            sb.AppendLine("    (via \"Via[0-1]_800:400\")");
            sb.AppendLine("  )");
        }

        private static void AppendPlacement(StringBuilder sb, PcbDocument pcb)
        {
            if (pcb.Footprints.Count == 0) return;

            sb.AppendLine("  (placement");

            // Group by footprint ID (library – footprint pair)
            var groups = pcb.Footprints.GroupBy(f => Sanitize(f.FootprintId));
            foreach (var group in groups)
            {
                sb.AppendLine($"    (component {group.Key}");
                foreach (var fp in group)
                {
                    double x = fp.X * MM_TO_UM;
                    double y = fp.Y * MM_TO_UM;
                    string side = (fp.Layer == PcbLayerType.BCu || fp.IsMirrored) ? "back" : "front";
                    sb.AppendLine($"      (place {Sanitize(fp.Designator)} {F(x)} {F(y)} {side} {F(fp.Rotation)})");
                }
                sb.AppendLine("    )");
            }

            sb.AppendLine("  )");
        }

        private static void AppendLibrary(StringBuilder sb, PcbDocument pcb)
        {
            sb.AppendLine("  (library");

            // Collect unique footprint types and emit padstacks
            var usedPadstacks = new HashSet<string>();

            foreach (var fp in pcb.Footprints)
            {
                sb.AppendLine($"    (image {Sanitize(fp.FootprintId)}");
                foreach (var pad in fp.Pads)
                {
                    string padstackName = BuildPadstackName(pad);
                    usedPadstacks.Add(padstackName);

                    double px = pad.X * MM_TO_UM;
                    double py = pad.Y * MM_TO_UM;
                    sb.AppendLine($"      (pin {padstackName} {Sanitize(pad.PadNumber)} {F(px)} {F(py)})");
                }
                sb.AppendLine("    )");
            }

            // Emit padstack definitions
            foreach (var psName in usedPadstacks)
            {
                sb.AppendLine($"    (padstack {psName}");
                // Simplified: all pads are circles on both copper layers
                double dia = psName.Contains("THT") ? 1600 : 900; // µm
                sb.AppendLine($"      (shape (circle F.Cu {F(dia)} 0 0))");
                sb.AppendLine($"      (shape (circle B.Cu {F(dia)} 0 0))");
                sb.AppendLine("      (attach off)");
                sb.AppendLine("    )");
            }

            // Default via padstack
            sb.AppendLine("    (padstack \"Via[0-1]_800:400\"");
            sb.AppendLine("      (shape (circle F.Cu 800 0 0))");
            sb.AppendLine("      (shape (circle B.Cu 800 0 0))");
            sb.AppendLine("      (attach off)");
            sb.AppendLine("    )");

            sb.AppendLine("  )");
        }

        private static void AppendNetwork(StringBuilder sb, PcbDocument pcb)
        {
            if (pcb.Ratsnest.Count == 0 && pcb.Traces.Count == 0) return;

            sb.AppendLine("  (network");

            // Build net → list of (designator, pad) from ratsnest
            var netPins = new Dictionary<string, List<(string Des, string Pad)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in pcb.Ratsnest)
            {
                if (!netPins.TryGetValue(r.NetName, out var list))
                    netPins[r.NetName] = list = new List<(string, string)>();

                if (!list.Any(p => p.Des == r.FromDesignator && p.Pad == r.FromPadNumber))
                    list.Add((r.FromDesignator, r.FromPadNumber));
                if (!list.Any(p => p.Des == r.ToDesignator && p.Pad == r.ToPadNumber))
                    list.Add((r.ToDesignator, r.ToPadNumber));
            }

            // Also add nets from existing routed traces
            foreach (var trace in pcb.Traces)
            {
                if (!string.IsNullOrEmpty(trace.NetName) && !netPins.ContainsKey(trace.NetName))
                    netPins[trace.NetName] = new List<(string, string)>();
            }

            foreach (var (netName, pins) in netPins)
            {
                sb.AppendLine($"    (net {Sanitize(netName)}");
                sb.AppendLine("      (pins");
                foreach (var (des, pad) in pins)
                    sb.AppendLine($"        {Sanitize(des + "-" + pad)}");
                sb.AppendLine("      )");
                sb.AppendLine("    )");
            }

            // Class rule
            sb.AppendLine("    (class default");
            sb.AppendLine($"      (circuit (use_via \"Via[0-1]_800:400\"))");
            sb.AppendLine($"      (rule (clearance {F(pcb.Rules.MinClearance_mm * MM_TO_UM)}) (width {F(pcb.Rules.DefaultTraceWidth_mm * MM_TO_UM)}))");
            sb.AppendLine("    )");

            sb.AppendLine("  )");
        }

        private static void AppendWiring(StringBuilder sb, PcbDocument pcb)
        {
            if (pcb.Traces.Count == 0 && pcb.Vias.Count == 0) return;

            sb.AppendLine("  (wiring");

            foreach (var trace in pcb.Traces)
            {
                string layer = trace.Layer == PcbLayerType.BCu ? "B.Cu" : "F.Cu";
                double w  = trace.Width_mm * MM_TO_UM;
                double x1 = trace.StartX   * MM_TO_UM;
                double y1 = trace.StartY   * MM_TO_UM;
                double x2 = trace.EndX     * MM_TO_UM;
                double y2 = trace.EndY     * MM_TO_UM;
                string net = Sanitize(trace.NetName);
                sb.AppendLine($"    (wire (path {layer} {F(w)} {F(x1)} {F(y1)} {F(x2)} {F(y2)}) (net {net}) (type route))");
            }

            foreach (var via in pcb.Vias)
            {
                double x   = via.X * MM_TO_UM;
                double y   = via.Y * MM_TO_UM;
                string net = Sanitize(via.NetName);
                sb.AppendLine($"    (via \"Via[0-1]_800:400\" {F(x)} {F(y)} (net {net}) (type route))");
            }

            sb.AppendLine("  )");
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────────────────────────────────────

        private static string F(double v)
            => v.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            // If the name has spaces, wrap in quotes
            if (s.Contains(' ') || s.Contains('/') || s.Contains('(') || s.Contains(')'))
                return $"\"{s}\"";
            return s;
        }

        private static string BuildPadstackName(PcbPad pad)
            => pad.Type == PadType.THT
                ? $"Pad_THT_{(int)(pad.Width_mm * 1000)}"
                : $"Pad_SMD_{(int)(pad.Width_mm * 1000)}x{(int)(pad.Height_mm * 1000)}";
    }
}
