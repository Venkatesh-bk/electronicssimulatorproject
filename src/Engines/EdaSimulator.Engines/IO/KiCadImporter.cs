using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using EdaSimulator.Engines.PCB;
using SMath = System.Math;

namespace EdaSimulator.Engines.IO
{
    /// <summary>
    /// Imports KiCad PCB (.kicad_pcb) files, parsing footprints, tracks, vias,
    /// and board outline Edge.Cuts geometry to reconstruct a PcbDocument.
    /// </summary>
    public static class KiCadImporter
    {
        private static double ParseDouble(string s)
        {
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0.0;
        }

        private static PcbLayerType ParseLayer(string layer)
        {
            layer = layer.Replace("\"", "").Trim();
            return layer switch
            {
                "F.Cu" or "FCu" => PcbLayerType.FCu,
                "B.Cu" or "BCu" => PcbLayerType.BCu,
                "F.Mask" => PcbLayerType.FMask,
                "B.Mask" => PcbLayerType.BMask,
                "F.SilkS" => PcbLayerType.FSilkS,
                "B.SilkS" => PcbLayerType.BSilkS,
                "Edge.Cuts" => PcbLayerType.EdgeCuts,
                _ => PcbLayerType.FCu
            };
        }

        /// <summary>
        /// Reads a .kicad_pcb file and returns a populated PcbDocument.
        /// </summary>
        public static PcbDocument Import(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"KiCad PCB file not found: {filePath}");

            var pcb = new PcbDocument();
            string content = File.ReadAllText(filePath);

            // 1. Parse nets lookup (net index to net name)
            // Example: (net 1 "GND")
            var netLookup = new Dictionary<string, string>();
            var netRegex = new Regex(@"\(net\s+(\d+)\s+""([^""]*)""\)", RegexOptions.IgnoreCase);
            foreach (Match m in netRegex.Matches(content))
            {
                netLookup[m.Groups[1].Value] = m.Groups[2].Value;
            }

            // 2. Parse board outline via Edge.Cuts lines and rects
            // Example: (gr_line (start 10 20) (end 110 20) (stroke (width 0.1)) (layer "Edge.Cuts"))
            var edgeCutsRegex = new Regex(@"\((gr_line|gr_rect)\s+\((?:start|end|at)\s+([\d.-]+)\s+([\d.-]+)\)\s+\((?:start|end|size)\s+([\d.-]+)\s+([\d.-]+)\).*?Edge\.Cuts", RegexOptions.IgnoreCase);
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            bool foundOutline = false;

            foreach (Match m in edgeCutsRegex.Matches(content))
            {
                double x1 = ParseDouble(m.Groups[2].Value);
                double y1 = ParseDouble(m.Groups[3].Value);
                double x2 = ParseDouble(m.Groups[4].Value);
                double y2 = ParseDouble(m.Groups[5].Value);

                minX = SMath.Min(minX, SMath.Min(x1, x2));
                maxX = SMath.Max(maxX, SMath.Max(x1, x2));
                minY = SMath.Min(minY, SMath.Min(y1, y2));
                maxY = SMath.Max(maxY, SMath.Max(y1, y2));
                foundOutline = true;
            }

            if (foundOutline)
            {
                pcb.Outline.CornerX = minX;
                pcb.Outline.CornerY = minY;
                pcb.Outline.Width_mm = maxX - minX;
                pcb.Outline.Height_mm = maxY - minY;
            }

            // 3. Parse segments / traces
            // Example: (segment (start 10.5 20.3) (end 15.2 20.3) (width 0.25) (layer "F.Cu") (net 1))
            var segmentRegex = new Regex(@"\(segment\s+\(start\s+([\d.-]+)\s+([\d.-]+)\)\s+\(end\s+([\d.-]+)\s+([\d.-]+)\)\s+\(width\s+([\d.-]+)\)\s+\(layer\s+""?([^""]+)""?\)\s+\(net\s+(\d+)\)", RegexOptions.IgnoreCase);
            foreach (Match m in segmentRegex.Matches(content))
            {
                string netIndex = m.Groups[7].Value;
                string netName = netLookup.TryGetValue(netIndex, out var name) ? name : $"Net-{netIndex}";

                pcb.Traces.Add(new PcbTrace
                {
                    StartX = ParseDouble(m.Groups[1].Value),
                    StartY = ParseDouble(m.Groups[2].Value),
                    EndX = ParseDouble(m.Groups[3].Value),
                    EndY = ParseDouble(m.Groups[4].Value),
                    Width_mm = ParseDouble(m.Groups[5].Value),
                    Layer = ParseLayer(m.Groups[6].Value),
                    NetName = netName
                });
            }

            // 4. Parse vias
            // Example: (via (at 10 20) (size 0.6) (drill 0.3) (layers "F.Cu" "B.Cu") (net 1))
            var viaRegex = new Regex(@"\(via\s+\(at\s+([\d.-]+)\s+([\d.-]+)\)\s+\(size\s+([\d.-]+)\)\s+\(drill\s+([\d.-]+)\)\s+\(layers\s+""?([^""]+)""?\s+""?([^""]+)""?\)\s+\(net\s+(\d+)\)", RegexOptions.IgnoreCase);
            foreach (Match m in viaRegex.Matches(content))
            {
                string netIndex = m.Groups[7].Value;
                string netName = netLookup.TryGetValue(netIndex, out var name) ? name : $"Net-{netIndex}";

                pcb.Vias.Add(new PcbVia
                {
                    X = ParseDouble(m.Groups[1].Value),
                    Y = ParseDouble(m.Groups[2].Value),
                    PadDia_mm = ParseDouble(m.Groups[3].Value),
                    DrillDia_mm = ParseDouble(m.Groups[4].Value),
                    LayerFrom = ParseLayer(m.Groups[5].Value),
                    LayerTo = ParseLayer(m.Groups[6].Value),
                    NetName = netName
                });
            }

            // 5. Parse footprints
            List<string> fpBlocks = ExtractTopLevelSExpressions(content, "footprint", "module");
            foreach (var fpBlock in fpBlocks)
            {
                var footprint = ParseFootprintBlock(fpBlock, netLookup);
                if (footprint != null)
                {
                    pcb.Footprints.Add(footprint);
                }
            }

            return pcb;
        }

        private static List<string> ExtractTopLevelSExpressions(string content, params string[] keywords)
        {
            var results = new List<string>();
            int index = 0;
            while (index < content.Length)
            {
                int start = content.IndexOf('(', index);
                if (start == -1) break;

                int nextWordStart = start + 1;
                while (nextWordStart < content.Length && char.IsWhiteSpace(content[nextWordStart]))
                    nextWordStart++;

                bool matched = false;
                foreach (var word in keywords)
                {
                    if (nextWordStart + word.Length < content.Length &&
                        content.Substring(nextWordStart, word.Length) == word &&
                        char.IsWhiteSpace(content[nextWordStart + word.Length]))
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    int balance = 0;
                    int end = start;
                    while (end < content.Length)
                    {
                        if (content[end] == '(') balance++;
                        else if (content[end] == ')')
                        {
                            balance--;
                            if (balance == 0)
                            {
                                results.Add(content.Substring(start, end - start + 1));
                                break;
                            }
                        }
                        end++;
                    }
                    index = end + 1;
                }
                else
                {
                    index = start + 1;
                }
            }
            return results;
        }

        private static PcbFootprint? ParseFootprintBlock(string block, Dictionary<string, string> netLookup)
        {
            var headerMatch = Regex.Match(block, @"\b(?:footprint|module)\s+""?([^""\s)]+)""?", RegexOptions.IgnoreCase);
            if (!headerMatch.Success) return null;

            var fullId = headerMatch.Groups[1].Value;
            var parts = fullId.Split(':');
            var lib = parts.Length > 1 ? parts[0] : "Local";
            var fpId = parts.Length > 1 ? parts[1] : parts[0];

            var footprint = new PcbFootprint
            {
                Library = lib,
                FootprintId = fpId
            };

            var atMatch = Regex.Match(block, @"\bat\s+([\d.-]+)\s+([\d.-]+)(?:\s+([\d.-]+))?\b", RegexOptions.IgnoreCase);
            if (atMatch.Success)
            {
                footprint.X = ParseDouble(atMatch.Groups[1].Value);
                footprint.Y = ParseDouble(atMatch.Groups[2].Value);
                if (atMatch.Groups[3].Success)
                {
                    footprint.Rotation = ParseDouble(atMatch.Groups[3].Value);
                }
            }

            var refPropMatch = Regex.Match(block, @"\(property\s+""Reference""\s+""([^""]+)""", RegexOptions.IgnoreCase);
            if (refPropMatch.Success)
            {
                footprint.Designator = refPropMatch.Groups[1].Value;
            }
            else
            {
                var refTextMatch = Regex.Match(block, @"\(fp_text\s+reference\s+""?([^""\s)]+)""?", RegexOptions.IgnoreCase);
                if (refTextMatch.Success) footprint.Designator = refTextMatch.Groups[1].Value;
            }

            var valPropMatch = Regex.Match(block, @"\(property\s+""Value""\s+""([^""]+)""", RegexOptions.IgnoreCase);
            if (valPropMatch.Success)
            {
                footprint.Value = valPropMatch.Groups[1].Value;
            }
            else
            {
                var valTextMatch = Regex.Match(block, @"\(fp_text\s+value\s+""?([^""\s)]+)""?", RegexOptions.IgnoreCase);
                if (valTextMatch.Success) footprint.Value = valTextMatch.Groups[1].Value;
            }

            var padBlocks = ExtractTopLevelSExpressions(block, "pad");
            foreach (var padBlock in padBlocks)
            {
                var pad = ParsePadBlock(padBlock, footprint.X, footprint.Y, footprint.Rotation, netLookup);
                if (pad != null)
                {
                    footprint.Pads.Add(pad);
                }
            }

            return footprint;
        }

        private static PcbPad? ParsePadBlock(string padBlock, double fpX, double fpY, double fpRot, Dictionary<string, string> netLookup)
        {
            var match = Regex.Match(padBlock, @"\(pad\s+""?([^""\s)]+)""?\s+(\S+)\s+(\S+)", RegexOptions.IgnoreCase);
            if (!match.Success) return null;

            string num = match.Groups[1].Value;
            string typeStr = match.Groups[2].Value.ToLower();
            
            var type = PadType.SMD;
            if (typeStr == "thru_hole") type = PadType.THT;
            else if (typeStr == "npth") type = PadType.NPTH;

            var pad = new PcbPad
            {
                PadNumber = num,
                Type = type
            };

            double dx = 0, dy = 0;
            var atMatch = Regex.Match(padBlock, @"\bat\s+([\d.-]+)\s+([\d.-]+)\b", RegexOptions.IgnoreCase);
            if (atMatch.Success)
            {
                dx = ParseDouble(atMatch.Groups[1].Value);
                dy = ParseDouble(atMatch.Groups[2].Value);
            }

            double rad = fpRot * SMath.PI / 180.0;
            double cos = SMath.Cos(rad);
            double sin = SMath.Sin(rad);

            pad.X = fpX + (dx * cos - dy * sin);
            pad.Y = fpY + (dx * sin + dy * cos);

            var sizeMatch = Regex.Match(padBlock, @"\(size\s+([\d.-]+)\s+([\d.-]+)\)", RegexOptions.IgnoreCase);
            if (sizeMatch.Success)
            {
                pad.Width_mm = ParseDouble(sizeMatch.Groups[1].Value);
                pad.Height_mm = ParseDouble(sizeMatch.Groups[2].Value);
            }

            var drillMatch = Regex.Match(padBlock, @"\(drill\s+([\d.-]+)\)", RegexOptions.IgnoreCase);
            if (drillMatch.Success)
            {
                pad.DrillDia_mm = ParseDouble(drillMatch.Groups[1].Value);
            }

            var layerMatch = Regex.Match(padBlock, @"\(layers\s+""?([^""\s)]+)""", RegexOptions.IgnoreCase);
            if (layerMatch.Success)
            {
                pad.Layer = ParseLayer(layerMatch.Groups[1].Value);
            }

            var netMatch = Regex.Match(padBlock, @"\(net\s+(\d+)(?:\s+""([^""]*)"")?\)", RegexOptions.IgnoreCase);
            if (netMatch.Success)
            {
                string netIndex = netMatch.Groups[1].Value;
                if (netMatch.Groups[2].Success)
                {
                    pad.NetName = netMatch.Groups[2].Value;
                }
                else
                {
                    pad.NetName = netLookup.TryGetValue(netIndex, out var name) ? name : $"Net-{netIndex}";
                }
            }

            return pad;
        }
    }
}
