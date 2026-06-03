using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace EdaSimulator.Engines.PCB
{
    /// <summary>
    /// Parses the Specctra Session (.ses) file output by FreeRouting back into
    /// routed <see cref="PcbTrace"/> and <see cref="PcbVia"/> objects.
    /// </summary>
    public static class SpecctraSessionImporter
    {
        private const double UM_TO_MM = 0.001;

        /// <summary>
        /// Reads a FreeRouting .ses file and populates the supplied
        /// <see cref="PcbDocument"/> with the routed traces and vias.
        /// The existing unrouted ratsnest is cleared on success.
        /// </summary>
        /// <returns>Number of route segments imported.</returns>
        public static int Import(string sesFilePath, PcbDocument pcb)
        {
            if (!File.Exists(sesFilePath))
                throw new FileNotFoundException($"Specctra session file not found: {sesFilePath}");

            string content = File.ReadAllText(sesFilePath);

            pcb.Traces.Clear();
            pcb.Vias.Clear();

            int count = 0;

            // ── Parse wires ──────────────────────────────────────────────────────────
            // Pattern: (wire (path <layer> <width> <x1> <y1> <x2> <y2>) (net <name>) …)
            var wireRegex = new Regex(
                @"\(wire\s+\(path\s+(\S+)\s+([\d.E+\-]+)\s+([\d.E+\-]+)\s+([\d.E+\-]+)\s+([\d.E+\-]+)\s+([\d.E+\-]+)\)\s+\(net\s+((?:""[^""]*"")|\S+)\)",
                RegexOptions.IgnoreCase);

            foreach (Match m in wireRegex.Matches(content))
            {
                string layer    = m.Groups[1].Value;
                double widthUm  = ParseDouble(m.Groups[2].Value);
                double x1Um     = ParseDouble(m.Groups[3].Value);
                double y1Um     = ParseDouble(m.Groups[4].Value);
                double x2Um     = ParseDouble(m.Groups[5].Value);
                double y2Um     = ParseDouble(m.Groups[6].Value);
                string netName  = m.Groups[7].Value.Trim('"');

                var trace = new PcbTrace
                {
                    StartX   = x1Um * UM_TO_MM,
                    StartY   = y1Um * UM_TO_MM,
                    EndX     = x2Um * UM_TO_MM,
                    EndY     = y2Um * UM_TO_MM,
                    Width_mm = widthUm * UM_TO_MM,
                    Layer    = ParseLayer(layer),
                    NetName  = netName
                };
                pcb.Traces.Add(trace);
                count++;
            }

            // ── Parse vias ───────────────────────────────────────────────────────────
            // Pattern: (via "<padstackName>" <x> <y> (net <name>) …)
            var viaRegex = new Regex(
                @"\(via\s+""?[^""]*""?\s+([\d.E+\-]+)\s+([\d.E+\-]+)\s+\(net\s+((?:""[^""]*"")|\S+)\)",
                RegexOptions.IgnoreCase);

            foreach (Match m in viaRegex.Matches(content))
            {
                double xUm    = ParseDouble(m.Groups[1].Value);
                double yUm    = ParseDouble(m.Groups[2].Value);
                string netName = m.Groups[3].Value.Trim('"');

                var via = new PcbVia
                {
                    X           = xUm * UM_TO_MM,
                    Y           = yUm * UM_TO_MM,
                    DrillDia_mm = 0.3,
                    PadDia_mm   = 0.6,
                    LayerFrom   = PcbLayerType.FCu,
                    LayerTo     = PcbLayerType.BCu,
                    NetName     = netName
                };
                pcb.Vias.Add(via);
            }

            // All ratsnest connections are now routed
            pcb.Ratsnest.Clear();

            return count;
        }

        private static PcbLayerType ParseLayer(string layerName)
        {
            return layerName.ToUpperInvariant() switch
            {
                "B.CU" or "BCU" or "BACK" => PcbLayerType.BCu,
                "IN1.CU" or "IN1CU"       => PcbLayerType.In1Cu,
                "IN2.CU" or "IN2CU"       => PcbLayerType.In2Cu,
                _                          => PcbLayerType.FCu
            };
        }

        private static double ParseDouble(string s)
            => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0.0;
    }
}
