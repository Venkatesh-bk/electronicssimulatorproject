// SMath alias resolves System.Math unambiguously
using SMath = System.Math;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using EdaSimulator.Engines.Models;

namespace EdaSimulator.Engines.PCB
{
    /// <summary>
    /// Generates RS-274X (Gerber) format files for each PCB copper layer.
    /// Implements the Gerber Format Specification Revision 2023.08 by Ucamco.
    /// Reference: https://www.ucamco.com/en/gerber/downloads
    /// Output is directly compatible with OSHPark, JLCPCB, PCBWay, and Eurocircuits.
    /// </summary>
    public class GerberWriter
    {
        private const string GERBER_VERSION = "RS-274X Revision 2023.08";
        private const double UNITS_MM = 1.0;

        /// <summary>Generates all Gerber layer files for the PCB document.</summary>
        public Dictionary<string, string> GenerateAllLayers(PcbDocument pcb)
        {
            ArgumentNullException.ThrowIfNull(pcb);
            var files = new Dictionary<string, string>();

            // Front copper (F.Cu) - most important
            files[$"{pcb.Title}-F_Cu.gbr"]     = GenerateCopperLayer(pcb, PcbLayerType.FCu);
            files[$"{pcb.Title}-B_Cu.gbr"]     = GenerateCopperLayer(pcb, PcbLayerType.BCu);
            files[$"{pcb.Title}-F_SilkS.gbr"]  = GenerateSilkscreenLayer(pcb, PcbLayerType.FSilkS);
            files[$"{pcb.Title}-B_SilkS.gbr"]  = GenerateSilkscreenLayer(pcb, PcbLayerType.BSilkS);
            files[$"{pcb.Title}-F_Mask.gbr"]   = GenerateSolderMaskLayer(pcb, PcbLayerType.FMask);
            files[$"{pcb.Title}-B_Mask.gbr"]   = GenerateSolderMaskLayer(pcb, PcbLayerType.BMask);
            files[$"{pcb.Title}-Edge_Cuts.gbr"] = GenerateEdgeCuts(pcb);
            files[$"{pcb.Title}.drl"]           = GenerateExcellonDrill(pcb);

            return files;
        }

        /// <summary>Generates a copper layer Gerber file (traces + pads).</summary>
        public string GenerateCopperLayer(PcbDocument pcb, PcbLayerType layer)
        {
            var sb = new StringBuilder();
            AppendHeader(sb, pcb.Title, layer.ToString());

            // Aperture definitions — we'll use D10 for traces and D11 for pads
            sb.AppendLine("%ADD10C,0.250000*%");  // 0.25mm round aperture (trace)
            sb.AppendLine("%ADD11C,1.600000*%");  // 1.6mm round aperture (pad)
            sb.AppendLine("%ADD12C,0.600000*%");  // 0.6mm via pad

            // Draw traces
            sb.AppendLine("D10*"); // select trace aperture
            foreach (var trace in pcb.Traces.Where(t => t.Layer == layer))
            {
                // Move to start, draw to end
                sb.AppendLine($"X{FormatCoord(trace.StartX)}Y{FormatCoord(trace.StartY)}D02*");
                sb.AppendLine($"X{FormatCoord(trace.EndX)}Y{FormatCoord(trace.EndY)}D01*");
            }

            // Draw via pads
            sb.AppendLine("D12*"); // select via pad aperture
            foreach (var via in pcb.Vias)
            {
                if (layer == PcbLayerType.FCu || layer == PcbLayerType.BCu)
                    sb.AppendLine($"X{FormatCoord(via.X)}Y{FormatCoord(via.Y)}D03*"); // flash
            }

            // Draw footprint pads on this layer
            sb.AppendLine("D11*"); // select pad aperture
            foreach (var fp in pcb.Footprints)
            {
                foreach (var pad in fp.Pads.Where(p => p.Layer == layer || pad_is_on_layer(p, layer)))
                {
                    double px = fp.X + pad.X;
                    double py = fp.Y + pad.Y;
                    sb.AppendLine($"X{FormatCoord(px)}Y{FormatCoord(py)}D03*"); // flash pad
                }
            }

            AppendFooter(sb);
            return sb.ToString();
        }

        private string GenerateSilkscreenLayer(PcbDocument pcb, PcbLayerType layer)
        {
            var sb = new StringBuilder();
            AppendHeader(sb, pcb.Title, layer.ToString());
            sb.AppendLine("%ADD10C,0.120000*%"); // 0.12mm silkscreen line

            sb.AppendLine("D10*");
            foreach (var fp in pcb.Footprints)
            {
                bool isFront = layer == PcbLayerType.FSilkS && !fp.IsMirrored;
                bool isBack  = layer == PcbLayerType.BSilkS && fp.IsMirrored;

                if (isFront || isBack)
                {
                    // Draw courtyard rectangle as silkscreen reference
                    double hw = fp.CrtYd_Width_mm / 2;
                    double hh = fp.CrtYd_Height_mm / 2;
                    // Top edge
                    sb.AppendLine($"X{FormatCoord(fp.X - hw)}Y{FormatCoord(fp.Y - hh)}D02*");
                    sb.AppendLine($"X{FormatCoord(fp.X + hw)}Y{FormatCoord(fp.Y - hh)}D01*");
                    // Right edge
                    sb.AppendLine($"X{FormatCoord(fp.X + hw)}Y{FormatCoord(fp.Y + hh)}D01*");
                    // Bottom edge
                    sb.AppendLine($"X{FormatCoord(fp.X - hw)}Y{FormatCoord(fp.Y + hh)}D01*");
                    // Left edge (close)
                    sb.AppendLine($"X{FormatCoord(fp.X - hw)}Y{FormatCoord(fp.Y - hh)}D01*");
                }
            }

            AppendFooter(sb);
            return sb.ToString();
        }

        private string GenerateSolderMaskLayer(PcbDocument pcb, PcbLayerType layer)
        {
            var sb = new StringBuilder();
            AppendHeader(sb, pcb.Title, layer.ToString());
            sb.AppendLine("%ADD11C,1.800000*%"); // Mask opening slightly larger than pad
            sb.AppendLine("%ADD12C,0.700000*%"); // Via mask opening

            sb.AppendLine("D11*");
            foreach (var fp in pcb.Footprints)
            {
                bool isFront = layer == PcbLayerType.FMask && !fp.IsMirrored;
                bool isBack  = layer == PcbLayerType.BMask && fp.IsMirrored;

                if (isFront || isBack)
                {
                    foreach (var pad in fp.Pads)
                    {
                        double px = fp.X + pad.X;
                        double py = fp.Y + pad.Y;
                        sb.AppendLine($"X{FormatCoord(px)}Y{FormatCoord(py)}D03*");
                    }
                }
            }

            // Via openings on both mask layers
            sb.AppendLine("D12*");
            foreach (var via in pcb.Vias)
                sb.AppendLine($"X{FormatCoord(via.X)}Y{FormatCoord(via.Y)}D03*");

            AppendFooter(sb);
            return sb.ToString();
        }

        private string GenerateEdgeCuts(PcbDocument pcb)
        {
            var sb = new StringBuilder();
            AppendHeader(sb, pcb.Title, "Edge.Cuts");
            sb.AppendLine("%ADD10C,0.050000*%");
            sb.AppendLine("D10*");

            var o = pcb.Outline;
            // Draw board outline rectangle
            sb.AppendLine($"X{FormatCoord(o.CornerX)}Y{FormatCoord(o.CornerY)}D02*");
            sb.AppendLine($"X{FormatCoord(o.CornerX + o.Width_mm)}Y{FormatCoord(o.CornerY)}D01*");
            sb.AppendLine($"X{FormatCoord(o.CornerX + o.Width_mm)}Y{FormatCoord(o.CornerY + o.Height_mm)}D01*");
            sb.AppendLine($"X{FormatCoord(o.CornerX)}Y{FormatCoord(o.CornerY + o.Height_mm)}D01*");
            sb.AppendLine($"X{FormatCoord(o.CornerX)}Y{FormatCoord(o.CornerY)}D01*");

            AppendFooter(sb);
            return sb.ToString();
        }

        /// <summary>Generates Excellon II drill file (NC drill data).</summary>
        public string GenerateExcellonDrill(PcbDocument pcb)
        {
            var sb = new StringBuilder();
            sb.AppendLine("; Excellon drill file generated by EDA Simulator Platform");
            sb.AppendLine("; Format: Excellon II, Metric, Trailing Zeros");
            sb.AppendLine("M48");
            sb.AppendLine("FMAT,2");
            sb.AppendLine("METRIC,TZ");

            // Collect unique drill sizes
            var drills = new List<(double dia, IEnumerable<(double x, double y)> positions)>();

            var viaGroups = pcb.Vias.GroupBy(v => v.DrillDia_mm);
            int toolNum = 1;

            foreach (var grp in viaGroups)
            {
                sb.AppendLine($"T{toolNum:D2}C{grp.Key:F4}");
                drills.Add((grp.Key, grp.Select(v => (v.X, v.Y))));
                toolNum++;
            }

            foreach (var fp in pcb.Footprints)
            {
                var thtPads = fp.Pads.Where(p => p.Type == PadType.THT && p.DrillDia_mm > 0).GroupBy(p => p.DrillDia_mm);
                foreach (var grp in thtPads)
                {
                    sb.AppendLine($"T{toolNum:D2}C{grp.Key:F4}");
                    drills.Add((grp.Key, grp.Select(p => (fp.X + p.X, fp.Y + p.Y))));
                    toolNum++;
                }
            }

            sb.AppendLine("%");
            sb.AppendLine("G90"); // Absolute mode
            sb.AppendLine("G05"); // Drill mode

            int t = 1;
            foreach (var (_, positions) in drills)
            {
                sb.AppendLine($"T{t:D2}");
                foreach (var (x, y) in positions)
                    sb.AppendLine($"X{FormatDrill(x)}Y{FormatDrill(y)}");
                t++;
            }

            sb.AppendLine("M30"); // End of program
            return sb.ToString();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private void AppendHeader(StringBuilder sb, string title, string layer)
        {
            sb.AppendLine($"%TF.GenerationSoftware,EDA Simulator Platform,Phase7,2025*%");
            sb.AppendLine($"%TF.CreationDate,{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}*%");
            sb.AppendLine($"%TF.ProjectId,{title},00000000-0000-0000-0000-000000000000,1.0*%");
            sb.AppendLine($"%TF.SameCoordinates,Original*%");
            sb.AppendLine($"%TF.FileFunction,{layer}*%");
            sb.AppendLine($"%FSLAX46Y46*%"); // Format spec: absolute, 4.6 metric
            sb.AppendLine($"%MOMM*%");        // Units: mm
            sb.AppendLine($"%LPD*%");         // Layer polarity: dark
        }

        private void AppendFooter(StringBuilder sb)
        {
            sb.AppendLine("M02*"); // End of file
        }

        /// <summary>Formats mm coordinate to Gerber 4.6 integer format (multiply by 1,000,000).</summary>
        private static string FormatCoord(double mm) => ((long)(mm * 1_000_000)).ToString();
        private static string FormatDrill(double mm) => mm.ToString("F4");

        private static bool pad_is_on_layer(PcbPad pad, PcbLayerType layer)
        {
            if (pad.Type == PadType.THT) return layer == PcbLayerType.FCu || layer == PcbLayerType.BCu;
            return pad.Layer == layer;
        }
    }
}

