// SMath alias resolves System.Math unambiguously
using SMath = System.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EdaSimulator.Engines.Models;

namespace EdaSimulator.Engines.PCB
{
    /// <summary>
    /// Generates Bill of Materials (BOM) and Pick-and-Place data from a Schematic.
    /// Outputs: CSV (Excel-compatible), JSON, and Centroid/Pick-and-Place CSV for SMT assembly.
    /// Format follows IPC-7711B and industry conventions (JLCPCB, Mouser, DigiKey BOM format).
    /// </summary>
    public static class BomGenerator
    {
        // ──────────────────────────────────────────────────────────────────────────────
        // BOM Generation from Schematic
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Generates a structured BOM from the schematic component list.
        /// Identical components (same type + value) are grouped into line items.
        /// </summary>
        public static List<BomLineItem> GenerateBom(Schematic schematic)
        {
            ArgumentNullException.ThrowIfNull(schematic);

            // Group by type + value to consolidate identical parts
            var groups = schematic.Components.Values
                .GroupBy(c => $"{c.GetType().Name}|{c.Value}")
                .OrderBy(g => g.Key);

            var bom = new List<BomLineItem>();
            int lineNum = 1;

            foreach (var group in groups)
            {
                var first = group.First();
                var designators = group.Select(c => c.Designator).OrderBy(d => d).ToList();

                bom.Add(new BomLineItem
                {
                    LineNumber   = lineNum++,
                    Quantity     = group.Count(),
                    Designators  = designators,
                    ComponentType= first.GetType().Name,
                    Value        = first.Value,
                    Description  = GenerateDescription(first),
                    Manufacturer = SuggestManufacturer(first),
                    PartNumber   = SuggestPartNumber(first),
                    Footprint    = SuggestFootprint(first),
                    Package      = SuggestPackage(first),
                    MountType    = SuggestMountType(first),
                    DigiKeyPN    = SuggestDigiKeyPn(first),
                    MouserPN     = SuggestMouserPn(first)
                });
            }

            return bom;
        }

        /// <summary>Exports BOM as a CSV string (Excel-compatible).</summary>
        public static string ToCsv(List<BomLineItem> bom)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Line,Qty,Designators,Type,Value,Description,Manufacturer,Part Number,Footprint,Package,Mount,DigiKey PN,Mouser PN");

            foreach (var item in bom)
            {
                string designators = string.Join(" ", item.Designators);
                sb.AppendLine($"{item.LineNumber},{item.Quantity},\"{designators}\",{item.ComponentType},{item.Value}," +
                              $"\"{item.Description}\",\"{item.Manufacturer}\",{item.PartNumber}," +
                              $"{item.Footprint},{item.Package},{item.MountType},{item.DigiKeyPN},{item.MouserPN}");
            }

            return sb.ToString();
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Pick & Place (Centroid) Export
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Generates a Pick-and-Place centroid CSV for SMT assembly machines.
        /// Format: JLCPCB / IPC-7711B compatible.
        /// </summary>
        public static string GeneratePickAndPlace(PcbDocument pcb)
        {
            ArgumentNullException.ThrowIfNull(pcb);
            var sb = new StringBuilder();
            sb.AppendLine("Designator,Val,Package,Mid X(mm),Mid Y(mm),Rotation,Layer");

            foreach (var fp in pcb.Footprints)
            {
                string layer = fp.IsMirrored ? "Bottom" : "Top";
                sb.AppendLine($"{fp.Designator},{fp.Value},{fp.FootprintId}," +
                              $"{fp.X:F4},{fp.Y:F4},{fp.Rotation:F1},{layer}");
            }

            return sb.ToString();
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Smart Part Lookup Helpers (offline reference library)
        // ──────────────────────────────────────────────────────────────────────────────

        private static string GenerateDescription(Component c)
        {
            return c.GetType().Name switch
            {
                "Resistor"      => $"Resistor {c.Value} 1% 0.1W",
                "Capacitor"     => $"Capacitor {c.Value} 25V X7R",
                "Inductor"      => $"Inductor {c.Value} 2A",
                "VoltageSource" => $"Voltage Source {c.Value}",
                "CurrentSource" => $"Current Source {c.Value}",
                "Diode"         => $"Diode {c.Value}",
                "BJT"           => $"NPN BJT {c.Value}",
                "MOSFET"        => $"N-Channel MOSFET {c.Value}",
                "OpAmp"         => $"Operational Amplifier {c.Value}",
                _               => c.GetType().Name
            };
        }

        private static string SuggestManufacturer(Component c) => c.GetType().Name switch
        {
            "Resistor"  => "Yageo",
            "Capacitor" => "Murata",
            "Inductor"  => "TDK",
            "Diode"     => "ON Semiconductor",
            "BJT"       => "ON Semiconductor",
            "MOSFET"    => "Vishay",
            "OpAmp"     => "Texas Instruments",
            _           => "Generic"
        };

        private static string SuggestPartNumber(Component c) => c.GetType().Name switch
        {
            "Resistor"  => $"RC0402FR-07{c.Value.Replace("k", "KL").Replace("M", "ML")}L",
            "Capacitor" => "GCM155R71H104KE02D",
            "Inductor"  => "LQM21FN100N80L",
            "Diode"     => c.Value == "1N4148" ? "1N4148W-7-F" : c.Value,
            "BJT"       => c.Value == "2N2222" ? "2N2222ATF" : c.Value,
            "MOSFET"    => c.Value == "2N7002" ? "2N7002-7-F" : c.Value,
            "OpAmp"     => c.Value.Contains("358") ? "LM358DR" : c.Value,
            _           => c.Value
        };

        private static string SuggestFootprint(Component c) => c.GetType().Name switch
        {
            "Resistor"  or "Capacitor"   => "Resistor_SMD:R_0402_1005Metric",
            "Inductor"  => "Inductor_SMD:L_0402_1005Metric",
            "Diode"     => "Diode_SMD:D_SOD-323",
            "BJT"       => "Package_TO_SOT_SMD:SOT-23",
            "MOSFET"    => "Package_TO_SOT_SMD:SOT-23",
            "OpAmp"     => "Package_SO:SOIC-8_3.9x4.9mm_P1.27mm",
            _           => "THT:Generic"
        };

        private static string SuggestPackage(Component c) => c.GetType().Name switch
        {
            "Resistor"  or "Capacitor" => "0402",
            "Inductor"  => "0402",
            "Diode"     => "SOD-323",
            "BJT"       => "SOT-23",
            "MOSFET"    => "SOT-23",
            "OpAmp"     => "SOIC-8",
            _           => "DIP"
        };

        private static string SuggestMountType(Component c) => c.GetType().Name switch
        {
            "VoltageSource" or "CurrentSource" => "THT",
            _                                  => "SMD"
        };

        private static string SuggestDigiKeyPn(Component c) => c.GetType().Name switch
        {
            "Resistor"  => "311-0.0ERCT-ND",
            "Capacitor" => "490-GCM155R71H104KE02DCT-ND",
            "OpAmp"     => "296-1179-5-ND",
            _           => "N/A"
        };

        private static string SuggestMouserPn(Component c) => c.GetType().Name switch
        {
            "Resistor"  => "603-RC0402FR-070RL",
            "Capacitor" => "81-GCM155R71H104KE2D",
            "OpAmp"     => "926-LM358DR",
            _           => "N/A"
        };
    }

    public class BomLineItem
    {
        public int          LineNumber    { get; set; }
        public int          Quantity      { get; set; }
        public List<string> Designators   { get; set; } = new();
        public string       ComponentType { get; set; } = "";
        public string       Value         { get; set; } = "";
        public string       Description   { get; set; } = "";
        public string       Manufacturer  { get; set; } = "";
        public string       PartNumber    { get; set; } = "";
        public string       Footprint     { get; set; } = "";
        public string       Package       { get; set; } = "";
        public string       MountType     { get; set; } = "";
        public string       DigiKeyPN     { get; set; } = "";
        public string       MouserPN      { get; set; } = "";
    }
}

