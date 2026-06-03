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
                int qty = group.Count();

                double unitPrice = SuggestUnitPrice(first);
                string stock = SuggestStock(first);
                string distributor = lineNum % 2 == 0 ? "DigiKey" : "Mouser";
                string partNo = SuggestPartNumber(first);
                string distUrl = distributor == "DigiKey"
                    ? $"https://www.digikey.com/en/products?keywords={Uri.EscapeDataString(partNo)}"
                    : $"https://www.mouser.com/Search/Refine?Keyword={Uri.EscapeDataString(partNo)}";

                bom.Add(new BomLineItem
                {
                    LineNumber   = lineNum++,
                    Quantity     = qty,
                    Designators  = designators,
                    ComponentType= first.GetType().Name,
                    Value        = first.Value,
                    Description  = GenerateDescription(first),
                    Manufacturer = SuggestManufacturer(first),
                    PartNumber   = partNo,
                    Footprint    = SuggestFootprint(first),
                    Package      = SuggestPackage(first),
                    MountType    = SuggestMountType(first),
                    DigiKeyPN    = SuggestDigiKeyPn(first),
                    MouserPN     = SuggestMouserPn(first),
                    UnitPrice    = unitPrice,
                    TotalPrice   = unitPrice * qty,
                    Stock        = stock,
                    Distributor  = distributor,
                    DistributorUrl = distUrl
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

        /// <summary>Exports BOM as a beautifully styled PDF document.</summary>
        public static void ExportBomToPdf(List<BomLineItem> bom, string projectTitle, string targetFilePath)
        {
            var pdf = new SimplePdfDocument();
            
            int itemsPerPage = 42;
            int pageCount = (int)System.Math.Ceiling(bom.Count / (double)itemsPerPage);
            if (pageCount == 0) pageCount = 1;

            for (int p = 0; p < pageCount; p++)
            {
                var pageContent = new StringBuilder();
                
                // Header (only on page 1)
                if (p == 0)
                {
                    pageContent.AppendLine("BT");
                    pageContent.AppendLine("/F2 18 Tf");
                    pageContent.AppendLine("0.17 0.24 0.31 rg");
                    pageContent.AppendLine("50 780 Td");
                    pageContent.AppendLine($"({EscapePdfString(projectTitle)} - BILL OF MATERIALS) Tj");
                    pageContent.AppendLine("ET");

                    pageContent.AppendLine("BT");
                    pageContent.AppendLine("/F1 10 Tf");
                    pageContent.AppendLine("0.4 0.4 0.4 rg");
                    pageContent.AppendLine("50 760 Td");
                    pageContent.AppendLine($"({DateTime.Now:yyyy-MM-dd HH:mm} | Total Line Items: {bom.Count} | Total Parts: {bom.Sum(b => b.Quantity)}) Tj");
                    pageContent.AppendLine("ET");

                    pageContent.AppendLine("0.8 0.8 0.8 RG");
                    pageContent.AppendLine("0.5 w");
                    pageContent.AppendLine("50 745 m");
                    pageContent.AppendLine("545 745 l");
                    pageContent.AppendLine("S");
                }
                
                double yStart = (p == 0) ? 725 : 780;

                // Table Header
                pageContent.AppendLine("BT");
                pageContent.AppendLine("/F2 9 Tf");
                pageContent.AppendLine("0.17 0.24 0.31 rg");
                pageContent.AppendLine($"50 {yStart} Td");
                pageContent.AppendLine("(Line) Tj");
                pageContent.AppendLine("30 0 Td");
                pageContent.AppendLine("(Qty) Tj");
                pageContent.AppendLine("35 0 Td");
                pageContent.AppendLine("(Designators) Tj");
                pageContent.AppendLine("120 0 Td");
                pageContent.AppendLine("(Component Type) Tj");
                pageContent.AppendLine("100 0 Td");
                pageContent.AppendLine("(Value) Tj");
                pageContent.AppendLine("80 0 Td");
                pageContent.AppendLine("(Part Number) Tj");
                pageContent.AppendLine("ET");

                pageContent.AppendLine("0.17 0.24 0.31 RG");
                pageContent.AppendLine("1 w");
                pageContent.AppendLine($"50 {yStart - 5} m");
                pageContent.AppendLine($"545 {yStart - 5} l");
                pageContent.AppendLine("S");

                double y = yStart - 20;
                int startIdx = p * itemsPerPage;
                int endIdx = SMath.Min(startIdx + itemsPerPage, bom.Count);

                for (int i = startIdx; i < endIdx; i++)
                {
                    var item = bom[i];
                    
                    if (i % 2 == 1)
                    {
                        pageContent.AppendLine("0.96 0.97 0.98 rg");
                        pageContent.AppendLine($"50 {y - 4} 495 14 re");
                        pageContent.AppendLine("f");
                    }

                    pageContent.AppendLine("BT");
                    pageContent.AppendLine("/F1 8 Tf");
                    pageContent.AppendLine("0.2 0.2 0.2 rg");
                    pageContent.AppendLine($"50 {y} Td");
                    pageContent.AppendLine($"({item.LineNumber}) Tj");
                    pageContent.AppendLine("30 0 Td");
                    pageContent.AppendLine($"({item.Quantity}) Tj");
                    
                    string desigs = string.Join(", ", item.Designators);
                    if (desigs.Length > 28) desigs = desigs.Substring(0, 25) + "...";
                    pageContent.AppendLine("35 0 Td");
                    pageContent.AppendLine($"({EscapePdfString(desigs)}) Tj");

                    string ctype = item.ComponentType;
                    if (ctype.Length > 22) ctype = ctype.Substring(0, 19) + "...";
                    pageContent.AppendLine("120 0 Td");
                    pageContent.AppendLine($"({EscapePdfString(ctype)}) Tj");

                    string val = item.Value;
                    if (val.Length > 18) val = val.Substring(0, 15) + "...";
                    pageContent.AppendLine("100 0 Td");
                    pageContent.AppendLine($"({EscapePdfString(val)}) Tj");

                    string partNum = item.PartNumber;
                    if (partNum.Length > 28) partNum = partNum.Substring(0, 25) + "...";
                    pageContent.AppendLine("80 0 Td");
                    pageContent.AppendLine($"({EscapePdfString(partNum)}) Tj");
                    
                    pageContent.AppendLine("ET");

                    y -= 15;
                }

                // Footer page numbering
                pageContent.AppendLine("BT");
                pageContent.AppendLine("/F1 8 Tf");
                pageContent.AppendLine("0.5 0.5 0.5 rg");
                pageContent.AppendLine($"270 40 Td");
                pageContent.AppendLine($"(Page {p + 1} of {pageCount}) Tj");
                pageContent.AppendLine("ET");

                pdf.AddPage(pageContent.ToString());
            }

            pdf.Save(targetFilePath);
        }

        private static string EscapePdfString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
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

        private static double SuggestUnitPrice(Component c) => c.GetType().Name switch
        {
            "Resistor"  => 0.012,
            "Capacitor" => 0.024,
            "Inductor"  => 0.150,
            "Diode"     => 0.052,
            "BJT"       => 0.085,
            "MOSFET"    => 0.185,
            "OpAmp"     => 0.350,
            _           => 0.100
        };

        private static string SuggestStock(Component c) => c.GetType().Name switch
        {
            "Resistor"  => "150,000+",
            "Capacitor" => "82,000+",
            "Inductor"  => "12,500",
            "Diode"     => "45,000+",
            "BJT"       => "31,000",
            "MOSFET"    => "24,000",
            "OpAmp"     => "8,500",
            _           => "5,000"
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
        public double       UnitPrice     { get; set; }
        public double       TotalPrice    { get; set; }
        public string       Stock         { get; set; } = "";
        public string       Distributor   { get; set; } = "";
        public string       DistributorUrl { get; set; } = "";
        public string       DesignatorString => string.Join(", ", Designators);
    }

    /// <summary>
    /// A lightweight, self-contained PDF generator for compiling table documents.
    /// Requires no external dependencies or assemblies.
    /// </summary>
    public class SimplePdfDocument
    {
        private readonly List<string> _pages = new();

        public void AddPage(string content)
        {
            _pages.Add(content);
        }

        public void Save(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(fs, Encoding.ASCII);

            var offsets = new List<long>();

            // Header
            writer.Write("%PDF-1.4\n");
            writer.Flush();

            // 1. Catalog Object
            offsets.Add(fs.Position);
            writer.Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            writer.Flush();

            // 2. Pages Object
            offsets.Add(fs.Position);
            var kidsList = new StringBuilder();
            for (int i = 0; i < _pages.Count; i++)
            {
                kidsList.Append($"{3 + i * 2} 0 R ");
            }
            writer.Write($"2 0 obj\n<< /Type /Pages /Kids [ {kidsList.ToString().Trim()} ] /Count {_pages.Count} >>\nendobj\n");
            writer.Flush();

            int font1Idx = 3 + _pages.Count * 2;
            int font2Idx = 4 + _pages.Count * 2;

            for (int i = 0; i < _pages.Count; i++)
            {
                int pageObjIdx = 3 + i * 2;
                int streamObjIdx = 4 + i * 2;

                // Page Object
                offsets.Add(fs.Position);
                writer.Write($"{pageObjIdx} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [ 0 0 595 842 ] /Contents {streamObjIdx} 0 R /Resources << /Font << /F1 {font1Idx} 0 R /F2 {font2Idx} 0 R >> >> >>\nendobj\n");
                writer.Flush();

                // Stream content
                byte[] streamBytes = Encoding.ASCII.GetBytes(_pages[i]);
                
                offsets.Add(fs.Position);
                writer.Write($"{streamObjIdx} 0 obj\n<< /Length {streamBytes.Length} >>\nstream\n");
                writer.Flush();
                fs.Write(streamBytes, 0, streamBytes.Length);
                writer.Write("\nendstream\nendobj\n");
                writer.Flush();
            }

            // Font Helvetica
            offsets.Add(fs.Position);
            writer.Write($"{font1Idx} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
            writer.Flush();

            // Font Helvetica-Bold
            offsets.Add(fs.Position);
            writer.Write($"{font2Idx} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>\nendobj\n");
            writer.Flush();

            // Cross-Reference Table
            long xrefOffset = fs.Position;
            int totalObjects = offsets.Count + 1; // including 0 object
            writer.Write($"xref\n0 {totalObjects}\n0000000000 65535 f \n");
            foreach (var offset in offsets)
            {
                writer.Write($"{offset:D10} 00000 n \n");
            }
            writer.Flush();

            // Trailer
            writer.Write($"trailer\n<< /Size {totalObjects} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
            writer.Flush();
        }
    }
}

